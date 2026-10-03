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
    private readonly PbrMaterial[] _skinnedMaterials;
    private readonly PbrMaterial _defaultMaterial;
    private readonly PbrMaterial _defaultSkinnedMaterial;
    private readonly GltfSkeletonPose? _pose;
    private readonly Matrix[] _jointPalette = new Matrix[48];
    private readonly RasterizerState _singleSided;
    private bool _disposed;

    public GltfModelRenderer(GltfModel model, Effect pbrEffect, NovaDirectionalLight light,
        ImageBasedLighting environment)
        : this(model, () => pbrEffect, light, environment, null)
    {
        ArgumentNullException.ThrowIfNull(pbrEffect);
    }

    public GltfModelRenderer(GltfModel model, Effect pbrEffect, NovaDirectionalLight light,
        ImageBasedLighting environment, GltfSkeletonPose pose)
        : this(model, () => pbrEffect, light, environment, pose)
    {
        ArgumentNullException.ThrowIfNull(pbrEffect);
    }

    public GltfModelRenderer(GltfModel model, Func<Effect> effectProvider, NovaDirectionalLight light,
        ImageBasedLighting environment)
        : this(model, effectProvider, light, environment, null)
    {
    }

    public GltfModelRenderer(GltfModel model, Func<Effect> effectProvider, NovaDirectionalLight light,
        ImageBasedLighting environment, GltfSkeletonPose? pose)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _pose = pose;
        if (pose is not null && pose.NodeCount != model.Nodes.Count)
            throw new ArgumentException("Pose and model node counts differ.", nameof(pose));
        ArgumentNullException.ThrowIfNull(effectProvider);
        ArgumentNullException.ThrowIfNull(light);
        ArgumentNullException.ThrowIfNull(environment);

        _defaultMaterial = new PbrMaterial("glTF default", effectProvider, light, environment, "PBR");
        _defaultSkinnedMaterial = new PbrMaterial(
            "glTF default skinned", effectProvider, light, environment, "PBRSkinned");
        _materials = CreateMaterials(effectProvider, light, environment, "PBR");
        _skinnedMaterials = CreateMaterials(effectProvider, light, environment, "PBRSkinned");
        Array.Fill(_jointPalette, Matrix.Identity);
        _singleSided = new RasterizerState { CullMode = CullMode.CullClockwiseFace };
        UpdateBounds();
    }

    public Matrix Transform { get; set; } = Matrix.Identity;
    public GltfSkeletonPose? Pose => _pose;
    public BoundingBox Bounds { get; private set; }
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
        UpdateBounds();

        for (var instanceIndex = 0; instanceIndex < _model.Instances.Count; instanceIndex++)
        {
            var instance = _model.Instances[instanceIndex];
            var primitive = _model.Primitives[instance.PrimitiveIndex];
            ValidateSkinning(instance, primitive);
            var sourceMaterial = GetSourceMaterial(primitive.MaterialIndex);
            device.RasterizerState = sourceMaterial?.DoubleSided == true
                ? RasterizerState.CullNone : _singleSided;
            var material = GetMaterial(primitive.MaterialIndex, primitive.IsSkinned);
            material.Apply(context);
            material.ApplySurface(GetInstanceWorld(instance) * Transform);
            if (primitive.IsSkinned) ApplyJointPalette(material.Effect, instance, primitive);
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
        var staticTechnique = shadowEffect.CurrentTechnique;

        for (var instanceIndex = 0; instanceIndex < _model.Instances.Count; instanceIndex++)
        {
            var instance = _model.Instances[instanceIndex];
            var primitive = _model.Primitives[instance.PrimitiveIndex];
            ValidateSkinning(instance, primitive);
            var sourceMaterial = GetSourceMaterial(primitive.MaterialIndex);
            device.RasterizerState = sourceMaterial?.DoubleSided == true
                ? RasterizerState.CullNone : _singleSided;
            shadowEffect.CurrentTechnique = primitive.IsSkinned
                ? shadowEffect.Techniques["SkinnedShadowDepth"] : staticTechnique;
            shadowEffect.Parameters["World"]?.SetValue(GetInstanceWorld(instance) * Transform);
            if (primitive.IsSkinned) ApplyJointPalette(shadowEffect, instance, primitive);
            foreach (var pass in shadowEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                primitive.Mesh.Draw(device);
                context.Statistics.RecordDraw(primitive.Mesh.PrimitiveCount, shadow: true);
            }
        }
        shadowEffect.CurrentTechnique = staticTechnique;
    }

    private GltfMaterial? GetSourceMaterial(int? index) =>
        index is >= 0 && index < _model.Materials.Count ? _model.Materials[index.Value] : null;

    private PbrMaterial GetMaterial(int? index, bool skinned)
    {
        var materials = skinned ? _skinnedMaterials : _materials;
        if (index is >= 0 && index < materials.Length) return materials[index.Value];
        return skinned ? _defaultSkinnedMaterial : _defaultMaterial;
    }

    private Texture2D? GetTexture(int? index) =>
        index is >= 0 && index < _model.Textures.Count ? _model.Textures[index.Value] : null;

    public void UpdateBounds()
    {
        if (_model.Instances.Count == 0)
        {
            Bounds = new BoundingBox(Vector3.Zero, Vector3.Zero);
            return;
        }
        var minimum = new Vector3(float.MaxValue);
        var maximum = new Vector3(float.MinValue);
        for (var instanceIndex = 0; instanceIndex < _model.Instances.Count; instanceIndex++)
        {
            var instance = _model.Instances[instanceIndex];
            var primitive = _model.Primitives[instance.PrimitiveIndex];
            ValidateSkinning(instance, primitive);
            var world = GetInstanceWorld(instance) * Transform;
            if (primitive.IsSkinned)
            {
                _pose!.WriteSkinPalette(instance.SkinIndex!.Value, instance.NodeIndex,
                    primitive.JointPalette, _jointPalette);
                for (var joint = 0; joint < primitive.JointBounds.Count; joint++)
                    IncludeTransformedBounds(primitive.JointBounds[joint], _jointPalette[joint] * world,
                        ref minimum, ref maximum);
            }
            else
            {
                IncludeTransformedBounds(primitive.Bounds, world, ref minimum, ref maximum);
            }
        }
        Bounds = new BoundingBox(minimum, maximum);
    }

    private Matrix GetInstanceWorld(GltfInstance instance) =>
        _pose is not null && instance.NodeIndex >= 0
            ? _pose.GetWorldTransform(instance.NodeIndex)
            : instance.World;

    private void ValidateSkinning(GltfInstance instance, GltfPrimitive primitive)
    {
        if (!primitive.IsSkinned) return;
        if (_pose is null)
            throw new InvalidOperationException(
                "A GltfSkeletonPose is required to draw a skinned primitive.");
        if (instance.NodeIndex < 0 || instance.SkinIndex is null)
            throw new InvalidOperationException("A skinned instance has no node or skin index.");
    }

    private void ApplyJointPalette(Effect effect, GltfInstance instance, GltfPrimitive primitive)
    {
        _pose!.WriteSkinPalette(instance.SkinIndex!.Value, instance.NodeIndex,
            primitive.JointPalette, _jointPalette);
        var parameter = effect.Parameters["JointPalette"] ?? throw new InvalidOperationException(
            "The skinned effect does not declare the required JointPalette parameter.");
        parameter.SetValue(_jointPalette);
    }

    private PbrMaterial[] CreateMaterials(Func<Effect> effectProvider, NovaDirectionalLight light,
        ImageBasedLighting environment, string technique) =>
        _model.Materials.Select((material, index) => new PbrMaterial(
            string.IsNullOrWhiteSpace(material.Name) ? $"glTF material {index}" : material.Name,
            effectProvider, light, environment, technique)
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
        }).ToArray();

    private static void IncludeTransformedBounds(BoundingBox bounds, Matrix transform,
        ref Vector3 minimum, ref Vector3 maximum)
    {
        IncludeTransformedPoint(new Vector3(bounds.Min.X, bounds.Min.Y, bounds.Min.Z), transform, ref minimum, ref maximum);
        IncludeTransformedPoint(new Vector3(bounds.Max.X, bounds.Min.Y, bounds.Min.Z), transform, ref minimum, ref maximum);
        IncludeTransformedPoint(new Vector3(bounds.Min.X, bounds.Max.Y, bounds.Min.Z), transform, ref minimum, ref maximum);
        IncludeTransformedPoint(new Vector3(bounds.Max.X, bounds.Max.Y, bounds.Min.Z), transform, ref minimum, ref maximum);
        IncludeTransformedPoint(new Vector3(bounds.Min.X, bounds.Min.Y, bounds.Max.Z), transform, ref minimum, ref maximum);
        IncludeTransformedPoint(new Vector3(bounds.Max.X, bounds.Min.Y, bounds.Max.Z), transform, ref minimum, ref maximum);
        IncludeTransformedPoint(new Vector3(bounds.Min.X, bounds.Max.Y, bounds.Max.Z), transform, ref minimum, ref maximum);
        IncludeTransformedPoint(new Vector3(bounds.Max.X, bounds.Max.Y, bounds.Max.Z), transform, ref minimum, ref maximum);
    }

    private static void IncludeTransformedPoint(Vector3 point, Matrix transform,
        ref Vector3 minimum, ref Vector3 maximum)
    {
        var transformed = Vector3.Transform(point, transform);
        minimum = Vector3.Min(minimum, transformed);
        maximum = Vector3.Max(maximum, transformed);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _singleSided.Dispose();
        _disposed = true;
    }
}
