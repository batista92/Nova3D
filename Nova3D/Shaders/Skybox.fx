#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 World;
float4x4 View;
float4x4 Projection;
float Exposure;
texture EnvironmentMap;

samplerCUBE EnvironmentSampler = sampler_state
{
    Texture = <EnvironmentMap>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float3 Direction : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Direction = input.Position.xyz;
    output.Position = mul(mul(mul(input.Position, World), View), Projection);
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float3 color = texCUBE(EnvironmentSampler, normalize(input.Direction)).rgb * Exposure;
    float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
    color *= (luminance / (1.0 + luminance)) / max(luminance, 0.0001);
    color = pow(saturate(color), 1.0 / 2.2);
    return float4(color, 1.0);
}

technique Skybox
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderFunction();
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}
