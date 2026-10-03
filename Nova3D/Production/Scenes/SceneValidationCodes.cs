namespace Nova3D.Production.Scenes;

/// <summary>Stable diagnostic codes produced by <see cref="SceneDocumentValidator"/>.</summary>
public static class SceneValidationCodes
{
    public const string InvalidFormat = "SCN001";
    public const string UnsupportedVersion = "SCN002";
    public const string InvalidSceneName = "SCN003";

    public const string InvalidNodeId = "SCN101";
    public const string DuplicateNodeId = "SCN102";
    public const string InvalidNodeName = "SCN103";
    public const string InvalidParentId = "SCN104";
    public const string MissingParent = "SCN105";
    public const string SelfParent = "SCN106";
    public const string ParentCycle = "SCN107";
    public const string NonFinitePosition = "SCN111";
    public const string NonFiniteRotation = "SCN112";
    public const string NonFiniteScale = "SCN113";
    public const string NonPositiveScale = "SCN114";

    public const string InvalidComponentId = "SCN201";
    public const string DuplicateComponentId = "SCN202";
    public const string InvalidComponentType = "SCN203";
    public const string UnknownComponentType = "SCN204";
}
