namespace Nova3D.Production.Scenes;

/// <summary>Aggregated semantic validation result in deterministic issue order.</summary>
public sealed class SceneValidationResult
{
    private readonly IReadOnlyList<SceneValidationIssue> _issues;

    internal SceneValidationResult(IEnumerable<SceneValidationIssue> issues)
    {
        _issues = Array.AsReadOnly(issues.ToArray());
    }

    public bool IsValid => _issues.Count == 0;
    public IReadOnlyList<SceneValidationIssue> Issues => _issues;
}
