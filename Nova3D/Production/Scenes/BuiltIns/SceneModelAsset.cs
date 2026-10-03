using Nova3D.Production.Assets.Gltf;
using Nova3D.Production.Scenes.Assets;

namespace Nova3D.Production.Scenes.BuiltIns;

/// <summary>A model plus the explicit ownership retained by a scene component.</summary>
public sealed class SceneModelAsset : IDisposable
{
    private readonly IDisposable? _lease;
    private readonly bool _ownsModel;
    private bool _disposed;

    private SceneModelAsset(GltfModel model, bool ownsModel, IDisposable? lease)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _ownsModel = ownsModel;
        _lease = lease;
    }

    public GltfModel Model { get; }

    public static SceneModelAsset Owned(GltfModel model) => new(model, true, null);
    public static SceneModelAsset Borrowed(GltfModel model) => new(model, false, null);

    public static SceneModelAsset Leased(SceneAssetLease<GltfModel> lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return new SceneModelAsset(lease.Value, false, lease);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _lease?.Dispose();
        if (_ownsModel)
            Model.Dispose();
        _disposed = true;
    }
}
