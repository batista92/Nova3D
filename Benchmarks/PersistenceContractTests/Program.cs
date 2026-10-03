using System.Text.Json;
using Microsoft.Xna.Framework.Input;
using Nova3D.Production.Input;
using Nova3D.Production.Logging;
using Nova3D.Production.Persistence;

string temporaryBase = Path.GetFullPath(Path.Combine(Path.GetTempPath(),
    "nova3d-persistence-" + Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(temporaryBase);

try
{
    var paths = new GameDataPaths("Nova Studio", "Contract Game", temporaryBase);
    Require(paths.RootDirectory.StartsWith(temporaryBase, StringComparison.OrdinalIgnoreCase),
        "user data remains under selected platform base");
    Require(paths.SettingsFile.EndsWith("settings.json", StringComparison.Ordinal), "settings path");
    Require(paths.BindingsFile.EndsWith("bindings.json", StringComparison.Ordinal), "bindings path");
    Require(paths.GetSaveSlotPath("slot-01").EndsWith(
        Path.Combine("Saves", "slot-01.json"), StringComparison.Ordinal), "save slot path");
    Expect<ArgumentException>(() => paths.GetSaveSlotPath("../escape"), "slot traversal");

    var logger = new CaptureLogger();
    var settingsStore = new VersionedJsonStore<GameSettings>(
        paths.SettingsFile, "game.settings", 1, () => new GameSettings(),
        validate: value =>
        {
            if (value.MasterVolume is < 0f or > 1f)
                throw new InvalidDataException("Master volume is outside [0,1].");
        }, logger: logger);

    var missing = settingsStore.Load();
    Require(missing.Source == PersistenceLoadSource.DefaultMissing &&
        missing.Value == new GameSettings(), "missing settings use defaults");

    var first = new GameSettings(0.75f, false);
    settingsStore.Save(first);
    Require(!Directory.EnumerateFiles(paths.RootDirectory, "*.tmp").Any(),
        "atomic save leaves no temporary file");
    var loaded = settingsStore.Load();
    Require(loaded.Source == PersistenceLoadSource.Primary && loaded.Value == first &&
        loaded.SourceVersion == 1 && !loaded.WasMigrated, "primary settings round trip");

    var second = new GameSettings(0.25f, true);
    settingsStore.Save(second);
    Require(File.Exists(settingsStore.BackupPath), "second save retains last-known-good backup");
    var third = new GameSettings(0.5f, true);
    settingsStore.Save(third);
    File.WriteAllText(settingsStore.Path,
        """
        {
          "format":"nova3d.data",
          "type":"game.settings",
          "version":1,
          "data":{"masterVolume":2.0,"fullscreen":false}
        }
        """);
    var recovered = settingsStore.Load();
    Require(recovered.Source == PersistenceLoadSource.Backup && recovered.Value == second,
        "semantically invalid primary recovers previous settings");

    File.WriteAllText(settingsStore.BackupPath, "[]");
    var invalid = settingsStore.Load();
    Require(invalid.Source == PersistenceLoadSource.DefaultInvalid &&
        invalid.Value == new GameSettings(), "corrupt primary and backup use defaults");
    Require(logger.Entries.Any(entry => entry.Level == LogLevel.Error &&
        entry.Message.Contains(settingsStore.Path, StringComparison.Ordinal)),
        "failure log contains affected path");

    string progressPath = paths.GetSaveSlotPath("campaign");
    Directory.CreateDirectory(Path.GetDirectoryName(progressPath)!);
    File.WriteAllText(progressPath,
        """
        {
          "format": "nova3d.data",
          "type": "game.progress",
          "version": 1,
          "data": { "highestLevel": 3 }
        }
        """);
    var progressStore = new VersionedJsonStore<GameProgress>(
        progressPath, "game.progress", 2, () => new GameProgress(),
        migration: (sourceVersion, data) => sourceVersion switch
        {
            1 => new GameProgress(data.GetProperty("highestLevel").GetInt32(), BestSeconds: 0),
            _ => throw new JsonException($"Unsupported migration source {sourceVersion}.")
        });
    var migrated = progressStore.Load();
    Require(migrated.Source == PersistenceLoadSource.Primary && migrated.WasMigrated &&
        migrated.SourceVersion == 1 && migrated.Value == new GameProgress(3, 0),
        "explicit v1 to v2 migration");
    progressStore.Save(migrated.Value);
    var upgraded = progressStore.Load();
    Require(upgraded.SourceVersion == 2 && !upgraded.WasMigrated,
        "saving migrated value writes current version");

    string futurePath = paths.GetSaveSlotPath("future");
    File.WriteAllText(futurePath,
        """
        { "format":"nova3d.data", "type":"game.progress", "version":99, "data":{} }
        """);
    var futureStore = new VersionedJsonStore<GameProgress>(
        futurePath, "game.progress", 2, () => new GameProgress());
    Require(futureStore.Load().Source == PersistenceLoadSource.DefaultInvalid,
        "future version is rejected without partial data");

    var sourceMap = new InputActionMap();
    sourceMap.Add("jump", InputActionKind.Digital,
        InputBinding.Key(Keys.Space), InputBinding.GamePadButtonInput(Buttons.A));
    using JsonDocument bindingDocument = JsonDocument.Parse(InputBindingJson.Serialize(sourceMap));
    var bindingStore = new VersionedJsonStore<JsonElement>(
        paths.BindingsFile, "game.bindings", 1,
        () => JsonDocument.Parse(InputBindingJson.Serialize(sourceMap)).RootElement.Clone());
    bindingStore.Save(bindingDocument.RootElement.Clone());
    var targetMap = new InputActionMap();
    var targetJump = targetMap.Add("jump", InputActionKind.Digital, InputBinding.Key(Keys.J));
    InputBindingJson.Apply(targetMap, bindingStore.Load().Value.GetRawText());
    string restoredBindings = InputBindingJson.Serialize(targetMap);
    Require(targetJump.Bindings.Count == 2 &&
        restoredBindings.Contains("\"key\": \"Space\"", StringComparison.Ordinal),
        "versioned bindings round trip through generic store");

    Console.WriteLine("Persistence G6.2 PASS | paths | atomic backup | corruption fallback | migration | bindings | slots");
}
finally
{
    string tempRoot = Path.GetFullPath(Path.GetTempPath());
    if (!temporaryBase.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
        temporaryBase.Length <= tempRoot.Length)
        throw new InvalidOperationException("Refusing to delete an unverified persistence test directory.");
    if (Directory.Exists(temporaryBase)) Directory.Delete(temporaryBase, recursive: true);
}

static void Require(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException($"FAIL: {description}");
}

static void Expect<TException>(Action action, string description) where TException : Exception
{
    try { action(); }
    catch (TException) { return; }
    throw new InvalidOperationException($"FAIL: {description} did not throw {typeof(TException).Name}");
}

sealed record GameSettings(float MasterVolume = 1f, bool Fullscreen = false);
sealed record GameProgress(int HighestUnlockedLevel = 1, double BestSeconds = 0);

sealed class CaptureLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];
    public void Log(LogLevel level, string category, string message, Exception? exception = null) =>
        Entries.Add((level, message));
}
