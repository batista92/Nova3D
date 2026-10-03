using Nova3D.Rendering;

namespace Nova3D.Production.Scenes.BuiltIns;

/// <summary>A camera created from a scene node transform.</summary>
public sealed class SceneCameraComponent
{
    internal SceneCameraComponent(Camera3D camera, bool isPrimary)
    {
        Camera = camera;
        IsPrimary = isPrimary;
    }

    public Camera3D Camera { get; }
    public bool IsPrimary { get; }
}
