using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Tests.Terrain;

internal sealed class TerrainMaterialTextures : IDisposable
{
    private const int TextureSize = 256;

    public TerrainMaterialTextures(GraphicsDevice device)
    {
        for (var layer = 0; layer < 4; layer++)
        {
            AlbedoHeightMaps[layer] = new Texture2D(device, TextureSize, TextureSize, true, SurfaceFormat.Color);
            NormalAoRoughnessMaps[layer] = new Texture2D(device, TextureSize, TextureSize, true, SurfaceFormat.Color);
            BuildLayer(layer);
        }
    }

    public Texture2D[] AlbedoHeightMaps { get; } = new Texture2D[4];
    public Texture2D[] NormalAoRoughnessMaps { get; } = new Texture2D[4];

    private void BuildLayer(int layer)
    {
        for (var level = 0; level < AlbedoHeightMaps[layer].LevelCount; level++)
        {
            var size = Math.Max(1, TextureSize >> level);
            var albedoHeight = new Color[size * size];
            var normalAoRoughness = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = x / (float)size;
                var v = y / (float)size;
                var h = DetailHeight(layer, u, v);
                var delta = 1f / size;
                var hx = DetailHeight(layer, u + delta, v) - DetailHeight(layer, u - delta, v);
                var hy = DetailHeight(layer, u, v + delta) - DetailHeight(layer, u, v - delta);
                var normal = Vector3.Normalize(new Vector3(-hx * 2.5f, -hy * 2.5f, 1f));
                var index = y * size + x;
                var roughness = layer switch { 0 => 0.78f, 1 => 0.88f, 2 => 0.66f, _ => 0.92f };
                var ao = MathHelper.Lerp(0.72f, 1f, h);
                var albedo = SampleAlbedo(layer, h);
                albedoHeight[index] = new Color(albedo.X, albedo.Y, albedo.Z, h);
                normalAoRoughness[index] = new Color(
                    normal.X * 0.5f + 0.5f,
                    normal.Y * 0.5f + 0.5f,
                    ao,
                    roughness);
            }
            AlbedoHeightMaps[layer].SetData(level, null, albedoHeight, 0, albedoHeight.Length);
            NormalAoRoughnessMaps[layer].SetData(level, null, normalAoRoughness, 0, normalAoRoughness.Length);
        }
    }

    private static Vector3 SampleAlbedo(int layer, float height)
    {
        var variation = (height - 0.5f) * 0.22f;
        return layer switch
        {
            0 => Vector3.Clamp(new Vector3(0.08f, 0.25f, 0.035f) + new Vector3(variation * 0.35f, variation, variation * 0.2f), Vector3.Zero, Vector3.One),
            1 => Vector3.Clamp(new Vector3(0.27f, 0.105f, 0.035f) + new Vector3(variation, variation * 0.55f, variation * 0.25f), Vector3.Zero, Vector3.One),
            2 => Vector3.Clamp(new Vector3(0.29f, 0.30f, 0.31f) + Vector3.One * variation, Vector3.Zero, Vector3.One),
            _ => Vector3.Clamp(new Vector3(0.64f, 0.48f, 0.25f) + new Vector3(variation, variation * 0.85f, variation * 0.55f), Vector3.Zero, Vector3.One)
        };
    }

    private static float DetailHeight(int layer, float u, float v)
    {
        var broad = ValueNoise(u, v, 4, layer * 17 + 3);
        var medium = ValueNoise(u, v, 9, layer * 31 + 7);
        var fine = ValueNoise(u, v, 23, layer * 47 + 11);
        var fractal = broad * 0.52f + medium * 0.31f + fine * 0.17f;
        return MathHelper.Clamp(layer switch
        {
            0 => fractal,
            1 => broad * 0.7f + medium * 0.3f,
            2 => MathF.Abs(fractal * 2f - 1f),
            _ => fractal * 0.88f + (MathF.Sin((u * 5f + v * 1.4f) * MathF.Tau) * 0.5f + 0.5f) * 0.12f
        }, 0f, 1f);
    }

    private static float ValueNoise(float u, float v, int frequency, int seed)
    {
        u -= MathF.Floor(u); v -= MathF.Floor(v);
        var x = u * frequency; var y = v * frequency;
        var x0 = (int)MathF.Floor(x); var y0 = (int)MathF.Floor(y);
        var tx = x - x0; var ty = y - y0;
        tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
        var a = Hash(Wrap(x0, frequency), Wrap(y0, frequency), seed);
        var b = Hash(Wrap(x0 + 1, frequency), Wrap(y0, frequency), seed);
        var c = Hash(Wrap(x0, frequency), Wrap(y0 + 1, frequency), seed);
        var d = Hash(Wrap(x0 + 1, frequency), Wrap(y0 + 1, frequency), seed);
        return MathHelper.Lerp(MathHelper.Lerp(a, b, tx), MathHelper.Lerp(c, d, tx), ty);
    }

    private static int Wrap(int value, int period) => (value % period + period) % period;
    private static float Hash(int x, int y, int seed)
    {
        var value = unchecked((uint)(x * 374761393 + y * 668265263 + seed * 1442695041));
        value = (value ^ (value >> 13)) * 1274126177u;
        return (value ^ (value >> 16)) / (float)uint.MaxValue;
    }

    public void Dispose()
    {
        foreach (var texture in AlbedoHeightMaps.Concat(NormalAoRoughnessMaps)) texture.Dispose();
    }
}
