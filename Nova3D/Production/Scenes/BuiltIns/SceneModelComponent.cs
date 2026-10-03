using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Production.Assets.Gltf;
using Nova3D.Rendering;
using Nova3D.Rendering.Models;

namespace Nova3D.Production.Scenes.BuiltIns;

/// <summary>Renderable glTF scene component with explicit model ownership.</summary>
public sealed class SceneModelComponent : IDisposable
{
    private readonly SceneModelAsset _assetOwnership;
    private bool _disposed;

    internal SceneModelComponent(
        string asset,
        SceneModelAsset assetOwnership,
        GltfModelRenderer renderer,
        bool visible,
        bool castsShadows)
    {
        Asset = asset;
        _assetOwnership = assetOwnership;
        Model = assetOwnership.Model;
        Renderer = renderer;
        Visible = visible;
        CastsShadows = castsShadows;
    }

    public string Asset { get; }
    public GltfModel Model { get; }
    public GltfModelRenderer Renderer { get; }
    public bool Visible { get; set; }
    public bool CastsShadows { get; set; }
    public Matrix Transform { get => Renderer.Transform; set => Renderer.Transform = value; }

    public void Draw(RenderContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Visible)
            Renderer.Draw(context);
    }

    public void DrawShadows(RenderContext context, Effect shadowEffect, Matrix lightViewProjection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Visible && CastsShadows)
            Renderer.DrawShadows(context, shadowEffect, lightViewProjection);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Renderer.Dispose();
        _assetOwnership.Dispose();
        _disposed = true;
    }
}
