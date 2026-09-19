#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 ViewProjection;
float3 LightDirection;
float3 CameraPosition;
float4x4 LightViewProjection;
float2 ShadowMapTexelSize;
texture ShadowMap;
sampler2D ShadowSampler = sampler_state { Texture=<ShadowMap>; MinFilter=Point; MagFilter=Point; MipFilter=None; AddressU=Clamp; AddressV=Clamp; };

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float4 Color : COLOR0;
    float4 InstanceRow0 : TEXCOORD1;
    float4 InstanceRow1 : TEXCOORD2;
    float4 InstanceRow2 : TEXCOORD3;
    float4 InstanceRow3 : TEXCOORD4;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float3 Normal : TEXCOORD0;
    float4 Color : COLOR0;
    float Fog : TEXCOORD1;
    float4 LightPosition : TEXCOORD2;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    float4x4 instanceWorld = float4x4(
        input.InstanceRow0, input.InstanceRow1, input.InstanceRow2, input.InstanceRow3);
    float4 worldPosition = mul(input.Position, instanceWorld);
    output.Position = mul(worldPosition, ViewProjection);
    output.Normal = normalize(mul(float4(input.Normal, 0.0), instanceWorld).xyz);
    output.Color = input.Color;
    output.Fog = saturate((distance(CameraPosition, worldPosition.xyz) - 900.0) / 650.0);
    output.LightPosition = mul(float4(worldPosition.xyz + output.Normal * 0.32, 1.0), LightViewProjection);
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float nDotL = max(dot(normalize(input.Normal), normalize(-LightDirection)), 0.0);
    float3 p = input.LightPosition.xyz / input.LightPosition.w;
    float2 uv = p.xy * float2(0.5, -0.5) + 0.5;
    float shadow = 1.0;
    if (p.z > 0 && p.z < 1 && uv.x > 0 && uv.x < 1 && uv.y > 0 && uv.y < 1)
    {
        shadow = 0;
        float bias = max(0.00065, 0.003 * (1-nDotL));
        for (int y=-1; y<=1; y++) for (int x=-1; x<=1; x++)
            shadow += p.z-bias <= tex2Dlod(ShadowSampler,float4(uv+float2(x,y)*ShadowMapTexelSize,0,0)).r ? 1 : 0;
        shadow /= 9;
    }
    shadow = lerp(0.32, 1.0, shadow);
    float3 color = input.Color.rgb * (0.22 + nDotL * 0.78 * shadow);
    color = lerp(color, float3(0.38, 0.42, 0.47), input.Fog);
    color = pow(saturate(color), 1.0 / 2.2);
    return float4(color, 1.0);
}

technique Vegetation
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderFunction();
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}
