using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.World.Terrain;

public sealed record TerrainLayer(string ShaderName, Texture2D AlbedoHeight,
    Texture2D NormalAoRoughness);

/// <summary>Named terrain textures; texture ownership remains with the asset source.</summary>
public sealed class TerrainLayerSet
{
    private readonly TerrainLayer[] _layers;

    public TerrainLayerSet(IEnumerable<TerrainLayer> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        _layers = layers.ToArray();
        if (_layers.Length == 0) throw new ArgumentException("At least one terrain layer is required.", nameof(layers));
        if (_layers.Any(layer => string.IsNullOrWhiteSpace(layer.ShaderName)))
            throw new ArgumentException("Every terrain layer requires a shader name.", nameof(layers));
        if (_layers.Select(layer => layer.ShaderName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _layers.Length)
            throw new ArgumentException("Terrain layer shader names must be unique.", nameof(layers));
    }

    public IReadOnlyList<TerrainLayer> Layers => _layers;

    public void Apply(Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        foreach (var layer in _layers)
        {
            var albedo = effect.Parameters[$"{layer.ShaderName}AlbedoHeight"] ??
                throw new InvalidOperationException($"Shader is missing '{layer.ShaderName}AlbedoHeight'.");
            var surface = effect.Parameters[$"{layer.ShaderName}NormalAoRoughness"] ??
                throw new InvalidOperationException($"Shader is missing '{layer.ShaderName}NormalAoRoughness'.");
            albedo.SetValue(layer.AlbedoHeight);
            surface.SetValue(layer.NormalAoRoughness);
        }
    }
}
