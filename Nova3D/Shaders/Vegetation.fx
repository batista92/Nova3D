#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 ViewProjection;
float4x4 View;
float4x4 LightViewProjection0;
float4x4 LightViewProjection1;
float4x4 LightViewProjection2;
float4x4 LightViewProjection3;
float3 LightDirection;
float3 CameraPosition;
float2 ShadowMapTexelSize;
float2 ShadowFadeRange;
float4 CascadeSplits;
float4 CascadeBlendStarts;
float ShadowDebugMode;
float MaterialMode;
texture ShadowMap0; sampler2D ShadowSampler0=sampler_state{Texture=<ShadowMap0>;MinFilter=Point;MagFilter=Point;MipFilter=None;AddressU=Clamp;AddressV=Clamp;};
texture ShadowMap1; sampler2D ShadowSampler1=sampler_state{Texture=<ShadowMap1>;MinFilter=Point;MagFilter=Point;MipFilter=None;AddressU=Clamp;AddressV=Clamp;};
texture ShadowMap2; sampler2D ShadowSampler2=sampler_state{Texture=<ShadowMap2>;MinFilter=Point;MagFilter=Point;MipFilter=None;AddressU=Clamp;AddressV=Clamp;};
texture ShadowMap3; sampler2D ShadowSampler3=sampler_state{Texture=<ShadowMap3>;MinFilter=Point;MagFilter=Point;MipFilter=None;AddressU=Clamp;AddressV=Clamp;};

struct VertexShaderInput
{
    float4 Position:POSITION0; float3 Normal:NORMAL0; float4 Color:COLOR0;
    float4 InstanceRow0:TEXCOORD1; float4 InstanceRow1:TEXCOORD2;
    float4 InstanceRow2:TEXCOORD3; float4 InstanceRow3:TEXCOORD4;
};

struct VertexShaderOutput
{
    float4 Position:SV_POSITION; float3 Normal:TEXCOORD0; float Fog:TEXCOORD1;
    float4 LightPosition0:TEXCOORD2; float4 LightPosition1:TEXCOORD3;
    float4 LightPosition2:TEXCOORD4; float4 LightPosition3:TEXCOORD5;
    float ViewDepth:TEXCOORD6; float3 WorldPosition:TEXCOORD7; float4 Color:COLOR0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    float4x4 instanceWorld=float4x4(input.InstanceRow0,input.InstanceRow1,input.InstanceRow2,input.InstanceRow3);
    float4 worldPosition=mul(input.Position,instanceWorld);
    output.Position=mul(worldPosition,ViewProjection);
    output.WorldPosition=worldPosition.xyz;
    output.Normal=normalize(mul(float4(input.Normal,0.0),instanceWorld).xyz);
    output.Color=input.Color;
    output.Fog=saturate((distance(CameraPosition,worldPosition.xyz)-900.0)/650.0);
    output.ViewDepth=-mul(worldPosition,View).z;
    float4 shadowPosition=float4(worldPosition.xyz+output.Normal*0.32,1.0);
    output.LightPosition0=mul(shadowPosition,LightViewProjection0);
    output.LightPosition1=mul(shadowPosition,LightViewProjection1);
    output.LightPosition2=mul(shadowPosition,LightViewProjection2);
    output.LightPosition3=mul(shadowPosition,LightViewProjection3);
    return output;
}

float SampleShadow(sampler2D shadowSampler,float4 lightPosition,float bias,float radius)
{
    float3 p=lightPosition.xyz/lightPosition.w;
#if OPENGL
    p.z=p.z*0.5+0.5;
#endif
    float2 uv=p.xy*float2(0.5,-0.5)+0.5;
    if(p.z<=0||p.z>=1||uv.x<=0||uv.x>=1||uv.y<=0||uv.y>=1)return 1.0;
    float shadow=0.0;
    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
        shadow+=p.z-bias<=tex2Dlod(shadowSampler,float4(uv+float2(x,y)*ShadowMapTexelSize*radius,0,0)).r?1.0:0.0;
    return shadow/9.0;
}

float InsideShadow(float4 lightPosition)
{
    float3 p=lightPosition.xyz/lightPosition.w;
#if OPENGL
    p.z=p.z*0.5+0.5;
#endif
    float2 uv=p.xy*float2(0.5,-0.5)+0.5;
    return uv.x>0&&uv.x<1&&uv.y>0&&uv.y<1&&p.z>0&&p.z<1?1.0:0.0;
}

