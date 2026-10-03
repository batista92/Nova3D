using Microsoft.Xna.Framework;

namespace Nova3D.Production.Scenes;

/// <summary>Builds validated, CPU-only load plans without creating runtime resources.</summary>
public static class SceneLoader
{
    public static SceneLoadPlan Prepare(string path, SceneComponentRegistry componentRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        return Prepare(SceneDocumentSerializer.Load(fullPath), componentRegistry, fullPath);
    }

    public static SceneLoadPlan Prepare(
        SceneDocument document,
        SceneComponentRegistry componentRegistry,
        string documentPath = "<memory>")
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(componentRegistry);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        var validation = SceneDocumentValidator.Validate(document, componentRegistry);
        if (!validation.IsValid)
            throw new SceneDocumentValidationException(documentPath, validation.Issues);

        return new SceneLoadPlan(documentPath, document, BuildPlan(document, componentRegistry));
    }

    private static IReadOnlyList<ScenePlannedNode> BuildPlan(
        SceneDocument document,
        SceneComponentRegistry componentRegistry)
    {
        var childIndices = new Dictionary<string, List<int>>(document.Nodes.Count, StringComparer.Ordinal);
        var rootIndices = new List<int>();

        for (var index = 0; index < document.Nodes.Count; index++)
        {
            var node = document.Nodes[index];
            if (node.ParentId is null)
            {
                rootIndices.Add(index);
            }
            else
            {
                if (!childIndices.TryGetValue(node.ParentId, out var children))
                {
                    children = [];
                    childIndices.Add(node.ParentId, children);
                }
                children.Add(index);
            }
        }

        var ordered = new List<ScenePlannedNode>(document.Nodes.Count);
        var plansById = new Dictionary<string, ScenePlannedNode>(document.Nodes.Count, StringComparer.Ordinal);
        var queue = new Queue<int>(rootIndices);
        while (queue.TryDequeue(out var index))
        {
            var node = document.Nodes[index];
            var local = node.Transform.CreateLocalMatrix();
            var world = node.ParentId is null ? local : local * plansById[node.ParentId].WorldTransform;
            var components = node.Components.Select(component =>
                new ScenePlannedNode.PlannedComponent(
                    component,
                    componentRegistry.GetRequired(component.Type)));
            var planned = new ScenePlannedNode(node, local, world, components);
            ordered.Add(planned);
            plansById.Add(node.Id, planned);

            if (childIndices.TryGetValue(node.Id, out var children))
            {
                foreach (var childIndex in children)
                    queue.Enqueue(childIndex);
            }
        }

        if (ordered.Count != document.Nodes.Count)
            throw new InvalidOperationException("Validated scene hierarchy could not be ordered.");

        return ordered;
    }
}
