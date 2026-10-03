using System.Text.Json;
using System.Text.Json.Serialization;
using Nova3D.Production.Logging;

namespace Nova3D.Production.Persistence;

/// <summary>
/// Versioned JSON persistence with same-directory atomic replacement and one
/// last-known-good backup. Call outside the frame hot path.
/// </summary>
public sealed class VersionedJsonStore<T> where T : notnull
{
    private const string Format = "nova3d.data";
    private readonly Func<T> _defaultFactory;
    private readonly PersistenceMigration<T>? _migration;
    private readonly Action<T>? _validate;
    private readonly JsonSerializerOptions _options;
    private readonly ILogger? _logger;

    public VersionedJsonStore(
        string path,
        string documentType,
        int currentVersion,
        Func<T> defaultFactory,
        PersistenceMigration<T>? migration = null,
        Action<T>? validate = null,
        JsonSerializerOptions? options = null,
        ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
        ArgumentNullException.ThrowIfNull(defaultFactory);
        if (currentVersion <= 0) throw new ArgumentOutOfRangeException(nameof(currentVersion));
        if (documentType.Any(char.IsWhiteSpace))
            throw new ArgumentException("Document type cannot contain whitespace.", nameof(documentType));

        Path = System.IO.Path.GetFullPath(path);
        BackupPath = Path + ".bak";
        DocumentType = documentType;
        CurrentVersion = currentVersion;
        _defaultFactory = defaultFactory;
        _migration = migration;
        _validate = validate;
        _logger = logger;
        _options = options is null ? CreateDefaultOptions() : new JsonSerializerOptions(options);
    }

    public string Path { get; }
    public string BackupPath { get; }
    public string DocumentType { get; }
    public int CurrentVersion { get; }

    public PersistenceLoadResult<T> Load()
    {
        bool primaryExists = File.Exists(Path);
        bool backupExists = File.Exists(BackupPath);
        bool hadInvalidFile = false;

        if (primaryExists)
        {
            if (TryLoad(Path, out T? value, out int version, out bool migrated, out Exception? error))
            {
                _logger?.Log(LogLevel.Information, "Persistence",
                    $"Loaded '{Path}' at version {version}.");
                return new(value, PersistenceLoadSource.Primary, version, migrated, Path);
            }

            hadInvalidFile = true;
            _logger?.Log(LogLevel.Error, "Persistence",
                $"Could not load primary '{Path}'; trying backup '{BackupPath}'.", error);
        }

        if (backupExists)
        {
            if (TryLoad(BackupPath, out T? value, out int version, out bool migrated, out Exception? error))
            {
                _logger?.Log(LogLevel.Warning, "Persistence",
                    $"Recovered '{Path}' from backup '{BackupPath}' at version {version}.");
                return new(value, PersistenceLoadSource.Backup, version, migrated, BackupPath);
            }

            hadInvalidFile = true;
            _logger?.Log(LogLevel.Error, "Persistence",
                $"Could not load backup '{BackupPath}'; using defaults for '{Path}'.", error);
        }

        var source = hadInvalidFile
            ? PersistenceLoadSource.DefaultInvalid
            : PersistenceLoadSource.DefaultMissing;
        _logger?.Log(hadInvalidFile ? LogLevel.Error : LogLevel.Information, "Persistence",
            hadInvalidFile
                ? $"No valid persisted value remains for '{Path}'; using defaults."
                : $"No persisted value exists at '{Path}'; using defaults.");
        return new(_defaultFactory(), source, null, false, Path);
    }

    public void Save(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        byte[] bytes = Serialize(value);
        string? directory = System.IO.Path.GetDirectoryName(Path);
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException($"Persistence path '{Path}' has no directory.");

        Directory.CreateDirectory(directory);
        string temporaryPath = System.IO.Path.Combine(directory,
            $".{System.IO.Path.GetFileName(Path)}.{Guid.NewGuid():N}.tmp");
        string previousPath = temporaryPath + ".previous";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(Path))
            {
                try
                {
                    File.Replace(temporaryPath, Path, previousPath, ignoreMetadataErrors: true);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(Path, previousPath, overwrite: false);
                    File.Move(temporaryPath, Path, overwrite: true);
                }
                File.Move(previousPath, BackupPath, overwrite: true);
            }
            else
            {
                File.Move(temporaryPath, Path);
            }

            _logger?.Log(LogLevel.Information, "Persistence",
                $"Saved '{Path}' at version {CurrentVersion}.");
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            if (File.Exists(previousPath)) File.Delete(previousPath);
        }
    }

    private bool TryLoad(
        string sourcePath,
        out T value,
        out int sourceVersion,
        out bool migrated,
        out Exception? error)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(sourcePath),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonException("Persistence document root must be an object.");

            ValidateEnvelopeProperties(root);
            string format = RequiredString(root, "format");
            string type = RequiredString(root, "type");
            sourceVersion = RequiredVersion(root);
            if (format != Format) throw new JsonException($"Unsupported persistence format '{format}'.");
            if (!string.Equals(type, DocumentType, StringComparison.Ordinal))
                throw new JsonException($"Expected document type '{DocumentType}', found '{type}'.");
            if (sourceVersion > CurrentVersion)
                throw new JsonException(
                    $"Document version {sourceVersion} is newer than supported version {CurrentVersion}.");

            JsonElement data = root.GetProperty("data");
            migrated = sourceVersion < CurrentVersion;
            if (migrated)
            {
                if (_migration is null)
                    throw new JsonException(
                        $"Document version {sourceVersion} requires an explicit migration to {CurrentVersion}.");
                value = _migration(sourceVersion, data.Clone()) ??
                    throw new JsonException("Persistence migration returned null.");
            }
            else
            {
                value = data.Deserialize<T>(_options) ??
                    throw new JsonException("Persistence data produced a null value.");
            }

            _validate?.Invoke(value);

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            value = default!;
            sourceVersion = 0;
            migrated = false;
            error = exception;
            return false;
        }
    }

    private byte[] Serialize(T value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", Format);
            writer.WriteString("type", DocumentType);
            writer.WriteNumber("version", CurrentVersion);
            writer.WritePropertyName("data");
            JsonSerializer.Serialize(writer, value, _options);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static void ValidateEnvelopeProperties(JsonElement root)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!names.Add(property.Name))
                throw new JsonException($"Duplicate persistence property '{property.Name}'.");
            if (property.Name is not ("format" or "type" or "version" or "data"))
                throw new JsonException($"Unknown persistence property '{property.Name}'.");
        }
        if (names.Count != 4 || !names.Contains("data"))
            throw new JsonException("Persistence document requires format, type, version and data.");
    }

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            throw new JsonException($"Persistence property '{name}' must be a string.");
        return value.GetString()!;
    }

    private static int RequiredVersion(JsonElement root)
    {
        if (!root.TryGetProperty("version", out JsonElement value) ||
            !value.TryGetInt32(out int version) || version <= 0)
            throw new JsonException("Persistence property 'version' must be a positive integer.");
        return version;
    }

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
}
