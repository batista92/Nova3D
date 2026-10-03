namespace Nova3D.Production.Scenes.BuiltIns;

/// <summary>Explicit registration helpers for Nova3D-owned scene component types.</summary>
public static class SceneBuiltInComponentRegistration
{
    public static void RegisterNova3DBuiltIns(
        this SceneComponentRegistry registry,
        SceneModelComponentDescriptor? modelDescriptor = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (modelDescriptor is not null)
            registry.Register(modelDescriptor);
        registry.Register(new SceneCameraComponentDescriptor());
        registry.Register(new SceneDirectionalLightComponentDescriptor());
        registry.Register(new SceneSpawnPointDescriptor());
        registry.Register(new SceneTagDescriptor());
    }
}
