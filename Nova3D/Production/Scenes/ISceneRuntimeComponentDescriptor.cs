namespace Nova3D.Production.Scenes;

/// <summary>Creates and destroys one runtime value for a validated scene component.</summary>
public interface ISceneRuntimeComponentDescriptor : ISceneComponentDescriptor
{
    object Create(SceneComponentInstantiationContext context);
    void Destroy(object instance);
}
