using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Production.Assets.Gltf;

public static class GltfAssetExtensions
{
    /// <summary>
    /// Registers a glTF model as a versioned file asset. GLB is preferred for
    /// hot reload because all dependent binary data is stored in one file.
    /// </summary>
    public static AssetHandle<GltfModel> LoadGltf(this FileAssetManager assets,
        string name, string path, GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(assets);
        var importer = new GltfImporter(device);
        return assets.Load(name, path, importer.Load, model => model.Dispose());
    }
}
