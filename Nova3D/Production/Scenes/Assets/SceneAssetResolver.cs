namespace Nova3D.Production.Scenes.Assets;

/// <summary>Resolves portable scene references inside one configured Assets root.</summary>
public sealed class SceneAssetResolver
{
    private readonly string _rootWithSeparator;

    public SceneAssetResolver(string assetsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsRoot);
        AssetsRoot = Path.GetFullPath(assetsRoot);
        _rootWithSeparator = Path.EndsInDirectorySeparator(AssetsRoot)
            ? AssetsRoot
            : AssetsRoot + Path.DirectorySeparatorChar;
    }

    public string AssetsRoot { get; }

    public string ResolveExistingFile(SceneLoadPlan plan, string assetReference)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return ResolveExistingFile(plan.DocumentPath, assetReference);
    }

    public string ResolveExistingFile(string scenePath, string assetReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetReference);

        ValidatePortableReference(scenePath, assetReference);
        var platformReference = assetReference.Replace('/', Path.DirectorySeparatorChar);
        var resolved = Path.GetFullPath(Path.Combine(AssetsRoot, platformReference));
        if (!resolved.StartsWith(_rootWithSeparator, PathComparison()) &&
            !string.Equals(resolved, AssetsRoot, PathComparison()))
        {
            throw new SceneAssetPathException(
                scenePath, assetReference, "the resolved path escapes the configured Assets root.");
        }

        if (!File.Exists(resolved))
            throw new SceneAssetNotFoundException(scenePath, assetReference, resolved);
        if (!HasExactPathCasing(assetReference))
        {
            throw new SceneAssetPathException(
                scenePath, assetReference,
                "path casing does not match the file system; use the exact casing for cross-platform builds.");
        }
        return resolved;
    }

    private static void ValidatePortableReference(string scenePath, string assetReference)
    {
        var invalid = assetReference.Contains('\\') ||
                      Path.IsPathRooted(assetReference) ||
                      Uri.TryCreate(assetReference, UriKind.Absolute, out _) ||
                      assetReference.Split('/').Any(segment => segment is "" or "." or "..");
        if (invalid)
        {
            throw new SceneAssetPathException(
                scenePath,
                assetReference,
                "use a '/'-separated path relative to the configured Assets root without '.' or '..'.");
        }
    }

    private bool HasExactPathCasing(string assetReference)
    {
        var current = AssetsRoot;
        foreach (var segment in assetReference.Split('/'))
        {
            var match = Directory.EnumerateFileSystemEntries(current)
                .Select(Path.GetFileName)
                .Any(name => string.Equals(name, segment, StringComparison.Ordinal));
            if (!match)
                return false;
            current = Path.Combine(current, segment);
        }
        return true;
    }

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
