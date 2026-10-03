namespace Nova3D.Production.Scenes;

/// <summary>Provides component data and aggregated diagnostic reporting to a descriptor.</summary>
public sealed class SceneComponentValidationContext
{
    private readonly ICollection<SceneValidationIssue> _issues;

    internal SceneComponentValidationContext(
        SceneComponentDocument component,
        string componentJsonPath,
        ICollection<SceneValidationIssue> issues)
    {
        Component = component;
        ComponentJsonPath = componentJsonPath;
        _issues = issues;
    }

    public SceneComponentDocument Component { get; }
    public string ComponentJsonPath { get; }
    public string PropertiesJsonPath => $"{ComponentJsonPath}.properties";

    public void Report(string code, string message, string? propertyPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (propertyPath is not null &&
            (string.IsNullOrWhiteSpace(propertyPath) || propertyPath.StartsWith('$') || propertyPath.StartsWith('.')))
        {
            throw new ArgumentException(
                "A component property path must be relative to the properties object.", nameof(propertyPath));
        }

        var path = PropertiesJsonPath;
        if (propertyPath is not null)
            path += propertyPath.StartsWith('[') ? propertyPath : $".{propertyPath}";

        _issues.Add(new SceneValidationIssue(code, path, message));
    }
}
