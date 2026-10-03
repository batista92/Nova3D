namespace Nova3D.Production.Scenes;

/// <summary>Describes and validates one explicitly registered scene component type.</summary>
public interface ISceneComponentDescriptor
{
    string Type { get; }
    void Validate(SceneComponentValidationContext context);
}
