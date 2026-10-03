namespace Nova3D.Production.Scenes;

/// <summary>A deterministic semantic problem in a scene document.</summary>
public sealed record SceneValidationIssue(string Code, string JsonPath, string Message)
{
    public override string ToString() => $"{Code} {JsonPath}: {Message}";
}
