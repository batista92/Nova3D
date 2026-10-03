using Microsoft.Xna.Framework;
using Nova3D.Production.Assets.Gltf;

namespace Nova3D.Production.Debugging;

public static class GltfSkeletonDebug
{
    /// <summary>Adds animated hierarchy bones and joint crosses to an existing debug batch.</summary>
    public static void Draw(DebugRenderer debug, GltfModel model, GltfSkeletonPose pose,
        Matrix transform, Color boneColor, Color jointColor, float jointSize = 0.025f)
    {
        ArgumentNullException.ThrowIfNull(debug);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(pose);
        if (pose.NodeCount != model.Nodes.Count)
            throw new ArgumentException("Pose and model node counts differ.", nameof(pose));
        if (!float.IsFinite(jointSize) || jointSize < 0f)
            throw new ArgumentOutOfRangeException(nameof(jointSize));

        for (var nodeIndex = 0; nodeIndex < model.Nodes.Count; nodeIndex++)
        {
            var position = Position(pose.GetWorldTransform(nodeIndex), transform);
            var parent = model.Nodes[nodeIndex].Parent;
            if (parent >= 0)
                debug.Line(Position(pose.GetWorldTransform(parent), transform), position, boneColor);
            if (jointSize <= 0f) continue;
            debug.Line(position - Vector3.Right * jointSize, position + Vector3.Right * jointSize, jointColor);
            debug.Line(position - Vector3.Up * jointSize, position + Vector3.Up * jointSize, jointColor);
            debug.Line(position - Vector3.Backward * jointSize, position + Vector3.Backward * jointSize, jointColor);
        }
    }

    private static Vector3 Position(Matrix nodeWorld, Matrix transform) =>
        Vector3.Transform(new Vector3(nodeWorld.M41, nodeWorld.M42, nodeWorld.M43), transform);
}
