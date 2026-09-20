using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Production.Assets;

public static class ShaderHotReload
{
    /// <summary>Registers raw MonoGame effect bytecode produced as a .mgfxo file.</summary>
    public static AssetHandle<Effect> LoadEffect(this FileAssetManager assets,
        string name, string mgfxoPath, GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(device);
        return assets.Load(name, mgfxoPath,
            path => new Effect(device, File.ReadAllBytes(path)),
            effect => effect.Dispose());
    }
}
