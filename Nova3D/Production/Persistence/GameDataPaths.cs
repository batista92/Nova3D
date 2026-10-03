namespace Nova3D.Production.Persistence;

/// <summary>Platform user-data paths for one studio/game identity.</summary>
public sealed class GameDataPaths
{
    public GameDataPaths(string studioName, string gameName, string? baseDirectory = null)
    {
        ValidateSegment(studioName, nameof(studioName));
        ValidateSegment(gameName, nameof(gameName));

        string root = baseDirectory ?? Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);
        if (string.IsNullOrWhiteSpace(root))
            throw new PlatformNotSupportedException("The platform did not provide a local application-data directory.");

        RootDirectory = Path.GetFullPath(Path.Combine(root, studioName, gameName));
        SettingsFile = Path.Combine(RootDirectory, "settings.json");
        BindingsFile = Path.Combine(RootDirectory, "bindings.json");
        SavesDirectory = Path.Combine(RootDirectory, "Saves");
    }

    public string RootDirectory { get; }
    public string SettingsFile { get; }
    public string BindingsFile { get; }
    public string SavesDirectory { get; }

    public string GetSaveSlotPath(string slotName)
    {
        ValidateSegment(slotName, nameof(slotName));
        return Path.Combine(SavesDirectory, slotName + ".json");
    }

    private static void ValidateSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value is "." or ".." ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.Contains(Path.DirectorySeparatorChar) ||
            value.Contains(Path.AltDirectorySeparatorChar))
            throw new ArgumentException("User-data path segments must be safe file names.", parameterName);
    }
}