float CascadedShadow(VertexShaderOutput input,float nDotL)
{
    int cascade=InsideShadow(input.LightPosition0)>0.5?0:InsideShadow(input.LightPosition1)>0.5?1:InsideShadow(input.LightPosition2)>0.5?2:3;
    float biasScale=cascade==0?1.0:cascade==1?1.5:cascade==2?2.3:3.5;
    float radius=cascade==0?0.8:cascade==1?1.0:cascade==2?1.25:1.6;
    float bias=max(0.0003,0.0014*(1.0-nDotL))*biasScale;
    float shadow,next,blend;
    if(cascade==0)shadow=SampleShadow(ShadowSampler0,input.LightPosition0,bias,radius);
    else if(cascade==1)shadow=SampleShadow(ShadowSampler1,input.LightPosition1,bias,radius);
    else if(cascade==2)shadow=SampleShadow(ShadowSampler2,input.LightPosition2,bias,radius);
    else shadow=SampleShadow(ShadowSampler3,input.LightPosition3,bias,radius);
    return lerp(shadow,1.0,saturate((input.ViewDepth-ShadowFadeRange.x)/(ShadowFadeRange.y-ShadowFadeRange.x)));
}

float4 PixelShaderFunction(VertexShaderOutput input):COLOR0
{
    float nDotL=max(dot(normalize(input.Normal),normalize(-LightDirection)),0.0);
    int cascade=InsideShadow(input.LightPosition0)>0.5?0:InsideShadow(input.LightPosition1)>0.5?1:InsideShadow(input.LightPosition2)>0.5?2:3;
    if(ShadowDebugMode>0.5&&ShadowDebugMode<1.5)
        return cascade==0?float4(1,0,0,1):cascade==1?float4(0,1,0,1):cascade==2?float4(0,0,1,1):float4(1,1,0,1);
    if(ShadowDebugMode>1.5&&ShadowDebugMode<2.5)
    {
        float4 lp=cascade==0?input.LightPosition0:cascade==1?input.LightPosition1:cascade==2?input.LightPosition2:input.LightPosition3;
        float3 p=lp.xyz/lp.w;
#if OPENGL
        p.z=p.z*0.5+0.5;
#endif
        float2 uv=p.xy*float2(0.5,-0.5)+0.5;
        if(uv.x<=0)return float4(1,0,0,1);
        if(uv.x>=1)return float4(1,1,0,1);
        if(uv.y<=0)return float4(0,0,1,1);
        if(uv.y>=1)return float4(1,0,1,1);
        if(p.z<=0)return float4(0,1,1,1);
        if(p.z>=1)return float4(1,0.35,0,1);
        return float4(0,1,0,1);
    }
    if(ShadowDebugMode>3.5)
    {
        float4 positions[4]={input.LightPosition0,input.LightPosition1,input.LightPosition2,input.LightPosition3};
        float anyCoverage=0.0;
        for(int map=0;map<4;map++)
        {
            float3 test=positions[map].xyz/positions[map].w;
#if OPENGL
            test.z=test.z*0.5+0.5;
#endif
            float2 testUv=test.xy*float2(0.5,-0.5)+0.5;
            anyCoverage=max(anyCoverage,testUv.x>0&&testUv.x<1&&testUv.y>0&&testUv.y<1&&test.z>0&&test.z<1?1.0:0.0);
        }
        return anyCoverage>0.5?float4(0,1,0,1):float4(1,0,0,1);
    }
    float rawShadow=CascadedShadow(input,nDotL);
    if(ShadowDebugMode>2.5)return float4(rawShadow,rawShadow,rawShadow,1);
    float shadow=lerp(0.45,1.0,rawShadow);
    float3 color;
    if(MaterialMode>0.5)
    {
        float3 n=normalize(input.Normal);
        float3 v=normalize(CameraPosition-input.WorldPosition);
        float3 l=normalize(-LightDirection);
        float3 h=normalize(v+l);
        float roughness=0.68;
        float specular=pow(max(dot(n,h),0.0),lerp(72.0,8.0,roughness))*(1.0-roughness)*0.35;
        color=input.Color.rgb*(0.22+nDotL*0.78*shadow)+specular*shadow;
    }
    else
        color=input.Color.rgb*(0.24+nDotL*0.76*shadow);
    color=lerp(color,float3(0.38,0.42,0.47),input.Fog);
    color=pow(saturate(color),1.0/2.2);
    return float4(color,1.0);
}

technique Vegetation
{
    pass Pass0
    {
        VertexShader=compile VS_SHADERMODEL VertexShaderFunction();
        PixelShader=compile PS_SHADERMODEL PixelShaderFunction();
    }
}
