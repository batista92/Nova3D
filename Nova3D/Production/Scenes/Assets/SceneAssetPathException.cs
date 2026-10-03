namespace Nova3D.Production.Scenes.Assets;

/// <summary>Reports a scene asset reference that cannot safely resolve under its root.</summary>
public sealed class SceneAssetPathException : IOException
{
    public SceneAssetPathException(string scenePath, string assetReference, string message)
        : base($"Invalid asset reference '{assetReference}' in scene '{scenePath}': {message}")
    {
        ScenePath = scenePath;
        AssetReference = assetReference;
    }

    public string ScenePath { get; }
    public string AssetReference { get; }
}
