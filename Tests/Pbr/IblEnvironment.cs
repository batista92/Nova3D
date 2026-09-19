using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Tests.Pbr;

/// <summary>
/// Builds the complete split-sum IBL data set from a small analytic HDR sky.
/// Keeping generation here makes the proof of concept self-contained; an HDR
/// equirectangular asset can replace SampleEnvironment later without touching PBR.fx.
/// </summary>
internal sealed class IblEnvironment : IDisposable
{
    private static readonly CubeMapFace[] Faces = Enum.GetValues<CubeMapFace>();
    private static readonly Vector3 KeyLightDirection = Vector3.Normalize(new Vector3(0.5f, 1f, 0.35f));

    public IblEnvironment(GraphicsDevice device)
    {
        EnvironmentMap = BuildEnvironment(device, 128);
        IrradianceMap = BuildIrradiance(device, 24);
        PrefilteredMap = BuildPrefiltered(device, 128);
        BrdfLut = BuildBrdfLut(device, 128);
    }

    public TextureCube EnvironmentMap { get; }
    public TextureCube IrradianceMap { get; }
    public TextureCube PrefilteredMap { get; }
    public Texture2D BrdfLut { get; }
    public int PrefilterMipCount => 8;

    private static TextureCube BuildEnvironment(GraphicsDevice device, int size)
    {
        var texture = new TextureCube(device, size, false, SurfaceFormat.Vector4);
        foreach (var face in Faces)
        {
            var pixels = BuildFace(size, face, SampleEnvironment);
            texture.SetData(face, pixels);
        }
        return texture;
    }

    private static TextureCube BuildIrradiance(GraphicsDevice device, int size)
    {
        var texture = new TextureCube(device, size, false, SurfaceFormat.Vector4);
        foreach (var face in Faces)
        {
            var pixels = BuildFace(size, face, normal =>
            {
                BuildBasis(normal, out var tangent, out var bitangent);
                var sum = Vector3.Zero;
                const int sampleCount = 64;
                for (uint i = 0; i < sampleCount; i++)
                {
                    var xi = Hammersley(i, sampleCount);
                    var phi = MathF.Tau * xi.X;
                    var cosTheta = MathF.Sqrt(1f - xi.Y);
                    var sinTheta = MathF.Sqrt(xi.Y);
                    var sample = tangent * (MathF.Cos(phi) * sinTheta) +
                                 bitangent * (MathF.Sin(phi) * sinTheta) + normal * cosTheta;
                    sum += SampleEnvironment(Vector3.Normalize(sample));
                }
                // As amostras ja seguem distribuicao cosine-weighted. A media
                // representa irradiancia normalizada por PI, pronta para Albedo * I.
                return sum / sampleCount;
            });
            texture.SetData(face, pixels);
        }
        return texture;
    }

    private static TextureCube BuildPrefiltered(GraphicsDevice device, int baseSize)
    {
        var texture = new TextureCube(device, baseSize, true, SurfaceFormat.Vector4);
        var mipCount = 1 + (int)MathF.Log2(baseSize);
        for (var level = 0; level < mipCount; level++)
        {
            var size = Math.Max(1, baseSize >> level);
            var roughness = level / (float)(mipCount - 1);
            foreach (var face in Faces)
            {
                var pixels = BuildFace(size, face, direction => PrefilterGgx(direction, roughness));
                texture.SetData(face, level, null, pixels, 0, pixels.Length);
            }
        }
        return texture;
    }

    private static Vector3 PrefilterGgx(Vector3 reflection, float roughness)
    {
        if (roughness < 0.01f)
            return SampleEnvironment(reflection);

        BuildBasis(reflection, out var tangent, out var bitangent);
        var result = Vector3.Zero;
        var totalWeight = 0f;
        const uint sampleCount = 96;
        for (uint i = 0; i < sampleCount; i++)
        {
            var localHalfway = ImportanceSampleGgx(Hammersley(i, sampleCount), roughness);
            var halfway = Vector3.Normalize(
                tangent * localHalfway.X + bitangent * localHalfway.Y + reflection * localHalfway.Z);
            var light = Vector3.Normalize(2f * Vector3.Dot(reflection, halfway) * halfway - reflection);
            var nDotL = MathF.Max(Vector3.Dot(reflection, light), 0f);
            if (nDotL <= 0f)
                continue;

            result += SampleEnvironment(light) * nDotL;
            totalWeight += nDotL;
        }
        return result / MathF.Max(totalWeight, 0.0001f);
    }

    private static Texture2D BuildBrdfLut(GraphicsDevice device, int size)
    {
        var texture = new Texture2D(device, size, size, false, SurfaceFormat.Vector2);
        var pixels = new Vector2[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var nDotV = (x + 0.5f) / size;
            var roughness = (y + 0.5f) / size;
            pixels[y * size + x] = IntegrateBrdf(nDotV, roughness);
        }
        texture.SetData(pixels);
        return texture;
    }

