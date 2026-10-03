using Microsoft.Xna.Framework;

namespace Nova3D.Production.Assets.Gltf;

/// <summary>Mutable local and world node transforms for one glTF model instance.</summary>
public sealed class GltfSkeletonPose
{
    private readonly IReadOnlyList<GltfNode> _nodes;
    private readonly IReadOnlyList<GltfSkin> _skins;
    private readonly int[] _evaluationOrder;
    private readonly Vector3[] _translations;
    private readonly Quaternion[] _rotations;
    private readonly Vector3[] _scales;
    private readonly Matrix[] _localTransforms;
    private readonly Matrix[] _worldTransforms;

    public GltfSkeletonPose(GltfModel model)
        : this(model?.Nodes ?? throw new ArgumentNullException(nameof(model)), model.Skins)
    {
    }

    internal GltfSkeletonPose(IReadOnlyList<GltfNode> nodes, IReadOnlyList<GltfSkin> skins)
    {
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _skins = skins ?? throw new ArgumentNullException(nameof(skins));
        _evaluationOrder = BuildEvaluationOrder(nodes);
        _translations = new Vector3[nodes.Count];
        _rotations = new Quaternion[nodes.Count];
        _scales = new Vector3[nodes.Count];
        _localTransforms = new Matrix[nodes.Count];
        _worldTransforms = new Matrix[nodes.Count];
        ResetToBindPose();
    }

    public int NodeCount => _nodes.Count;
    public ReadOnlySpan<Matrix> LocalTransforms => _localTransforms;
    public ReadOnlySpan<Matrix> WorldTransforms => _worldTransforms;

    public Vector3 GetTranslation(int nodeIndex) => _translations[ValidateNode(nodeIndex)];
    public Quaternion GetRotation(int nodeIndex) => _rotations[ValidateNode(nodeIndex)];
    public Vector3 GetScale(int nodeIndex) => _scales[ValidateNode(nodeIndex)];
    public Matrix GetLocalTransform(int nodeIndex) => _localTransforms[ValidateNode(nodeIndex)];
    public Matrix GetWorldTransform(int nodeIndex) => _worldTransforms[ValidateNode(nodeIndex)];

    public void ResetToBindPose()
    {
        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            _translations[i] = node.Translation;
            _rotations[i] = node.Rotation;
            _scales[i] = node.Scale;
            _localTransforms[i] = node.LocalTransform;
        }
        UpdateWorldTransforms();
    }

    public void UpdateWorldTransforms()
    {
        for (var orderIndex = 0; orderIndex < _evaluationOrder.Length; orderIndex++)
        {
            var nodeIndex = _evaluationOrder[orderIndex];
            var parent = _nodes[nodeIndex].Parent;
            _worldTransforms[nodeIndex] = parent < 0
                ? _localTransforms[nodeIndex]
                : _localTransforms[nodeIndex] * _worldTransforms[parent];
        }
    }

    /// <summary>
    /// Writes matrices for a primitive-local joint palette. The destination may be larger than
    /// the palette; only the first <c>jointPalette.Count</c> entries are changed.
    /// </summary>
    public void WriteSkinPalette(int skinIndex, int meshNodeIndex,
        IReadOnlyList<int> jointPalette, Span<Matrix> destination)
    {
        ArgumentNullException.ThrowIfNull(jointPalette);
        if ((uint)skinIndex >= (uint)_skins.Count) throw new ArgumentOutOfRangeException(nameof(skinIndex));
        ValidateNode(meshNodeIndex);
        if (destination.Length < jointPalette.Count)
            throw new ArgumentException("Destination is shorter than the primitive joint palette.", nameof(destination));

        var skin = _skins[skinIndex];
        var inverseMesh = Matrix.Invert(_worldTransforms[meshNodeIndex]);
        if (!IsFinite(inverseMesh))
            throw new InvalidOperationException("The animated mesh node transform is not invertible.");
        for (var i = 0; i < jointPalette.Count; i++)
        {
            var skinJoint = jointPalette[i];
            if ((uint)skinJoint >= (uint)skin.Joints.Count)
                throw new ArgumentOutOfRangeException(nameof(jointPalette),
                    $"Joint palette entry {skinJoint} is outside skin {skinIndex}.");
            var jointNode = skin.Joints[skinJoint];
            destination[i] = skin.InverseBindMatrices[skinJoint] *
                             _worldTransforms[jointNode] * inverseMesh;
        }
    }

    internal void ApplyLocalPose(Vector3[] translations, Quaternion[] rotations, Vector3[] scales)
    {
        for (var i = 0; i < _nodes.Count; i++)
        {
            _translations[i] = translations[i];
            _rotations[i] = rotations[i];
            _scales[i] = scales[i];
            _localTransforms[i] = _nodes[i].UsesMatrix
                ? _nodes[i].LocalTransform
                : Matrix.CreateScale(scales[i]) * Matrix.CreateFromQuaternion(rotations[i]) *
                  Matrix.CreateTranslation(translations[i]);
        }
        UpdateWorldTransforms();
    }

    internal void ApplyBlendedLocalPose(Vector3[] sourceTranslations, Quaternion[] sourceRotations,
        Vector3[] sourceScales, Vector3[] targetTranslations, Quaternion[] targetRotations,
        Vector3[] targetScales, float amount)
    {
        for (var i = 0; i < _nodes.Count; i++)
        {
            _translations[i] = Vector3.Lerp(sourceTranslations[i], targetTranslations[i], amount);
            _rotations[i] = Quaternion.Normalize(
                Quaternion.Slerp(sourceRotations[i], targetRotations[i], amount));
            _scales[i] = Vector3.Lerp(sourceScales[i], targetScales[i], amount);
            _localTransforms[i] = _nodes[i].UsesMatrix
                ? _nodes[i].LocalTransform
                : Matrix.CreateScale(_scales[i]) * Matrix.CreateFromQuaternion(_rotations[i]) *
                  Matrix.CreateTranslation(_translations[i]);
        }
        UpdateWorldTransforms();
    }

    private int ValidateNode(int nodeIndex)
    {
        if ((uint)nodeIndex >= (uint)_nodes.Count) throw new ArgumentOutOfRangeException(nameof(nodeIndex));
        return nodeIndex;
    }

    private static int[] BuildEvaluationOrder(IReadOnlyList<GltfNode> nodes)
    {
        var order = new int[nodes.Count];
        var write = 0;
        for (var i = 0; i < nodes.Count; i++)
            if (nodes[i].Parent < 0) order[write++] = i;
        for (var read = 0; read < write; read++)
        {
            var node = nodes[order[read]];
            for (var childIndex = 0; childIndex < node.Children.Count; childIndex++)
                order[write++] = node.Children[childIndex];
        }
        if (write != nodes.Count)
            throw new InvalidDataException("The glTF node hierarchy is cyclic or disconnected from its roots.");
        return order;
    }

    private static bool IsFinite(Matrix value) =>
        float.IsFinite(value.M11) && float.IsFinite(value.M12) &&
        float.IsFinite(value.M13) && float.IsFinite(value.M14) &&
        float.IsFinite(value.M21) && float.IsFinite(value.M22) &&
        float.IsFinite(value.M23) && float.IsFinite(value.M24) &&
        float.IsFinite(value.M31) && float.IsFinite(value.M32) &&
        float.IsFinite(value.M33) && float.IsFinite(value.M34) &&
        float.IsFinite(value.M41) && float.IsFinite(value.M42) &&
        float.IsFinite(value.M43) && float.IsFinite(value.M44);
}
