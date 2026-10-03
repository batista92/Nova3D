namespace Nova3D.Production.Scenes.Prefabs;

/// <summary>Identifies a prefab expansion or override error before runtime allocation.</summary>
public sealed class ScenePrefabException : Exception
{
    public ScenePrefabException(string documentPath, string jsonPath, string message)
        : base($"Prefab error in '{documentPath}' at {jsonPath}: {message}")
    {
        DocumentPath = documentPath;
        JsonPath = jsonPath;
    }

    public string DocumentPath { get; }
    public string JsonPath { get; }
}