    private static Vector2 IntegrateBrdf(float nDotV, float roughness)
    {
        var view = new Vector3(MathF.Sqrt(MathF.Max(0f, 1f - nDotV * nDotV)), 0f, nDotV);
        var a = 0f;
        var b = 0f;
        const uint sampleCount = 128;
        for (uint i = 0; i < sampleCount; i++)
        {
            var halfway = ImportanceSampleGgx(Hammersley(i, sampleCount), roughness);
            var light = Vector3.Normalize(2f * Vector3.Dot(view, halfway) * halfway - view);
            var nDotL = MathF.Max(light.Z, 0f);
            var nDotH = MathF.Max(halfway.Z, 0f);
            var vDotH = MathF.Max(Vector3.Dot(view, halfway), 0f);
            if (nDotL <= 0f)
                continue;

            var geometry = GeometrySmithIbl(nDotV, nDotL, roughness);
            var visibility = geometry * vDotH / MathF.Max(nDotH * nDotV, 0.0001f);
            var fresnel = MathF.Pow(1f - vDotH, 5f);
            a += (1f - fresnel) * visibility;
            b += fresnel * visibility;
        }
        return new Vector2(a / sampleCount, b / sampleCount);
    }

    private static Vector3 ImportanceSampleGgx(Vector2 xi, float roughness)
    {
        var a = roughness * roughness;
        var phi = MathF.Tau * xi.X;
        var cosTheta = MathF.Sqrt((1f - xi.Y) / (1f + (a * a - 1f) * xi.Y));
        var sinTheta = MathF.Sqrt(MathF.Max(0f, 1f - cosTheta * cosTheta));
        return new Vector3(MathF.Cos(phi) * sinTheta, MathF.Sin(phi) * sinTheta, cosTheta);
    }

    private static float GeometrySmithIbl(float nDotV, float nDotL, float roughness)
    {
        var k = roughness * roughness / 2f;
        var gv = nDotV / (nDotV * (1f - k) + k);
        var gl = nDotL / (nDotL * (1f - k) + k);
        return gv * gl;
    }

    private static Vector4[] BuildFace(int size, CubeMapFace face, Func<Vector3, Vector3> sample)
    {
        var pixels = new Vector4[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var u = 2f * (x + 0.5f) / size - 1f;
            var v = 2f * (y + 0.5f) / size - 1f;
            var color = sample(CubeDirection(face, u, v));
            pixels[y * size + x] = new Vector4(color, 1f);
        }
        return pixels;
    }

    private static Vector3 SampleEnvironment(Vector3 direction)
    {
        var height = MathHelper.Clamp(direction.Y * 0.5f + 0.5f, 0f, 1f);
        var room = Vector3.Lerp(new Vector3(0.025f, 0.028f, 0.035f), new Vector3(0.32f, 0.34f, 0.38f), height);
        var key = MathF.Pow(MathF.Max(Vector3.Dot(direction, KeyLightDirection), 0f), 48f) *
                  new Vector3(9.0f, 8.2f, 7.1f);
        var fillDirection = Vector3.Normalize(new Vector3(-0.8f, 0.35f, -0.25f));
        var fill = MathF.Pow(MathF.Max(Vector3.Dot(direction, fillDirection), 0f), 18f) *
                   new Vector3(1.0f, 1.15f, 1.4f);
        var rimDirection = Vector3.Normalize(new Vector3(0.1f, 0.15f, -1f));
        var rim = MathF.Pow(MathF.Max(Vector3.Dot(direction, rimDirection), 0f), 80f) *
                  new Vector3(2.4f, 1.5f, 0.9f);
        return room + key + fill + rim;
    }

    private static Vector3 CubeDirection(CubeMapFace face, float u, float v) => Vector3.Normalize(face switch
    {
        CubeMapFace.PositiveX => new Vector3(1, -v, -u),
        CubeMapFace.NegativeX => new Vector3(-1, -v, u),
        CubeMapFace.PositiveY => new Vector3(u, 1, v),
        CubeMapFace.NegativeY => new Vector3(u, -1, -v),
        CubeMapFace.PositiveZ => new Vector3(u, -v, 1),
        _ => new Vector3(-u, -v, -1)
    });

    private static Vector2 Hammersley(uint i, uint count) => new(i / (float)count, RadicalInverse(i));

    private static float RadicalInverse(uint bits)
    {
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
        return bits * 2.3283064365386963e-10f;
    }

    private static void BuildBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
    {
        var up = MathF.Abs(normal.Y) < 0.999f ? Vector3.Up : Vector3.Right;
        tangent = Vector3.Normalize(Vector3.Cross(up, normal));
        bitangent = Vector3.Cross(normal, tangent);
    }

    public void Dispose()
    {
        EnvironmentMap.Dispose();
        IrradianceMap.Dispose();
        PrefilteredMap.Dispose();
        BrdfLut.Dispose();
    }
}
