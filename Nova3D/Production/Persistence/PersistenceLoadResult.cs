namespace Nova3D.Production.Persistence;

public enum PersistenceLoadSource
{
    Primary,
    Backup,
    DefaultMissing,
    DefaultInvalid
}

/// <summary>Describes exactly where a persisted value came from.</summary>
public readonly record struct PersistenceLoadResult<T>(
    T Value,
    PersistenceLoadSource Source,
    int? SourceVersion,
    bool WasMigrated,
    string Path)
{
    public bool UsedFallback => Source != PersistenceLoadSource.Primary;
}

/// <summary>Converts an older document payload into the current game-owned type.</summary>
public delegate T PersistenceMigration<T>(int sourceVersion, System.Text.Json.JsonElement data);
