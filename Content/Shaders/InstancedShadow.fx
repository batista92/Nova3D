#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 LightViewProjection;
struct VSIn { float4 Position:POSITION0; float4 R0:TEXCOORD1; float4 R1:TEXCOORD2; float4 R2:TEXCOORD3; float4 R3:TEXCOORD4; };
struct VSOut { float4 Position:SV_POSITION; float Depth:TEXCOORD0; };
VSOut VSMain(VSIn input)
{
    VSOut output; float4x4 world=float4x4(input.R0,input.R1,input.R2,input.R3);
    float4 clip=mul(mul(input.Position,world),LightViewProjection);
    output.Position=clip; output.Depth=clip.z/clip.w; return output;
}
float4 PSMain(VSOut input):COLOR0 { return input.Depth.xxxx; }
technique InstancedShadow { pass Pass0 { VertexShader=compile VS_SHADERMODEL VSMain(); PixelShader=compile PS_SHADERMODEL PSMain(); } }
