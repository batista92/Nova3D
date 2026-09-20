using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Production.Assets.Gltf;
using Nova3D.Rendering.Lighting;
using Nova3D.Rendering.Materials;
using NovaDirectionalLight = Nova3D.Rendering.Lighting.DirectionalLight;

namespace Nova3D.Rendering.Models;

/// <summary>Draws an imported glTF scene without taking ownership of its GPU resources.</summary>
public sealed class GltfModelRenderer : IDisposable
{
    private readonly GltfModel _model;
    private readonly PbrMaterial[] _materials;
    private readonly PbrMaterial _defaultMaterial;
    private readonly RasterizerState _singleSided;
    private bool _disposed;

    public GltfModelRenderer(GltfModel model, Effect pbrEffect, NovaDirectionalLight light,
        ImageBasedLighting environment)
        : this(model, () => pbrEffect, light, environment)
    {
        ArgumentNullException.ThrowIfNull(pbrEffect);
    }

    public GltfModelRenderer(GltfModel model, Func<Effect> effectProvider, NovaDirectionalLight light,
        ImageBasedLighting environment)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        ArgumentNullException.ThrowIfNull(effectProvider);
        ArgumentNullException.ThrowIfNull(light);
        ArgumentNullException.ThrowIfNull(environment);

        _defaultMaterial = new PbrMaterial("glTF default", effectProvider, light, environment);
        _materials = model.Materials.Select((material, index) =>
        {
            var result = new PbrMaterial(
                string.IsNullOrWhiteSpace(material.Name) ? $"glTF material {index}" : material.Name,
                effectProvider, light, environment)
            {
                Albedo = new Vector3(material.BaseColorFactor.X, material.BaseColorFactor.Y,
                    material.BaseColorFactor.Z),
                Metallic = material.MetallicFactor,
                Roughness = material.RoughnessFactor,
                BaseColorTexture = GetTexture(material.BaseColorTexture),
                NormalTexture = GetTexture(material.NormalTexture),
                NormalScale = material.NormalScale,
                MetallicRoughnessTexture = GetTexture(material.MetallicRoughnessTexture),
                OcclusionTexture = GetTexture(material.OcclusionTexture),
                OcclusionStrength = material.OcclusionStrength
            };
            return result;
        }).ToArray();
        _singleSided = new RasterizerState { CullMode = CullMode.CullClockwiseFace };
    }

    public Matrix Transform { get; set; } = Matrix.Identity;
    public int DrawCallsLastFrame { get; private set; }
    public int TrianglesLastFrame { get; private set; }

    public void Draw(RenderContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(context);
        DrawCallsLastFrame = 0;
        TrianglesLastFrame = 0;
        var device = context.GraphicsDevice;
        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;

        foreach (var instance in _model.Instances)
        {
            var primitive = _model.Primitives[instance.PrimitiveIndex];
            var sourceMaterial = GetSourceMaterial(primitive.MaterialIndex);
            device.RasterizerState = sourceMaterial?.DoubleSided == true
                ? RasterizerState.CullNone : _singleSided;
            var material = GetMaterial(primitive.MaterialIndex);
            material.Apply(context);
            material.ApplySurface(instance.World * Transform);
            foreach (var pass in material.Effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                primitive.Mesh.Draw(device);
                context.Statistics.RecordDraw(primitive.Mesh.PrimitiveCount);
                DrawCallsLastFrame++;
                TrianglesLastFrame += primitive.Mesh.PrimitiveCount;
            }
        }
    }

    public void DrawShadows(RenderContext context, Effect shadowEffect, Matrix lightViewProjection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(shadowEffect);
        shadowEffect.Parameters["LightViewProjection"]?.SetValue(lightViewProjection);
        var device = context.GraphicsDevice;
        device.DepthStencilState = DepthStencilState.Default;

        foreach (var instance in _model.Instances)
        {
            var primitive = _model.Primitives[instance.PrimitiveIndex];
            var sourceMaterial = GetSourceMaterial(primitive.MaterialIndex);
            device.RasterizerState = sourceMaterial?.DoubleSided == true
                ? RasterizerState.CullNone : _singleSided;
            shadowEffect.Parameters["World"]?.SetValue(instance.World * Transform);
            foreach (var pass in shadowEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                primitive.Mesh.Draw(device);
                context.Statistics.RecordDraw(primitive.Mesh.PrimitiveCount, shadow: true);
            }
        }
    }

    private GltfMaterial? GetSourceMaterial(int? index) =>
        index is >= 0 && index < _model.Materials.Count ? _model.Materials[index.Value] : null;

    private PbrMaterial GetMaterial(int? index) =>
        index is >= 0 && index < _materials.Length ? _materials[index.Value] : _defaultMaterial;

    private Texture2D? GetTexture(int? index) =>
        index is >= 0 && index < _model.Textures.Count ? _model.Textures[index.Value] : null;

    public void Dispose()
    {
        if (_disposed) return;
        _singleSided.Dispose();
        _disposed = true;
    }
}
