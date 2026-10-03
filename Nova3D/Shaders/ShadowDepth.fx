#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 World;
float4x4 LightViewProjection;
#define MAX_JOINTS 48
float4x4 JointPalette[MAX_JOINTS];

struct VertexShaderInput
{
    float4 Position : POSITION0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float Depth : TEXCOORD0;
};

struct SkinnedVertexShaderInput
{
    float4 Position : POSITION0;
    float4 Joints : BLENDINDICES0;
    float4 Weights : BLENDWEIGHT0;
};

VertexShaderOutput BuildVertexOutput(float4 localPosition)
{
    VertexShaderOutput output;
    float4 clipPosition = mul(mul(localPosition, World), LightViewProjection);
    output.Position = clipPosition;
    output.Depth = clipPosition.z / clipPosition.w;
#if OPENGL
    output.Depth = output.Depth * 0.5 + 0.5;
#endif
    return output;
}

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    return BuildVertexOutput(input.Position);
}

VertexShaderOutput SkinnedVertexShaderFunction(SkinnedVertexShaderInput input)
{
    float4x4 skin = JointPalette[(int)input.Joints.x] * input.Weights.x +
                    JointPalette[(int)input.Joints.y] * input.Weights.y +
                    JointPalette[(int)input.Joints.z] * input.Weights.z +
                    JointPalette[(int)input.Joints.w] * input.Weights.w;
    return BuildVertexOutput(mul(input.Position, skin));
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    return float4(input.Depth, input.Depth, input.Depth, 1.0);
}

technique ShadowDepth
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderFunction();
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}

technique SkinnedShadowDepth
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL SkinnedVertexShaderFunction();
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}
