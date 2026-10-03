namespace Nova3D.Production.Scenes;

/// <summary>Prevents runtime allocation when a parsed scene is semantically invalid.</summary>
public sealed class SceneDocumentValidationException : Exception
{
    private readonly IReadOnlyList<SceneValidationIssue> _issues;

    public SceneDocumentValidationException(
        string documentPath,
        IEnumerable<SceneValidationIssue> issues)
        : base(CreateMessage(documentPath, issues, out var copiedIssues))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        DocumentPath = documentPath;
        _issues = copiedIssues;
    }

    public string DocumentPath { get; }
    public IReadOnlyList<SceneValidationIssue> Issues => _issues;

    private static string CreateMessage(
        string documentPath,
        IEnumerable<SceneValidationIssue> issues,
        out IReadOnlyList<SceneValidationIssue> copiedIssues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentNullException.ThrowIfNull(issues);
        var copy = issues.ToArray();
        copiedIssues = Array.AsReadOnly(copy);
        return $"Scene document '{documentPath}' has {copy.Length} validation issue(s):{Environment.NewLine}" +
               string.Join(Environment.NewLine, copy.Select(issue => issue.ToString()));
    }
}
