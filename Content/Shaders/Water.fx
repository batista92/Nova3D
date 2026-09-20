#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 ViewProjection;
float3 CameraPosition;
float3 LightDirection;
float Time;

struct VI{float4 Position:POSITION0;float3 Normal:NORMAL0;float4 Color:COLOR0;};
struct VO{float4 Position:SV_POSITION;float3 WorldPosition:TEXCOORD0;float Depth:TEXCOORD1;};
VO VS(VI i){VO o;o.WorldPosition=i.Position.xyz;o.Position=mul(i.Position,ViewProjection);o.Depth=distance(CameraPosition,i.Position.xyz);return o;}

float4 PS(VO i):COLOR0
{
    float2 p=i.WorldPosition.xz;
    float waveX=cos(p.x*.055+Time*.85)*.055+cos((p.x+p.y)*.031-Time*.62)*.031;
    float waveZ=sin(p.y*.047-Time*.72)*.047+cos((p.x-p.y)*.027+Time*.51)*.027;
    float3 n=normalize(float3(-waveX,1,-waveZ));
    float3 v=normalize(CameraPosition-i.WorldPosition);
    float3 l=normalize(-LightDirection);
    float3 h=normalize(v+l);
    float fresnel=.04+.96*pow(1-saturate(dot(n,v)),5);
    float diffuse=.28+.38*max(dot(n,l),0);
    float specular=pow(max(dot(n,h),0),96)*1.8;
    float ripple=.5+.5*sin(p.x*.12+p.y*.09+Time*.9);
    float3 deep=float3(.018,.15,.24);
    float3 shallow=float3(.035,.31,.40);
    float3 sky=float3(.42,.52,.64);
    float3 color=lerp(deep,shallow,.30+ripple*.08)*diffuse;
    color=lerp(color,sky,fresnel*.55)+specular;
    float fog=saturate((i.Depth-1500)/1400);
    color=lerp(color,float3(.38,.42,.47),fog);
    return float4(pow(saturate(color),1/2.2),.82);
}

technique Water{pass P0{VertexShader=compile VS_SHADERMODEL VS();PixelShader=compile PS_SHADERMODEL PS();}}
