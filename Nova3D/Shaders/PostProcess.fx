#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 MatrixTransform;
float2 SourceTexelSize;
float Exposure = 1.0;
float BloomStrength = 0.18;
texture Texture;
sampler2D SourceSampler = sampler_state { Texture=<Texture>; MinFilter=Linear; MagFilter=Linear; MipFilter=None; AddressU=Clamp; AddressV=Clamp; };
texture BloomTexture;
sampler2D BloomSampler = sampler_state { Texture=<BloomTexture>; MinFilter=Linear; MagFilter=Linear; MipFilter=None; AddressU=Clamp; AddressV=Clamp; };

struct VSIn { float4 Position:POSITION0; float4 Color:COLOR0; float2 TexCoord:TEXCOORD0; };
struct VSOut { float4 Position:SV_POSITION; float4 Color:COLOR0; float2 TexCoord:TEXCOORD0; };
VSOut VSMain(VSIn input) { VSOut o; o.Position=mul(input.Position,MatrixTransform); o.Color=input.Color; o.TexCoord=input.TexCoord; return o; }

float Luma(float3 c) { return dot(c,float3(0.2126,0.7152,0.0722)); }
float4 Extract(VSOut input):COLOR0
{
    float3 c=tex2D(SourceSampler,input.TexCoord).rgb;
    float contribution=saturate((Luma(c)-0.62)/0.38);
    return float4(c*contribution,1);
}

float4 BlurHorizontal(VSOut input):COLOR0
{
    float2 t=float2(SourceTexelSize.x,0); float3 c=tex2D(SourceSampler,input.TexCoord).rgb*0.227027;
    c+=(tex2D(SourceSampler,input.TexCoord+t*1.384615).rgb+tex2D(SourceSampler,input.TexCoord-t*1.384615).rgb)*0.316216;
    c+=(tex2D(SourceSampler,input.TexCoord+t*3.230769).rgb+tex2D(SourceSampler,input.TexCoord-t*3.230769).rgb)*0.070270;
    return float4(c,1);
}
float4 BlurVertical(VSOut input):COLOR0
{
    float2 t=float2(0,SourceTexelSize.y); float3 c=tex2D(SourceSampler,input.TexCoord).rgb*0.227027;
    c+=(tex2D(SourceSampler,input.TexCoord+t*1.384615).rgb+tex2D(SourceSampler,input.TexCoord-t*1.384615).rgb)*0.316216;
    c+=(tex2D(SourceSampler,input.TexCoord+t*3.230769).rgb+tex2D(SourceSampler,input.TexCoord-t*3.230769).rgb)*0.070270;
    return float4(c,1);
}

float3 Fxaa(float2 uv)
{
    float3 m=tex2D(SourceSampler,uv).rgb;
    float3 n=tex2D(SourceSampler,uv+float2(0,-SourceTexelSize.y)).rgb;
    float3 s=tex2D(SourceSampler,uv+float2(0, SourceTexelSize.y)).rgb;
    float3 w=tex2D(SourceSampler,uv+float2(-SourceTexelSize.x,0)).rgb;
    float3 e=tex2D(SourceSampler,uv+float2( SourceTexelSize.x,0)).rgb;
    float lm=Luma(m), contrast=max(max(max(Luma(n),Luma(s)),max(Luma(w),Luma(e))),lm)-min(min(min(Luma(n),Luma(s)),min(Luma(w),Luma(e))),lm);
    return contrast < 0.055 ? m : (m*0.5+(n+s+w+e)*0.125);
}
float3 Aces(float3 x)
{
    const float a=2.51,b=0.03,c=2.43,d=0.59,e=0.14;
    return saturate((x*(a*x+b))/(x*(c*x+d)+e));
}
float4 Composite(VSOut input):COLOR0
{
    // The current forward shaders write display-space colors. Decode them
    // before exposure/ACES, then encode exactly once at the end.
    float3 scene=pow(saturate(Fxaa(input.TexCoord)),2.2);
    float3 bloom=pow(saturate(tex2D(BloomSampler,input.TexCoord).rgb),2.2);
    float3 mapped=Aces((scene+bloom*BloomStrength)*Exposure);
    return float4(pow(mapped,1.0/2.2),1);
}

technique ExtractBloom { pass P0 { VertexShader=compile VS_SHADERMODEL VSMain(); PixelShader=compile PS_SHADERMODEL Extract(); } }
technique BlurH { pass P0 { VertexShader=compile VS_SHADERMODEL VSMain(); PixelShader=compile PS_SHADERMODEL BlurHorizontal(); } }
technique BlurV { pass P0 { VertexShader=compile VS_SHADERMODEL VSMain(); PixelShader=compile PS_SHADERMODEL BlurVertical(); } }
technique CompositeFinal { pass P0 { VertexShader=compile VS_SHADERMODEL VSMain(); PixelShader=compile PS_SHADERMODEL Composite(); } }
