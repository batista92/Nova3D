#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 ViewProjection;
float4x4 LightViewProjection;
float3 LightDirection;
float2 ShadowMapTexelSize;
texture ShadowMap;
sampler2D ShadowSampler = sampler_state { Texture=<ShadowMap>; MinFilter=Point; MagFilter=Point; MipFilter=None; AddressU=Clamp; AddressV=Clamp; };

struct VSIn { float4 Position:POSITION0; float3 Normal:NORMAL0; float4 Color:COLOR0; };
struct VSOut { float4 Position:SV_POSITION; float3 Normal:TEXCOORD0; float4 Color:COLOR0; float4 LightPosition:TEXCOORD1; };

VSOut VSMain(VSIn input)
{
    VSOut output;
    output.Position = mul(input.Position, ViewProjection);
    output.Normal = input.Normal;
    output.Color = input.Color;
    // World-space normal offset keeps a low-poly receiver away from its own
    // shadow-map surface and prevents the triangle-shaped acne seen up close.
    output.LightPosition = mul(float4(input.Position.xyz + input.Normal * 1.15, 1), LightViewProjection);
    return output;
}

float ShadowVisibility(float4 lightPosition, float nDotL)
{
    float3 p = lightPosition.xyz / lightPosition.w;
    float2 uv = p.xy * float2(0.5, -0.5) + 0.5;
    if (p.z <= 0 || p.z >= 1 || uv.x <= 0 || uv.x >= 1 || uv.y <= 0 || uv.y >= 1) return 1;
    float visibility = 0;
    float bias = max(0.00065, 0.0035 * (1 - nDotL));
    for (int y=-1; y<=1; y++) for (int x=-1; x<=1; x++)
        visibility += p.z - bias <= tex2Dlod(ShadowSampler, float4(uv + float2(x,y)*ShadowMapTexelSize,0,0)).r ? 1 : 0;
    return visibility / 9;
}

float4 PSMain(VSOut input):COLOR0
{
    float nDotL = max(dot(normalize(input.Normal), normalize(-LightDirection)), 0);
    float shadow = ShadowVisibility(input.LightPosition, nDotL);
    // Preserve some indirect light inside shadows instead of crushing or
    // washing out the original terrain palette.
    shadow = lerp(0.35, 1.0, shadow);
    float3 color = input.Color.rgb * (0.30 + nDotL * 0.70 * shadow);
    return float4(saturate(color), 1);
}

technique LargeWorld { pass Pass0 { VertexShader=compile VS_SHADERMODEL VSMain(); PixelShader=compile PS_SHADERMODEL PSMain(); } }
