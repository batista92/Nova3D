using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;

namespace Nova3D.Production.Scenes;

/// <summary>Performs CPU-only semantic validation of a parsed scene document.</summary>
public static partial class SceneDocumentValidator
{
    public static SceneValidationResult Validate(
        SceneDocument document,
        SceneComponentRegistry? componentRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        componentRegistry ??= new SceneComponentRegistry();
        var issues = new List<SceneValidationIssue>();

        ValidateHeader(document, issues);

        var nodesById = new Dictionary<string, (SceneNodeDocument Node, int Index)>(StringComparer.Ordinal);
        for (var index = 0; index < document.Nodes.Count; index++)
            ValidateNode(document.Nodes[index], index, componentRegistry, nodesById, issues);

        ValidateParentReferences(document, nodesById, issues);
        ValidateParentCycles(document, nodesById, issues);
        return new SceneValidationResult(issues);
    }

    private static void ValidateHeader(SceneDocument document, ICollection<SceneValidationIssue> issues)
    {
        if (!string.Equals(document.Format, SceneDocument.FormatName, StringComparison.Ordinal))
        {
            Add(issues, SceneValidationCodes.InvalidFormat, "$.format",
                $"Expected '{SceneDocument.FormatName}', got '{document.Format}'.");
        }

        if (document.Version != SceneDocument.CurrentVersion)
        {
            Add(issues, SceneValidationCodes.UnsupportedVersion, "$.version",
                $"Only scene version {SceneDocument.CurrentVersion} is supported; got {document.Version}.");
        }

        if (string.IsNullOrWhiteSpace(document.Name))
            Add(issues, SceneValidationCodes.InvalidSceneName, "$.name", "Scene name cannot be empty.");
    }

    private static void ValidateNode(
        SceneNodeDocument node,
        int nodeIndex,
        SceneComponentRegistry componentRegistry,
        IDictionary<string, (SceneNodeDocument Node, int Index)> nodesById,
        ICollection<SceneValidationIssue> issues)
    {
        var path = $"$.nodes[{nodeIndex}]";
        var idIsValid = IsValidId(node.Id);
        if (!idIsValid)
        {
            Add(issues, SceneValidationCodes.InvalidNodeId, $"{path}.id",
                "Node ID must contain 1-128 ASCII characters and match [A-Za-z][A-Za-z0-9_.-]*.");
        }
        else if (!nodesById.TryAdd(node.Id, (node, nodeIndex)))
        {
            Add(issues, SceneValidationCodes.DuplicateNodeId, $"{path}.id",
                $"Node ID '{node.Id}' is already used by another node.");
        }

        if (string.IsNullOrWhiteSpace(node.Name))
            Add(issues, SceneValidationCodes.InvalidNodeName, $"{path}.name", "Node name cannot be empty.");

        if (node.ParentId is not null && !IsValidId(node.ParentId))
        {
            Add(issues, SceneValidationCodes.InvalidParentId, $"{path}.parent",
                "Parent ID must match [A-Za-z][A-Za-z0-9_.-]*.");
        }

        ValidateTransform(node.Transform, path, issues);
        ValidateComponents(node, path, componentRegistry, issues);
    }

    private static void ValidateTransform(
        SceneTransformDocument transform,
        string nodePath,
        ICollection<SceneValidationIssue> issues)
    {
        if (!IsFinite(transform.Position))
            Add(issues, SceneValidationCodes.NonFinitePosition, $"{nodePath}.transform.position",
                "Position components must be finite.");

        if (!IsFinite(transform.RotationDegrees))
            Add(issues, SceneValidationCodes.NonFiniteRotation, $"{nodePath}.transform.rotationDegrees",
                "Rotation components must be finite.");

        if (!IsFinite(transform.Scale))
        {
            Add(issues, SceneValidationCodes.NonFiniteScale, $"{nodePath}.transform.scale",
                "Scale components must be finite.");
        }
        else if (transform.Scale.X <= 0f || transform.Scale.Y <= 0f || transform.Scale.Z <= 0f)
        {
            Add(issues, SceneValidationCodes.NonPositiveScale, $"{nodePath}.transform.scale",
                "Every scale component must be greater than zero.");
        }
    }

