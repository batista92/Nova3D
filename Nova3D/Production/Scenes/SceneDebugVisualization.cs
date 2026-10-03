using Microsoft.Xna.Framework;
using Nova3D.Production.Debugging;
using Nova3D.Production.Scenes.BuiltIns;

namespace Nova3D.Production.Scenes;

/// <summary>Queues scene node bounds and reports world-space name anchors for a caller's text renderer.</summary>
public static class SceneDebugVisualization
{
    public static void Queue(
        SceneInstance scene,
        DebugRenderer lines,
        Action<string, Vector3>? nameAnchor = null,
        float markerSize = 0.35f,
        Color? nodeColor = null,
        Color? modelColor = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(lines);
        if (scene.IsDisposed)
            throw new ObjectDisposedException(nameof(scene));
        if (!float.IsFinite(markerSize) || markerSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(markerSize));

        var regular = nodeColor ?? Color.Cyan;
        var model = modelColor ?? Color.Yellow;
        foreach (var node in scene.Nodes)
        {
            var hasModel = false;
            foreach (var component in node.Components)
            {
                if (component.Value is not SceneModelComponent renderable)
                    continue;
                lines.BoundingBox(TransformBounds(renderable.Model.Bounds, renderable.Transform), model);
                hasModel = true;
            }
            if (!hasModel)
                lines.OrientedBox(node.WorldTransform, new Vector3(markerSize), regular);

            nameAnchor?.Invoke(node.Document.Name, node.WorldTransform.Translation + Vector3.Up * markerSize);
        }
    }

    private static BoundingBox TransformBounds(BoundingBox local, Matrix world)
    {
        var minimum = new Vector3(float.MaxValue);
        var maximum = new Vector3(float.MinValue);
        foreach (var corner in local.GetCorners())
        {
            var point = Vector3.Transform(corner, world);
            minimum = Vector3.Min(minimum, point);
            maximum = Vector3.Max(maximum, point);
        }
        return new BoundingBox(minimum, maximum);
    }
}
