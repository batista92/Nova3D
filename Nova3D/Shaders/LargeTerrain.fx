#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 ViewProjection,View,LightViewProjection0,LightViewProjection1,LightViewProjection2,LightViewProjection3;
float3 LightDirection,CameraPosition;
float2 ShadowMapTexelSize,ShadowFadeRange;
float TextureScale,TriplanarSharpness,ShadowDebugMode;

texture GrassAlbedoHeight; sampler2D GrassA=sampler_state{Texture=<GrassAlbedoHeight>;MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;AddressU=Wrap;AddressV=Wrap;};
texture DirtAlbedoHeight; sampler2D DirtA=sampler_state{Texture=<DirtAlbedoHeight>;MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;AddressU=Wrap;AddressV=Wrap;};
texture RockAlbedoHeight; sampler2D RockA=sampler_state{Texture=<RockAlbedoHeight>;MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;AddressU=Wrap;AddressV=Wrap;};
texture SandAlbedoHeight; sampler2D SandA=sampler_state{Texture=<SandAlbedoHeight>;MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;AddressU=Wrap;AddressV=Wrap;};
texture GrassNormalAoRoughness; sampler2D GrassN=sampler_state{Texture=<GrassNormalAoRoughness>;MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;AddressU=Wrap;AddressV=Wrap;};
texture DirtNormalAoRoughness; sampler2D DirtN=sampler_state{Texture=<DirtNormalAoRoughness>;MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;AddressU=Wrap;AddressV=Wrap;};
texture RockNormalAoRoughness; sampler2D RockN=sampler_state{Texture=<RockNormalAoRoughness>;MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;AddressU=Wrap;AddressV=Wrap;};
texture SandNormalAoRoughness; sampler2D SandN=sampler_state{Texture=<SandNormalAoRoughness>;MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;AddressU=Wrap;AddressV=Wrap;};
texture ShadowMap0; sampler2D S0=sampler_state{Texture=<ShadowMap0>;MinFilter=Point;MagFilter=Point;MipFilter=None;AddressU=Clamp;AddressV=Clamp;};
texture ShadowMap1; sampler2D S1=sampler_state{Texture=<ShadowMap1>;MinFilter=Point;MagFilter=Point;MipFilter=None;AddressU=Clamp;AddressV=Clamp;};
texture ShadowMap2; sampler2D S2=sampler_state{Texture=<ShadowMap2>;MinFilter=Point;MagFilter=Point;MipFilter=None;AddressU=Clamp;AddressV=Clamp;};
texture ShadowMap3; sampler2D S3=sampler_state{Texture=<ShadowMap3>;MinFilter=Point;MagFilter=Point;MipFilter=None;AddressU=Clamp;AddressV=Clamp;};

struct VI{float4 Position:POSITION0;float3 Normal:NORMAL0;float4 Color:COLOR0;};
struct VO{float4 Position:SV_POSITION;float3 WorldPosition:TEXCOORD0;float3 Normal:TEXCOORD1;float4 L0:TEXCOORD2;float4 L1:TEXCOORD3;float4 L2:TEXCOORD4;float4 L3:TEXCOORD5;float Depth:TEXCOORD6;};
VO VS(VI i){VO o;o.Position=mul(i.Position,ViewProjection);o.WorldPosition=i.Position.xyz;o.Normal=normalize(i.Normal);float4 p=float4(i.Position.xyz+i.Normal*.35,1);o.L0=mul(p,LightViewProjection0);o.L1=mul(p,LightViewProjection1);o.L2=mul(p,LightViewProjection2);o.L3=mul(p,LightViewProjection3);o.Depth=-mul(i.Position,View).z;return o;}

float Inside(float4 lp){float3 p=lp.xyz/lp.w;
#if OPENGL
p.z=p.z*.5+.5;
#endif
float2 uv=p.xy*float2(.5,-.5)+.5;return uv.x>0&&uv.x<1&&uv.y>0&&uv.y<1&&p.z>0&&p.z<1?1:0;}
int Cascade(VO i){return Inside(i.L0)>.5?0:Inside(i.L1)>.5?1:Inside(i.L2)>.5?2:3;}
float SampleShadow(sampler2D s,float4 lp,float bias,float radius){float3 p=lp.xyz/lp.w;
#if OPENGL
p.z=p.z*.5+.5;
#endif
float2 uv=p.xy*float2(.5,-.5)+.5;if(p.z<=0||p.z>=1||uv.x<=0||uv.x>=1||uv.y<=0||uv.y>=1)return 1;float v=0;for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)v+=p.z-bias<=tex2Dlod(s,float4(uv+float2(x,y)*ShadowMapTexelSize*radius,0,0)).r?1:0;return v/9;}
float Shadow(VO i,float nl){int c=Cascade(i);float scale=c==0?1:c==1?1.5:c==2?2.3:3.5;float radius=c==0?.8:c==1?1:c==2?1.25:1.6;float b=max(.00025,.0012*(1-nl))*scale;float v=c==0?SampleShadow(S0,i.L0,b,radius):c==1?SampleShadow(S1,i.L1,b,radius):c==2?SampleShadow(S2,i.L2,b,radius):SampleShadow(S3,i.L3,b,radius);return lerp(v,1,saturate((i.Depth-ShadowFadeRange.x)/(ShadowFadeRange.y-ShadowFadeRange.x)));}

