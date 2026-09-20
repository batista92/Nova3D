#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif
float4x4 ViewProjection,View,LightViewProjection0,LightViewProjection1,LightViewProjection2,LightViewProjection3;
float3 LightDirection; float2 ShadowMapTexelSize,ShadowFadeRange; float4 CascadeSplits,CascadeBlendStarts; float ShadowDebugMode;
texture ShadowMap0; sampler2D S0=sampler_state{Texture=<ShadowMap0>;MinFilter=Point;MagFilter=Point;MipFilter=None;AddressU=Clamp;AddressV=Clamp;};
texture ShadowMap1; sampler2D S1=sampler_state{Texture=<ShadowMap1>;MinFilter=Point;MagFilter=Point;MipFilter=None;AddressU=Clamp;AddressV=Clamp;};
texture ShadowMap2; sampler2D S2=sampler_state{Texture=<ShadowMap2>;MinFilter=Point;MagFilter=Point;MipFilter=None;AddressU=Clamp;AddressV=Clamp;};
texture ShadowMap3; sampler2D S3=sampler_state{Texture=<ShadowMap3>;MinFilter=Point;MagFilter=Point;MipFilter=None;AddressU=Clamp;AddressV=Clamp;};
struct VI{float4 Position:POSITION0;float3 Normal:NORMAL0;float4 Color:COLOR0;};
struct VO{float4 Position:SV_POSITION;float3 Normal:TEXCOORD0;float4 Color:COLOR0;float4 L0:TEXCOORD1;float4 L1:TEXCOORD2;float4 L2:TEXCOORD3;float4 L3:TEXCOORD4;float Depth:TEXCOORD5;};
VO VS(VI i){VO o;o.Position=mul(i.Position,ViewProjection);o.Normal=i.Normal;o.Color=i.Color;float4 p=float4(i.Position.xyz+i.Normal*.35,1);o.L0=mul(p,LightViewProjection0);o.L1=mul(p,LightViewProjection1);o.L2=mul(p,LightViewProjection2);o.L3=mul(p,LightViewProjection3);o.Depth=-mul(i.Position,View).z;return o;}
float Sample(sampler2D s,float4 lp,float bias,float radius){float3 p=lp.xyz/lp.w;
#if OPENGL
p.z=p.z*.5+.5;
#endif
float2 uv=p.xy*float2(.5,-.5)+.5;if(p.z<=0||p.z>=1||uv.x<=0||uv.x>=1||uv.y<=0||uv.y>=1)return 1;float v=0;for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)v+=p.z-bias<=tex2Dlod(s,float4(uv+float2(x,y)*ShadowMapTexelSize*radius,0,0)).r?1:0;return v/9;}
float Inside(float4 lp){float3 p=lp.xyz/lp.w;
#if OPENGL
p.z=p.z*.5+.5;
#endif
float2 uv=p.xy*float2(.5,-.5)+.5;return uv.x>0&&uv.x<1&&uv.y>0&&uv.y<1&&p.z>0&&p.z<1?1:0;}
int CoveredCascade(VO i){return Inside(i.L0)>.5?0:Inside(i.L1)>.5?1:Inside(i.L2)>.5?2:3;}
float Shadow(VO i,float nl){int c=CoveredCascade(i);float scale=c==0?1:c==1?1.5:c==2?2.3:3.5;float radius=c==0?.8:c==1?1:c==2?1.25:1.6;float b=max(.00025,.0012*(1-nl))*scale;float v=c==0?Sample(S0,i.L0,b,radius):c==1?Sample(S1,i.L1,b,radius):c==2?Sample(S2,i.L2,b,radius):Sample(S3,i.L3,b,radius);return lerp(v,1,saturate((i.Depth-ShadowFadeRange.x)/(ShadowFadeRange.y-ShadowFadeRange.x)));}
int CascadeIndex(float depth){return depth<CascadeSplits.x?0:depth<CascadeSplits.y?1:depth<CascadeSplits.z?2:3;}
float4 CascadeColor(int c){return c==0?float4(1,0,0,1):c==1?float4(0,1,0,1):c==2?float4(0,0,1,1):float4(1,1,0,1);}
float4 Coverage(VO i,int c){float4 lp=c==0?i.L0:c==1?i.L1:c==2?i.L2:i.L3;float3 p=lp.xyz/lp.w;
#if OPENGL
p.z=p.z*.5+.5;
#endif
float2 uv=p.xy*float2(.5,-.5)+.5;if(uv.x<=0)return float4(1,0,0,1);if(uv.x>=1)return float4(1,1,0,1);if(uv.y<=0)return float4(0,0,1,1);if(uv.y>=1)return float4(1,0,1,1);if(p.z<=0)return float4(0,1,1,1);if(p.z>=1)return float4(1,.35,0,1);return float4(0,1,0,1);}
float4 PS(VO i):COLOR0{float nl=max(dot(normalize(i.Normal),normalize(-LightDirection)),0);int c=CoveredCascade(i);if(ShadowDebugMode>.5&&ShadowDebugMode<1.5)return CascadeColor(c);if(ShadowDebugMode>1.5&&ShadowDebugMode<2.5)return Coverage(i,c);if(ShadowDebugMode>3.5){float any=max(max(Inside(i.L0),Inside(i.L1)),max(Inside(i.L2),Inside(i.L3)));return any>.5?float4(0,1,0,1):float4(1,0,0,1);}float raw=Shadow(i,nl);if(ShadowDebugMode>2.5)return float4(raw,raw,raw,1);float sh=lerp(.45,1,raw);float3 color=saturate(i.Color.rgb*(.32+nl*.68*sh));float fog=saturate((i.Depth-1500)/1400);color=lerp(color,float3(.38,.42,.47),fog);return float4(color,1);}
technique LargeWorld{pass P0{VertexShader=compile VS_SHADERMODEL VS();PixelShader=compile PS_SHADERMODEL PS();}}
