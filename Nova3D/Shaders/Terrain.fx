#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 World;
float4x4 WorldInverseTranspose;
float4x4 View;
float4x4 Projection;
float4x4 LightViewProjection0;
float4x4 LightViewProjection1;
float4x4 LightViewProjection2;
float4x4 LightViewProjection3;
float3 CameraPosition;
float3 LightDirection;
float3 LightColor;
float Exposure;
float TextureScale;
float TriplanarSharpness;
float MaxReflectionLod;
float DebugView;
float3 BrushPosition;
float BrushRadius;
float BrushActive;
float2 ShadowMapTexelSize;
float4 CascadeSplits;
float4 CascadeBlendStarts;
float ShowCascades;

texture GrassAlbedoHeight; sampler2D GrassAlbedoHeightSampler = sampler_state { Texture = <GrassAlbedoHeight>; MinFilter = Linear; MagFilter = Linear; MipFilter = Linear; AddressU = Wrap; AddressV = Wrap; };
texture DirtAlbedoHeight;  sampler2D DirtAlbedoHeightSampler  = sampler_state { Texture = <DirtAlbedoHeight>;  MinFilter = Linear; MagFilter = Linear; MipFilter = Linear; AddressU = Wrap; AddressV = Wrap; };
texture RockAlbedoHeight;  sampler2D RockAlbedoHeightSampler  = sampler_state { Texture = <RockAlbedoHeight>;  MinFilter = Linear; MagFilter = Linear; MipFilter = Linear; AddressU = Wrap; AddressV = Wrap; };
texture SandAlbedoHeight;  sampler2D SandAlbedoHeightSampler  = sampler_state { Texture = <SandAlbedoHeight>;  MinFilter = Linear; MagFilter = Linear; MipFilter = Linear; AddressU = Wrap; AddressV = Wrap; };
texture GrassNormalAoRoughness; sampler2D GrassNormalAoRoughnessSampler = sampler_state { Texture = <GrassNormalAoRoughness>; MinFilter = Linear; MagFilter = Linear; MipFilter = Linear; AddressU = Wrap; AddressV = Wrap; };
texture DirtNormalAoRoughness;  sampler2D DirtNormalAoRoughnessSampler  = sampler_state { Texture = <DirtNormalAoRoughness>;  MinFilter = Linear; MagFilter = Linear; MipFilter = Linear; AddressU = Wrap; AddressV = Wrap; };
texture RockNormalAoRoughness;  sampler2D RockNormalAoRoughnessSampler  = sampler_state { Texture = <RockNormalAoRoughness>;  MinFilter = Linear; MagFilter = Linear; MipFilter = Linear; AddressU = Wrap; AddressV = Wrap; };
texture SandNormalAoRoughness;  sampler2D SandNormalAoRoughnessSampler  = sampler_state { Texture = <SandNormalAoRoughness>;  MinFilter = Linear; MagFilter = Linear; MipFilter = Linear; AddressU = Wrap; AddressV = Wrap; };
texture IrradianceMap;
samplerCUBE IrradianceSampler = sampler_state { Texture = <IrradianceMap>; MinFilter = Linear; MagFilter = Linear; MipFilter = Linear; AddressU = Clamp; AddressV = Clamp; };
texture PrefilteredMap;
samplerCUBE PrefilteredSampler = sampler_state { Texture = <PrefilteredMap>; MinFilter = Linear; MagFilter = Linear; MipFilter = Linear; AddressU = Clamp; AddressV = Clamp; };
texture BrdfLut;
sampler2D BrdfSampler = sampler_state { Texture = <BrdfLut>; MinFilter = Linear; MagFilter = Linear; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
texture ShadowMap0; sampler2D ShadowSampler0 = sampler_state { Texture = <ShadowMap0>; MinFilter = Point; MagFilter = Point; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
texture ShadowMap1; sampler2D ShadowSampler1 = sampler_state { Texture = <ShadowMap1>; MinFilter = Point; MagFilter = Point; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
texture ShadowMap2; sampler2D ShadowSampler2 = sampler_state { Texture = <ShadowMap2>; MinFilter = Point; MagFilter = Point; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
texture ShadowMap3; sampler2D ShadowSampler3 = sampler_state { Texture = <ShadowMap3>; MinFilter = Point; MagFilter = Point; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };

static const float PI = 3.14159265359;

struct VertexShaderInput { float4 Position : POSITION0; float3 Normal : NORMAL0; };
struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float4 LightPosition0 : TEXCOORD2;
    float4 LightPosition1 : TEXCOORD3;
    float4 LightPosition2 : TEXCOORD4;
    float4 LightPosition3 : TEXCOORD5;
    float ViewDepth : TEXCOORD6;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    float4 worldPosition = mul(input.Position, World);
    output.WorldPosition = worldPosition.xyz;
    output.Normal = normalize(mul(float4(input.Normal, 0.0), WorldInverseTranspose).xyz);
    float lightFacing = saturate(dot(output.Normal, normalize(-LightDirection)));
    float normalOffsetAmount = lerp(0.15, 0.04, lightFacing);
    float4 shadowWorldPosition = float4(worldPosition.xyz + output.Normal * normalOffsetAmount, 1.0);
    output.LightPosition0 = mul(shadowWorldPosition, LightViewProjection0);
    output.LightPosition1 = mul(shadowWorldPosition, LightViewProjection1);
    output.LightPosition2 = mul(shadowWorldPosition, LightViewProjection2);
    output.LightPosition3 = mul(shadowWorldPosition, LightViewProjection3);
    output.ViewDepth = -mul(worldPosition, View).z;
    output.Position = mul(mul(worldPosition, View), Projection);
    return output;
}

float SampleShadowPcf(sampler2D shadowSampler, float4 lightPosition, float bias)
{
    float3 projected = lightPosition.xyz / lightPosition.w;
    float2 uv = projected.xy * 0.5 + 0.5;
    uv.y = 1.0 - uv.y;
    if (uv.x <= 0.0 || uv.x >= 1.0 || uv.y <= 0.0 || uv.y >= 1.0 || projected.z <= 0.0 || projected.z >= 1.0)
        return 1.0;
    float visibility = 0.0;
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        float2 sampleUv = uv + float2(x, y) * ShadowMapTexelSize;
        float depth = tex2Dlod(shadowSampler, float4(sampleUv, 0.0, 0.0)).r;
        visibility += projected.z - bias <= depth ? 1.0 : 0.0;
    }
    return visibility / 9.0;
}

float CascadedShadow(VertexShaderOutput input, float nDotL, out int cascadeIndex)
{
    cascadeIndex = input.ViewDepth < CascadeSplits.x ? 0 : input.ViewDepth < CascadeSplits.y ? 1 : input.ViewDepth < CascadeSplits.z ? 2 : 3;
    if (nDotL <= 0.0) return 1.0;
    float cascadeBiasScale = cascadeIndex == 0 ? 1.0 : cascadeIndex == 1 ? 1.8 : cascadeIndex == 2 ? 3.2 : 5.0;
    float bias = max(0.0018 * (1.0 - nDotL), 0.0002) * cascadeBiasScale;
    if (cascadeIndex == 0)
    {
        float value = SampleShadowPcf(ShadowSampler0, input.LightPosition0, bias);
        if (input.ViewDepth > CascadeBlendStarts.x)
            value = lerp(value, SampleShadowPcf(ShadowSampler1, input.LightPosition1, bias),
                saturate((input.ViewDepth - CascadeBlendStarts.x) / (CascadeSplits.x - CascadeBlendStarts.x)));
        return value;
    }
    if (cascadeIndex == 1)
    {
        float value = SampleShadowPcf(ShadowSampler1, input.LightPosition1, bias);
        if (input.ViewDepth > CascadeBlendStarts.y)
            value = lerp(value, SampleShadowPcf(ShadowSampler2, input.LightPosition2, bias),
                saturate((input.ViewDepth - CascadeBlendStarts.y) / (CascadeSplits.y - CascadeBlendStarts.y)));
        return value;
    }
    if (cascadeIndex == 2)
    {
        float value = SampleShadowPcf(ShadowSampler2, input.LightPosition2, bias);
        if (input.ViewDepth > CascadeBlendStarts.z)
            value = lerp(value, SampleShadowPcf(ShadowSampler3, input.LightPosition3, bias),
                saturate((input.ViewDepth - CascadeBlendStarts.z) / (CascadeSplits.z - CascadeBlendStarts.z)));
        return value;
    }
    return SampleShadowPcf(ShadowSampler3, input.LightPosition3, bias);
}

float4 SampleTriplanar(sampler2D source, float3 position, float3 weights)
{
    float4 xProjection = tex2D(source, position.zy * TextureScale);
    float4 yProjection = tex2D(source, position.xz * TextureScale);
    float4 zProjection = tex2D(source, position.xy * TextureScale);
    return xProjection * weights.x + yProjection * weights.y + zProjection * weights.z;
}

float3 SampleTriplanarNormal(sampler2D source, float3 position, float3 geometryNormal, float3 weights)
{
    float2 encodedX = tex2D(source, position.zy * TextureScale).xy * 2.0 - 1.0;
    float2 encodedY = tex2D(source, position.xz * TextureScale).xy * 2.0 - 1.0;
    float2 encodedZ = tex2D(source, position.xy * TextureScale).xy * 2.0 - 1.0;
    float3 sampleX = float3(encodedX, sqrt(saturate(1.0 - dot(encodedX, encodedX))));
    float3 sampleY = float3(encodedY, sqrt(saturate(1.0 - dot(encodedY, encodedY))));
    float3 sampleZ = float3(encodedZ, sqrt(saturate(1.0 - dot(encodedZ, encodedZ))));
    float3 normalX = float3(sampleX.z * sign(geometryNormal.x), sampleX.y, sampleX.x);
    float3 normalY = float3(sampleY.x, sampleY.z * sign(geometryNormal.y), sampleY.y);
    float3 normalZ = float3(sampleZ.x, sampleZ.y, sampleZ.z * sign(geometryNormal.z));
    return normalize(normalX * weights.x + normalY * weights.y + normalZ * weights.z);
}

float DistributionGGX(float3 normal, float3 halfway, float roughness)
{
    float a2 = roughness * roughness * roughness * roughness;
    float nDotH = max(dot(normal, halfway), 0.0);
    float denominator = nDotH * nDotH * (a2 - 1.0) + 1.0;
    return a2 / max(PI * denominator * denominator, 0.0001);
}

float GeometrySchlickGGX(float nDotDirection, float roughness)
{
    float k = (roughness + 1.0) * (roughness + 1.0) / 8.0;
    return nDotDirection / max(nDotDirection * (1.0 - k) + k, 0.0001);
}

float3 FresnelSchlick(float cosine, float3 f0)
{
    return f0 + (1.0 - f0) * pow(saturate(1.0 - cosine), 5.0);
}

float3 FresnelSchlickRoughness(float cosine, float3 f0, float roughness)
{
    return f0 + (max(1.0 - roughness, f0) - f0) * pow(saturate(1.0 - cosine), 5.0);
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float3 geometryNormal = normalize(input.Normal);
    float3 triplanarWeights = pow(abs(geometryNormal), TriplanarSharpness);
    triplanarWeights /= max(triplanarWeights.x + triplanarWeights.y + triplanarWeights.z, 0.0001);

    float slope = 1.0 - saturate(geometryNormal.y);
    float rockWeight = smoothstep(0.10, 0.34, slope);
    float sandWeight = (1.0 - rockWeight) * (1.0 - smoothstep(-0.45, 0.45, input.WorldPosition.y));
    float grassWeight = (1.0 - rockWeight) * smoothstep(0.0, 0.85, input.WorldPosition.y);
    float dirtWeight = max(0.0, 1.0 - rockWeight - sandWeight - grassWeight);
    float4 baseWeights = float4(grassWeight, dirtWeight, rockWeight, sandWeight);
    baseWeights /= max(dot(baseWeights, 1.0), 0.0001);

    float4 grassAlbedoHeight = SampleTriplanar(GrassAlbedoHeightSampler, input.WorldPosition, triplanarWeights);
    float4 dirtAlbedoHeight  = SampleTriplanar(DirtAlbedoHeightSampler, input.WorldPosition, triplanarWeights);
    float4 rockAlbedoHeight  = SampleTriplanar(RockAlbedoHeightSampler, input.WorldPosition, triplanarWeights);
    float4 sandAlbedoHeight  = SampleTriplanar(SandAlbedoHeightSampler, input.WorldPosition, triplanarWeights);
    float4 grassSurface = SampleTriplanar(GrassNormalAoRoughnessSampler, input.WorldPosition, triplanarWeights);
    float4 dirtSurface  = SampleTriplanar(DirtNormalAoRoughnessSampler, input.WorldPosition, triplanarWeights);
    float4 rockSurface  = SampleTriplanar(RockNormalAoRoughnessSampler, input.WorldPosition, triplanarWeights);
    float4 sandSurface  = SampleTriplanar(SandNormalAoRoughnessSampler, input.WorldPosition, triplanarWeights);
    float4 materialHeights = float4(grassAlbedoHeight.w, dirtAlbedoHeight.w, rockAlbedoHeight.w, sandAlbedoHeight.w);
    float4 heightScores = baseWeights * (materialHeights + 0.35);
    float highest = max(max(heightScores.x, heightScores.y), max(heightScores.z, heightScores.w));
    float4 layerWeights = max(heightScores - (highest - 0.16), 0.0);
    layerWeights /= max(dot(layerWeights, 1.0), 0.0001);

    float3 grassAlbedo = grassAlbedoHeight.rgb;
    float3 dirtAlbedo = dirtAlbedoHeight.rgb;
    float3 rockAlbedo = rockAlbedoHeight.rgb;
    float3 sandAlbedo = sandAlbedoHeight.rgb;
    float3 albedo = grassAlbedo * layerWeights.x + dirtAlbedo * layerWeights.y +
                    rockAlbedo * layerWeights.z + sandAlbedo * layerWeights.w;

    float3 grassNormal = SampleTriplanarNormal(GrassNormalAoRoughnessSampler, input.WorldPosition, geometryNormal, triplanarWeights);
    float3 dirtNormal  = SampleTriplanarNormal(DirtNormalAoRoughnessSampler, input.WorldPosition, geometryNormal, triplanarWeights);
    float3 rockNormal  = SampleTriplanarNormal(RockNormalAoRoughnessSampler, input.WorldPosition, geometryNormal, triplanarWeights);
    float3 sandNormal  = SampleTriplanarNormal(SandNormalAoRoughnessSampler, input.WorldPosition, geometryNormal, triplanarWeights);
    float3 normal = normalize(grassNormal * layerWeights.x + dirtNormal * layerWeights.y +
                              rockNormal * layerWeights.z + sandNormal * layerWeights.w);
    float roughness = dot(float4(grassSurface.w, dirtSurface.w, rockSurface.w, sandSurface.w), layerWeights);
    float ao = dot(float4(grassSurface.z, dirtSurface.z, rockSurface.z, sandSurface.z), layerWeights);

    float3 viewDirection = normalize(CameraPosition - input.WorldPosition);
    float3 lightDirection = normalize(-LightDirection);
    float3 halfway = normalize(viewDirection + lightDirection);
    float3 f0 = float3(0.04, 0.04, 0.04);
    float3 fresnel = FresnelSchlick(max(dot(halfway, viewDirection), 0.0), f0);
    float distribution = DistributionGGX(normal, halfway, roughness);
    float geometry = GeometrySchlickGGX(max(dot(normal, viewDirection), 0.0), roughness) *
                     GeometrySchlickGGX(max(dot(normal, lightDirection), 0.0), roughness);
    float3 specular = distribution * geometry * fresnel /
        max(4.0 * max(dot(normal, viewDirection), 0.0) * max(dot(normal, lightDirection), 0.0), 0.0001);
    float3 direct = ((1.0 - fresnel) * albedo / PI + specular) * LightColor *
                    max(dot(normal, lightDirection), 0.0);
    int cascadeIndex;
    float shadowVisibility = CascadedShadow(input, max(dot(geometryNormal, lightDirection), 0.0), cascadeIndex);
    direct *= shadowVisibility;

    float nDotV = max(dot(normal, viewDirection), 0.0);
    float3 ambientFresnel = FresnelSchlickRoughness(nDotV, f0, roughness);
    float3 diffuseIbl = texCUBE(IrradianceSampler, normal).rgb * albedo;
    float3 reflection = reflect(-viewDirection, normal);
    float3 prefiltered = texCUBElod(PrefilteredSampler, float4(reflection, roughness * MaxReflectionLod)).rgb;
    float2 brdf = tex2D(BrdfSampler, float2(nDotV, roughness)).rg;
    float3 ambient = ((1.0 - ambientFresnel) * diffuseIbl +
                      prefiltered * (ambientFresnel * brdf.x + brdf.y)) * ao;
    float3 color = (direct + ambient) * Exposure;

    if (DebugView > 0.5 && DebugView < 1.5)
        color = layerWeights.x * float3(0.1, 0.8, 0.1) + layerWeights.y * float3(0.45, 0.18, 0.05) +
                layerWeights.z * float3(0.55, 0.55, 0.58) + layerWeights.w * float3(0.9, 0.72, 0.3);
    else if (DebugView > 1.5 && DebugView < 2.5)
        color = normal * 0.5 + 0.5;
    else if (DebugView > 2.5)
        color = shadowVisibility;
    else if (ShowCascades > 0.5)
    {
        float3 cascadeColor = cascadeIndex == 0 ? float3(1.0, 0.2, 0.2) :
                              cascadeIndex == 1 ? float3(0.2, 1.0, 0.2) :
                              cascadeIndex == 2 ? float3(0.2, 0.45, 1.0) : float3(1.0, 0.85, 0.2);
        color = lerp(color, cascadeColor, 0.35);
    }

    if (BrushActive > 0.5)
    {
        float brushDistance = distance(input.WorldPosition.xz, BrushPosition.xz);
        float ring = 1.0 - smoothstep(0.025, 0.075, abs(brushDistance - BrushRadius));
        float centerMark = 1.0 - smoothstep(0.0, 0.07, brushDistance);
        color = lerp(color, float3(1.0, 0.55, 0.08), saturate(ring + centerMark));
    }

    float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
    color *= (luminance / (1.0 + luminance)) / max(luminance, 0.0001);
    color = pow(saturate(color), 1.0 / 2.2);
    return float4(color, 1.0);
}

technique Terrain
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderFunction();
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}
