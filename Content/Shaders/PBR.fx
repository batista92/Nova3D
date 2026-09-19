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
float3 Albedo;
float Metallic;
float Roughness;
float AmbientOcclusion;
float MaxReflectionLod;
float Exposure;
float DebugView;
float2 ShadowMapTexelSize;
float4 CascadeSplits;
float4 CascadeBlendStarts;
float ShowCascades;

texture ShadowMap0;
sampler2D ShadowSampler0 = sampler_state
{
    Texture = <ShadowMap0>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};
texture ShadowMap1;
sampler2D ShadowSampler1 = sampler_state { Texture = <ShadowMap1>; MinFilter = Point; MagFilter = Point; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
texture ShadowMap2;
sampler2D ShadowSampler2 = sampler_state { Texture = <ShadowMap2>; MinFilter = Point; MagFilter = Point; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
texture ShadowMap3;
sampler2D ShadowSampler3 = sampler_state { Texture = <ShadowMap3>; MinFilter = Point; MagFilter = Point; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };

texture IrradianceMap;
samplerCUBE IrradianceSampler = sampler_state
{
    Texture = <IrradianceMap>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

texture PrefilteredMap;
samplerCUBE PrefilteredSampler = sampler_state
{
    Texture = <PrefilteredMap>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

texture BrdfLut;
sampler2D BrdfSampler = sampler_state
{
    Texture = <BrdfLut>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

static const float PI = 3.14159265359;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
};

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
    output.LightPosition0 = mul(worldPosition, LightViewProjection0);
    output.LightPosition1 = mul(worldPosition, LightViewProjection1);
    output.LightPosition2 = mul(worldPosition, LightViewProjection2);
    output.LightPosition3 = mul(worldPosition, LightViewProjection3);
    output.ViewDepth = -mul(worldPosition, View).z;
    output.Position = mul(mul(worldPosition, View), Projection);
    return output;
}

float SampleShadowPcf(sampler2D shadowSampler, float4 lightPosition, float bias)
{
    float3 projected = lightPosition.xyz / lightPosition.w;
    float2 uv = projected.xy * 0.5 + 0.5;
    uv.y = 1.0 - uv.y;
    float currentDepth = projected.z;

    if (uv.x <= 0.0 || uv.x >= 1.0 || uv.y <= 0.0 || uv.y >= 1.0 ||
        currentDepth <= 0.0 || currentDepth >= 1.0)
        return 1.0;

    float visibility = 0.0;
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        float2 sampleUv = uv + float2(x, y) * ShadowMapTexelSize;
        float storedDepth = tex2Dlod(shadowSampler, float4(sampleUv, 0.0, 0.0)).r;
        visibility += currentDepth - bias <= storedDepth ? 1.0 : 0.0;
    }
    return visibility / 9.0;
}

float CalculateCascadedShadow(VertexShaderOutput input, float nDotL, out int cascadeIndex)
{
    cascadeIndex = input.ViewDepth < CascadeSplits.x ? 0 :
                   input.ViewDepth < CascadeSplits.y ? 1 :
                   input.ViewDepth < CascadeSplits.z ? 2 : 3;
    if (nDotL <= 0.0)
        return 1.0;

    float bias = max(0.00045 * (1.0 - nDotL), 0.00008);
    if (cascadeIndex == 0)
    {
        float visibility = SampleShadowPcf(ShadowSampler0, input.LightPosition0, bias);
        if (input.ViewDepth > CascadeBlendStarts.x)
        {
            float nextVisibility = SampleShadowPcf(ShadowSampler1, input.LightPosition1, bias);
            float blend = saturate((input.ViewDepth - CascadeBlendStarts.x) /
                                   (CascadeSplits.x - CascadeBlendStarts.x));
            visibility = lerp(visibility, nextVisibility, blend);
        }
        return visibility;
    }
    if (cascadeIndex == 1)
    {
        float visibility = SampleShadowPcf(ShadowSampler1, input.LightPosition1, bias);
        if (input.ViewDepth > CascadeBlendStarts.y)
        {
            float nextVisibility = SampleShadowPcf(ShadowSampler2, input.LightPosition2, bias);
            float blend = saturate((input.ViewDepth - CascadeBlendStarts.y) /
                                   (CascadeSplits.y - CascadeBlendStarts.y));
            visibility = lerp(visibility, nextVisibility, blend);
        }
        return visibility;
    }
    if (cascadeIndex == 2)
    {
        float visibility = SampleShadowPcf(ShadowSampler2, input.LightPosition2, bias);
        if (input.ViewDepth > CascadeBlendStarts.z)
        {
            float nextVisibility = SampleShadowPcf(ShadowSampler3, input.LightPosition3, bias);
            float blend = saturate((input.ViewDepth - CascadeBlendStarts.z) /
                                   (CascadeSplits.z - CascadeBlendStarts.z));
            visibility = lerp(visibility, nextVisibility, blend);
        }
        return visibility;
    }
    return SampleShadowPcf(ShadowSampler3, input.LightPosition3, bias);
}

float DistributionGGX(float3 normal, float3 halfway, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float nDotH = max(dot(normal, halfway), 0.0);
    float denominator = nDotH * nDotH * (a2 - 1.0) + 1.0;
    return a2 / max(PI * denominator * denominator, 0.0001);
}

float GeometrySchlickGGX(float nDotDirection, float roughness)
{
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;
    return nDotDirection / max(nDotDirection * (1.0 - k) + k, 0.0001);
}

float GeometrySmith(float3 normal, float3 viewDirection, float3 lightDirection, float roughness)
{
    return GeometrySchlickGGX(max(dot(normal, viewDirection), 0.0), roughness) *
           GeometrySchlickGGX(max(dot(normal, lightDirection), 0.0), roughness);
}

float3 FresnelSchlick(float cosine, float3 f0)
{
    return f0 + (1.0 - f0) * pow(saturate(1.0 - cosine), 5.0);
}

float3 FresnelSchlickRoughness(float cosine, float3 f0, float roughness)
{
    return f0 + (max(float3(1.0 - roughness, 1.0 - roughness, 1.0 - roughness), f0) - f0) *
           pow(saturate(1.0 - cosine), 5.0);
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float3 normal = normalize(input.Normal);
    float3 viewDirection = normalize(CameraPosition - input.WorldPosition);
    float3 lightDirection = normalize(-LightDirection);
    float3 halfway = normalize(viewDirection + lightDirection);
    float3 f0 = lerp(float3(0.04, 0.04, 0.04), Albedo, Metallic);
    float3 fresnel = FresnelSchlick(max(dot(halfway, viewDirection), 0.0), f0);
    float distribution = DistributionGGX(normal, halfway, max(Roughness, 0.045));
    float geometry = GeometrySmith(normal, viewDirection, lightDirection, Roughness);
    float3 numerator = distribution * geometry * fresnel;
    float denominator = 4.0 * max(dot(normal, viewDirection), 0.0) *
                        max(dot(normal, lightDirection), 0.0) + 0.0001;
    float3 specular = numerator / denominator;
    float3 diffuseWeight = (1.0 - fresnel) * (1.0 - Metallic);
    float nDotL = max(dot(normal, lightDirection), 0.0);
    // Faces opostas a luz ja possuem contribuicao direta zero. Consultar o
    // shadow map nelas apenas cria auto-sombra sem efeito visual e desperdiça PCF.
    int cascadeIndex;
    float shadowVisibility = CalculateCascadedShadow(input, nDotL, cascadeIndex);
    float3 direct = (diffuseWeight * Albedo / PI + specular) * LightColor * nDotL * shadowVisibility;
    float nDotV = max(dot(normal, viewDirection), 0.0);
    float3 ambientFresnel = FresnelSchlickRoughness(nDotV, f0, Roughness);
    float3 ambientDiffuseWeight = (1.0 - ambientFresnel) * (1.0 - Metallic);
    float3 irradiance = texCUBE(IrradianceSampler, normal).rgb;
    float3 diffuseIbl = irradiance * Albedo;
    float3 reflection = reflect(-viewDirection, normal);
    float3 prefiltered = texCUBElod(PrefilteredSampler, float4(reflection, Roughness * MaxReflectionLod)).rgb;
    float2 brdf = tex2D(BrdfSampler, float2(nDotV, Roughness)).rg;
    float3 diffuseContribution = ambientDiffuseWeight * diffuseIbl * AmbientOcclusion;
    float3 splitSum = ambientFresnel * brdf.x + brdf.y;
    float3 specularIbl = prefiltered * splitSum * AmbientOcclusion;
    float3 ambient = diffuseContribution + specularIbl;
    float3 color = (direct + ambient) * Exposure;

    // 1: albedo para dieletricos / F0 para metais; 2: luz direta;
    // 3: IBL difuso; 4: IBL especular. As teclas 1-5 selecionam a visao.
    if (DebugView > 0.5 && DebugView < 1.5)
        color = lerp(Albedo, f0, Metallic);
    else if (DebugView > 1.5 && DebugView < 2.5)
        color = direct * Exposure;
    else if (DebugView > 2.5 && DebugView < 3.5)
        color = diffuseContribution * Exposure;
    else if (DebugView > 3.5 && DebugView < 4.5)
        color = specularIbl * Exposure;
    else if (DebugView > 4.5 && DebugView < 5.5)
        color = ambientFresnel;
    else if (DebugView > 5.5 && DebugView < 6.5)
        color = prefiltered * Exposure;
    else if (DebugView > 6.5 && DebugView < 7.5)
        color = float3(brdf.x, brdf.y, 0.0);
    else if (DebugView > 7.5)
        color = float3(shadowVisibility, shadowVisibility, shadowVisibility);
    else if (ShowCascades > 0.5)
    {
        float3 cascadeColor = cascadeIndex == 0 ? float3(1.0, 0.2, 0.2) :
                              cascadeIndex == 1 ? float3(0.2, 1.0, 0.2) :
                              cascadeIndex == 2 ? float3(0.2, 0.45, 1.0) :
                                                  float3(1.0, 0.85, 0.2);
        color = lerp(color, cascadeColor, 0.35);
    }
    // Reinhard por luminancia preserva a cromaticidade dos condutores. A
    // versao por canal dessaturava reflexos HDR dourados ate ficarem cinza.
    float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
    color *= (luminance / (1.0 + luminance)) / max(luminance, 0.0001);
    color = pow(saturate(color), 1.0 / 2.2);
    return float4(color, 1.0);
}

technique PBR
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderFunction();
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}
