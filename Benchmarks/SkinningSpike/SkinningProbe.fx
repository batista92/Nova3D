#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define SV_POSITION SV_POSITION
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// G5.1 compilation probe. Keep this aligned with the documented uniform limit.
#define MAX_JOINTS 48

float4x4 JointPalette[MAX_JOINTS];
float4x4 World;
float4x4 WorldInverseTranspose;
float4x4 View;
float4x4 Projection;
float4x4 LightViewProjection0;
float4x4 LightViewProjection1;
float4x4 LightViewProjection2;
float4x4 LightViewProjection3;

struct VertexInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TextureCoordinate : TEXCOORD0;
    float4 Joints : BLENDINDICES0;
    float4 Weights : BLENDWEIGHT0;
};

struct VertexOutput
{
    float4 Position : SV_POSITION;
    float3 Normal : TEXCOORD0;
    float2 TextureCoordinate : TEXCOORD1;
    float4 Shadow0 : TEXCOORD2;
    float4 Shadow1 : TEXCOORD3;
    float4 Shadow2 : TEXCOORD4;
    float4 Shadow3 : TEXCOORD5;
};

VertexOutput VertexShaderFunction(VertexInput input)
{
    float4x4 skin = JointPalette[(int)input.Joints.x] * input.Weights.x +
                      JointPalette[(int)input.Joints.y] * input.Weights.y +
                      JointPalette[(int)input.Joints.z] * input.Weights.z +
                      JointPalette[(int)input.Joints.w] * input.Weights.w;
    float4 localPosition = mul(input.Position, skin);
    float3 localNormal = mul(float4(input.Normal, 0.0), skin).xyz;
    float4 worldPosition = mul(localPosition, World);
    VertexOutput output;
    output.Normal = normalize(mul(float4(localNormal, 0.0), WorldInverseTranspose).xyz);
    output.TextureCoordinate = input.TextureCoordinate;
    output.Shadow0 = mul(worldPosition, LightViewProjection0);
    output.Shadow1 = mul(worldPosition, LightViewProjection1);
    output.Shadow2 = mul(worldPosition, LightViewProjection2);
    output.Shadow3 = mul(worldPosition, LightViewProjection3);
    output.Position = mul(mul(worldPosition, View), Projection);
    return output;
}

float4 PixelShaderFunction(VertexOutput input) : COLOR0
{
    return float4(abs(normalize(input.Normal)), 1.0);
}

technique SkinningProbe
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderFunction();
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}
