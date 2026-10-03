# Persistence

Nova3D provides reliable storage mechanics while the game owns every data
contract. Define settings and progress as ordinary game records; do not persist
GPU resources, physics handles, Gum controls or runtime scene instances.

## Platform paths

```csharp
var paths = new GameDataPaths("MyStudio", "MyGame");

string settings = paths.SettingsFile;
string bindings = paths.BindingsFile;
string slot = paths.GetSaveSlotPath("campaign-01");
```

The default root comes from `Environment.SpecialFolder.LocalApplicationData`.
Studio, game and slot names must be safe single path segments. A custom base
directory exists for tests and portable tooling; packaged games should use the
platform default.

## Versioned settings

```csharp
public sealed record GameSettings(
    float MasterVolume = 1f,
    bool Fullscreen = false);

var store = new VersionedJsonStore<GameSettings>(
    paths.SettingsFile,
    documentType: "mygame.settings",
    currentVersion: 1,
    defaultFactory: () => new GameSettings(),
    validate: value =>
    {
        if (value.MasterVolume is < 0f or > 1f)
            throw new InvalidDataException("MasterVolume must be in [0,1].");
    },
    logger: logger);

PersistenceLoadResult<GameSettings> result = store.Load();
GameSettings settings = result.Value;
store.Save(settings with { Fullscreen = true });
```

The on-disk envelope contains `format`, `type`, `version` and `data`. Envelope
fields are strict and duplicate fields are rejected. The optional validator
runs before a value is accepted, so semantically invalid primary data can fall
back to the previous valid file.

`Save` writes and flushes a temporary file in the destination directory, swaps
it into place, and retains the previous primary as `.bak`. `Load` tries primary,
then backup, then defaults. Inspect `PersistenceLoadResult.Source`; do not hide a
recovery from the player when it matters.

## Explicit migration

Increase `currentVersion` only with an explicit migration:

```csharp
var progressStore = new VersionedJsonStore<GameProgress>(
    paths.GetSaveSlotPath("campaign-01"),
    "mygame.progress",
    currentVersion: 2,
    defaultFactory: () => new GameProgress(),
    migration: (sourceVersion, data) => sourceVersion switch
    {
        1 => new GameProgress(
            data.GetProperty("highestLevel").GetInt32(),
            BestSeconds: 0),
        _ => throw new JsonException($"Cannot migrate version {sourceVersion}.")
    });
```

When `WasMigrated` is true, call `Save(result.Value)` at a deliberate lifecycle
point to rewrite the current version. Future versions are rejected and never
partially applied.

## Input bindings

`InputBindingJson` remains the binding format. Store its JSON as a cloned
`JsonElement` so the outer document receives atomic backup and recovery:

```csharp
static JsonElement CaptureBindings(InputActionMap map)
{
    using JsonDocument document = JsonDocument.Parse(InputBindingJson.Serialize(map));
    return document.RootElement.Clone();
}

var bindingStore = new VersionedJsonStore<JsonElement>(
    paths.BindingsFile,
    "mygame.bindings",
    currentVersion: 1,
    defaultFactory: () => CaptureBindings(actions),
    validate: profile => InputBindingJson.Apply(actions, profile.GetRawText()));

var loadedBindings = bindingStore.Load();
bindingStore.Save(CaptureBindings(actions));
```

`InputBindingJson.Apply` is transactional: it validates the entire profile
before replacing bindings. Capture defaults before loading persisted bindings.

## Save slots and threading

Create one `VersionedJsonStore<T>` per slot path. Store only the minimum stable
game state needed to reconstruct play. The store is synchronous and is not
thread-safe; save on explicit checkpoints/settings confirmation, never during
the per-frame update hot path. Coordinate background saves in game code so two
writes cannot target the same file concurrently.

`Load` recovers expected file, JSON, validation and migration failures with a
logged fallback. `Save` deliberately propagates I/O failures so the game can
tell the player that progress was not written.

## Validation

```powershell
dotnet run --project Benchmarks/PersistenceContractTests/PersistenceContractTests.csproj -c Release
```

The contract covers platform-root containment, traversal rejection, atomic
replacement, repeated backup rotation, corrupted/invalid fallback, future
versions, explicit migration, bindings and save slots.
