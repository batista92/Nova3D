using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Rendering.Lighting;

/// <summary>Split-sum IBL textures consumed by PBR materials.</summary>
public sealed class ImageBasedLighting
{
    public ImageBasedLighting(TextureCube environmentMap, TextureCube irradianceMap,
        TextureCube prefilteredMap, Texture2D brdfLut, int prefilteredMipCount)
    {
        EnvironmentMap = environmentMap ?? throw new ArgumentNullException(nameof(environmentMap));
        IrradianceMap = irradianceMap ?? throw new ArgumentNullException(nameof(irradianceMap));
        PrefilteredMap = prefilteredMap ?? throw new ArgumentNullException(nameof(prefilteredMap));
        BrdfLut = brdfLut ?? throw new ArgumentNullException(nameof(brdfLut));
        if (prefilteredMipCount <= 0) throw new ArgumentOutOfRangeException(nameof(prefilteredMipCount));
        PrefilteredMipCount = prefilteredMipCount;
    }

    public TextureCube EnvironmentMap { get; }
    public TextureCube IrradianceMap { get; }
    public TextureCube PrefilteredMap { get; }
    public Texture2D BrdfLut { get; }
    public int PrefilteredMipCount { get; }
}
