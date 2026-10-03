namespace Nova3D.Production.Scenes.Assets;

/// <summary>Reports a missing scene asset with authored and resolved paths.</summary>
public sealed class SceneAssetNotFoundException : FileNotFoundException
{
    public SceneAssetNotFoundException(
        string scenePath,
        string assetReference,
        string resolvedPath)
        : base(
            $"Scene asset '{assetReference}' referenced by '{scenePath}' was not found at '{resolvedPath}'.",
            resolvedPath)
    {
        ScenePath = scenePath;
        AssetReference = assetReference;
        ResolvedPath = resolvedPath;
    }

    public string ScenePath { get; }
    public string AssetReference { get; }
    public string ResolvedPath { get; }
}