float4 Tri(sampler2D s,float3 p,float3 w){return tex2D(s,p.zy*TextureScale)*w.x+tex2D(s,p.xz*TextureScale)*w.y+tex2D(s,p.xy*TextureScale)*w.z;}
void TriSurface(sampler2D s,float3 p,float3 n,float3 w,out float3 mappedNormal,out float ao,out float roughness){float4 tx=tex2D(s,p.zy*TextureScale);float4 ty=tex2D(s,p.xz*TextureScale);float4 tz=tex2D(s,p.xy*TextureScale);float2 ex=tx.xy*2-1;float2 ey=ty.xy*2-1;float2 ez=tz.xy*2-1;float3 sx=float3(ex,sqrt(saturate(1-dot(ex,ex))));float3 sy=float3(ey,sqrt(saturate(1-dot(ey,ey))));float3 sz=float3(ez,sqrt(saturate(1-dot(ez,ez))));float3 nx=float3(sx.z*sign(n.x),sx.y,sx.x);float3 ny=float3(sy.x,sy.z*sign(n.y),sy.y);float3 nz=float3(sz.x,sz.y,sz.z*sign(n.z));mappedNormal=normalize(nx*w.x+ny*w.y+nz*w.z);float4 surface=tx*w.x+ty*w.y+tz*w.z;ao=surface.z;roughness=surface.w;}

float4 PS(VO i):COLOR0
{
    float3 gn=normalize(i.Normal);float3 tw=pow(abs(gn),TriplanarSharpness);tw/=max(tw.x+tw.y+tw.z,.0001);
    float slope=1-saturate(gn.y);float rock=smoothstep(.12,.42,slope);float sand=(1-rock)*(1-smoothstep(-8,12,i.WorldPosition.y));float grass=(1-rock)*smoothstep(-2,28,i.WorldPosition.y);float dirt=max(0,1-rock-sand-grass);float4 bw=float4(grass,dirt,rock,sand);bw/=max(dot(bw,1),.0001);
    float4 ga=Tri(GrassA,i.WorldPosition,tw),da=Tri(DirtA,i.WorldPosition,tw),ra=Tri(RockA,i.WorldPosition,tw),sa=Tri(SandA,i.WorldPosition,tw);
    float4 heights=float4(ga.a,da.a,ra.a,sa.a);float4 score=bw*(heights+.35);float highest=max(max(score.x,score.y),max(score.z,score.w));float4 lw=max(score-(highest-.16),0);lw/=max(dot(lw,1),.0001);
    float3 albedo=ga.rgb*lw.x+da.rgb*lw.y+ra.rgb*lw.z+sa.rgb*lw.w;
    float3 ng,nd,nr,ns;float aog,aod,aor,aos;float rg,rd,rr,rs;
    TriSurface(GrassN,i.WorldPosition,gn,tw,ng,aog,rg);TriSurface(DirtN,i.WorldPosition,gn,tw,nd,aod,rd);TriSurface(RockN,i.WorldPosition,gn,tw,nr,aor,rr);TriSurface(SandN,i.WorldPosition,gn,tw,ns,aos,rs);
    float3 n=normalize(ng*lw.x+nd*lw.y+nr*lw.z+ns*lw.w);
    float rough=dot(float4(rg,rd,rr,rs),lw);float ao=dot(float4(aog,aod,aor,aos),lw);
    float3 l=normalize(-LightDirection),v=normalize(CameraPosition-i.WorldPosition),h=normalize(l+v);float nl=max(dot(n,l),0);float spec=pow(max(dot(n,h),0),lerp(48,5,rough))*(1-rough)*.35;float shadow=Shadow(i,max(dot(gn,l),0));
    float3 color=albedo*(.30*ao+.70*nl*lerp(.45,1,shadow))+spec*shadow;
    float fog=saturate((distance(CameraPosition,i.WorldPosition)-1500)/1400);color=lerp(color,float3(.38,.42,.47),fog);
    if(ShadowDebugMode>.5&&ShadowDebugMode<1.5){int c=Cascade(i);return c==0?float4(1,0,0,1):c==1?float4(0,1,0,1):c==2?float4(0,0,1,1):float4(1,1,0,1);}
    if(ShadowDebugMode>2.5&&ShadowDebugMode<3.5)return float4(shadow,shadow,shadow,1);
    return float4(pow(saturate(color),1/2.2),1);
}
technique LargeTerrain{pass P0{VertexShader=compile VS_SHADERMODEL VS();PixelShader=compile PS_SHADERMODEL PS();}}
