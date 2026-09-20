using System.Text.Json;
using Nova3D.Production.Logging;

namespace Nova3D.Production.Configuration;

public static class ConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static Nova3DConfiguration LoadOrDefault(string path, ILogger? logger = null)
    {
        try
        {
            if (!File.Exists(path))
            {
                logger?.Log(LogLevel.Warning, "Configuration", $"Configuration not found at '{path}'; using defaults.");
                return new Nova3DConfiguration();
            }
            var configuration = JsonSerializer.Deserialize<Nova3DConfiguration>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("Configuration file produced no object.");
            configuration.Validate();
            logger?.Log(LogLevel.Information, "Configuration", $"Loaded '{Path.GetFullPath(path)}'.");
            return configuration;
        }
        catch (Exception exception)
        {
            logger?.Log(LogLevel.Error, "Configuration", "Invalid configuration; using defaults.", exception);
            return new Nova3DConfiguration();
        }
    }
}