    private static void ValidateComponents(
        SceneNodeDocument node,
        string nodePath,
        SceneComponentRegistry componentRegistry,
        ICollection<SceneValidationIssue> issues)
    {
        var componentIds = new HashSet<string>(StringComparer.Ordinal);
        for (var componentIndex = 0; componentIndex < node.Components.Count; componentIndex++)
        {
            var component = node.Components[componentIndex];
            var path = $"{nodePath}.components[{componentIndex}]";
            if (!IsValidId(component.Id))
            {
                Add(issues, SceneValidationCodes.InvalidComponentId, $"{path}.id",
                    "Component ID must contain 1-128 ASCII characters and match [A-Za-z][A-Za-z0-9_.-]*.");
            }
            else if (!componentIds.Add(component.Id))
            {
                Add(issues, SceneValidationCodes.DuplicateComponentId, $"{path}.id",
                    $"Component ID '{component.Id}' is already used by this node.");
            }

            if (!IsValidComponentType(component.Type))
            {
                Add(issues, SceneValidationCodes.InvalidComponentType, $"{path}.type",
                    "Component type must be a lower-case dot-separated identifier.");
            }
            else if (!componentRegistry.TryGet(component.Type, out var descriptor))
            {
                Add(issues, SceneValidationCodes.UnknownComponentType, $"{path}.type",
                    $"Component type '{component.Type}' is not registered.");
            }
            else
            {
                descriptor.Validate(new SceneComponentValidationContext(component, path, issues));
            }
        }
    }

    private static void ValidateParentReferences(
        SceneDocument document,
        IReadOnlyDictionary<string, (SceneNodeDocument Node, int Index)> nodesById,
        ICollection<SceneValidationIssue> issues)
    {
        for (var index = 0; index < document.Nodes.Count; index++)
        {
            var node = document.Nodes[index];
            if (node.ParentId is null || !IsValidId(node.ParentId))
                continue;

            var path = $"$.nodes[{index}].parent";
            if (string.Equals(node.Id, node.ParentId, StringComparison.Ordinal))
            {
                Add(issues, SceneValidationCodes.SelfParent, path,
                    $"Node '{node.Id}' cannot be its own parent.");
            }
            else if (!nodesById.ContainsKey(node.ParentId))
            {
                Add(issues, SceneValidationCodes.MissingParent, path,
                    $"Parent node '{node.ParentId}' does not exist.");
            }
        }
    }

    private static void ValidateParentCycles(
        SceneDocument document,
        IReadOnlyDictionary<string, (SceneNodeDocument Node, int Index)> nodesById,
        ICollection<SceneValidationIssue> issues)
    {
        var finished = new HashSet<string>(StringComparer.Ordinal);
        foreach (var startNode in document.Nodes)
        {
            if (!nodesById.TryGetValue(startNode.Id, out var registeredStart) ||
                !ReferenceEquals(startNode, registeredStart.Node) ||
                finished.Contains(startNode.Id))
            {
                continue;
            }

            var chain = new List<string>();
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            var currentId = startNode.Id;
            while (!finished.Contains(currentId) && nodesById.TryGetValue(currentId, out var current))
            {
                if (positions.TryGetValue(currentId, out var cycleStart))
                {
                    var cycle = chain.Skip(cycleStart).Append(currentId);
                    Add(issues, SceneValidationCodes.ParentCycle,
                        $"$.nodes[{current.Index}].parent",
                        $"Parent hierarchy contains a cycle: {string.Join(" -> ", cycle)}.");
                    break;
                }

                positions.Add(currentId, chain.Count);
                chain.Add(currentId);
                var parentId = current.Node.ParentId;
                if (parentId is null ||
                    string.Equals(parentId, currentId, StringComparison.Ordinal) ||
                    !nodesById.ContainsKey(parentId))
                {
                    break;
                }

                currentId = parentId;
            }

            foreach (var id in chain)
                finished.Add(id);
        }
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsValidId(string value) =>
        value.Length is >= 1 and <= 128 && IdPattern().IsMatch(value);

    internal static bool IsValidComponentType(string value) =>
        value.Length is >= 3 and <= 128 && ComponentTypePattern().IsMatch(value);

    private static void Add(
        ICollection<SceneValidationIssue> issues,
        string code,
        string path,
        string message) => issues.Add(new SceneValidationIssue(code, path, message));

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex ComponentTypePattern();
}
