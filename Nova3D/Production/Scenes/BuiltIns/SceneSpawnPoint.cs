using Microsoft.Xna.Framework;

namespace Nova3D.Production.Scenes.BuiltIns;

/// <summary>A named gameplay spawn transform authored in a scene.</summary>
public sealed class SceneSpawnPoint
{
    internal SceneSpawnPoint(string kind, Matrix worldTransform)
    {
        Kind = kind;
        WorldTransform = worldTransform;
    }

    public string Kind { get; }
    public Matrix WorldTransform { get; }
    public Vector3 Position => WorldTransform.Translation;
}
