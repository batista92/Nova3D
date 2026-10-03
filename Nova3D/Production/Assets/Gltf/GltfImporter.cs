using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering;

namespace Nova3D.Production.Assets.Gltf;

/// <summary>Runtime glTF 2.0 geometry importer. GPU resources are created on the calling thread.</summary>
public sealed class GltfImporter
{
    private const int MaximumJointsPerPrimitive = 48;
    private sealed record Source(JsonDocument Json, string Directory, byte[]? GlbBuffer);
    private sealed record View(byte[] Buffer, int Offset, int Length, int? Stride);
    private sealed record Accessor(View View, int Offset, int Count, int ComponentType,
        string Type, bool Normalized);
    private sealed record NodeSource(string Name, int[] Children, Vector3 Translation,
        Quaternion Rotation, Vector3 Scale, Matrix LocalTransform, bool UsesMatrix,
        int? MeshIndex, int? SkinIndex);
    private sealed record AnimationChannelSource(int SamplerIndex, int NodeIndex,
        GltfAnimationTargetPath TargetPath);

    private readonly GraphicsDevice _device;

    public GltfImporter(GraphicsDevice device) =>
        _device = device ?? throw new ArgumentNullException(nameof(device));

    public GltfModel Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("glTF asset was not found.", fullPath);

        var source = Open(fullPath);
        using var json = source.Json;
        var root = source.Json.RootElement;
        ValidateAsset(root);
        var buffers = ReadBuffers(root, source.Directory, source.GlbBuffer);
        var views = ReadViews(root, buffers);
        var accessors = ReadAccessors(root, views);
        var materials = ReadMaterials(root);
        Texture2D[] textures = Array.Empty<Texture2D>();
        List<GltfPrimitive>? primitives = null;
        try
        {
            textures = ReadTextures(root, source.Directory, views);
            (primitives, var meshPrimitiveMap) = ReadMeshes(root, accessors);
            var nodes = ReadNodes(root);
            var skins = ReadSkins(root, accessors, nodes.Length);
            ValidateSkinnedMeshes(nodes, skins, primitives, meshPrimitiveMap);
            var animations = ReadAnimations(root, accessors, nodes);
            var instances = ReadInstances(root, nodes, meshPrimitiveMap);
            return new GltfModel(primitives.ToArray(), materials, instances.ToArray(), textures,
                nodes, skins, animations);
        }
        catch
        {
            if (primitives is not null)
                foreach (var primitive in primitives) primitive.Mesh.Dispose();
            foreach (var texture in textures.Distinct()) texture.Dispose();
            throw;
        }
    }

    private static Source Open(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
        if (!path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
            return new Source(JsonDocument.Parse(File.ReadAllBytes(path)), directory, null);

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x46546C67)
            throw new InvalidDataException("Invalid GLB header.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) != 2)
            throw new NotSupportedException("Only glTF 2.0 is supported.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)) != bytes.Length)
            throw new InvalidDataException("GLB length does not match its header.");

        byte[]? json = null;
        byte[]? binary = null;
        var cursor = 12;
        while (cursor + 8 <= bytes.Length)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor)));
            var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor + 4));
            cursor += 8;
            if (length < 0 || cursor + length > bytes.Length) throw new InvalidDataException("Invalid GLB chunk.");
            if (type == 0x4E4F534A) json = bytes.AsSpan(cursor, length).ToArray();
            else if (type == 0x004E4942) binary = bytes.AsSpan(cursor, length).ToArray();
            cursor += length;
        }
        if (json is null) throw new InvalidDataException("GLB has no JSON chunk.");
        return new Source(JsonDocument.Parse(json), directory, binary);
    }

    private static void ValidateAsset(JsonElement root)
    {
        if (!root.TryGetProperty("asset", out var asset) ||
            !asset.TryGetProperty("version", out var version) ||
            !version.GetString()!.StartsWith("2.", StringComparison.Ordinal))
            throw new NotSupportedException("Only glTF 2.x assets are supported.");
    }

    private static byte[][] ReadBuffers(JsonElement root, string directory, byte[]? glbBuffer)
    {
        if (!root.TryGetProperty("buffers", out var values)) return Array.Empty<byte[]>();
        var result = new byte[values.GetArrayLength()][];
        var index = 0;
        foreach (var buffer in values.EnumerateArray())
        {
            byte[] data;
            if (!buffer.TryGetProperty("uri", out var uriElement))
                data = glbBuffer ?? throw new InvalidDataException("A buffer has no URI or GLB BIN chunk.");
            else
                data = ReadUri(directory, uriElement.GetString()!);
            var expected = buffer.GetProperty("byteLength").GetInt32();
            if (data.Length < expected) throw new InvalidDataException($"Buffer {index} is shorter than declared.");
            result[index++] = data;
        }
        return result;
    }

    private static View[] ReadViews(JsonElement root, byte[][] buffers)
    {
        if (!root.TryGetProperty("bufferViews", out var values)) return Array.Empty<View>();
        var result = new View[values.GetArrayLength()];
        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            var buffer = buffers[value.GetProperty("buffer").GetInt32()];
            var offset = value.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0;
            var length = value.GetProperty("byteLength").GetInt32();
            int? stride = value.TryGetProperty("byteStride", out var s) ? s.GetInt32() : null;
            if (offset < 0 || length < 0 || offset + length > buffer.Length)
                throw new InvalidDataException($"Buffer view {index} is out of range.");
            result[index++] = new View(buffer, offset, length, stride);
        }
        return result;
    }

    private static Accessor[] ReadAccessors(JsonElement root, View[] views)
    {
        if (!root.TryGetProperty("accessors", out var values)) return Array.Empty<Accessor>();
        var result = new Accessor[values.GetArrayLength()];
        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            if (value.TryGetProperty("sparse", out _))
                throw new NotSupportedException("Sparse glTF accessors are not supported yet.");
            if (!value.TryGetProperty("bufferView", out var view))
                throw new NotSupportedException("Accessors without a buffer view are not supported yet.");
            var viewIndex = view.GetInt32();
            if ((uint)viewIndex >= (uint)views.Length)
                throw new InvalidDataException($"Accessor {index} references an invalid buffer view.");
            var accessorOffset = value.TryGetProperty("byteOffset", out var offset) ? offset.GetInt32() : 0;
            var count = value.GetProperty("count").GetInt32();
            if (accessorOffset < 0 || count < 0)
                throw new InvalidDataException($"Accessor {index} has a negative offset or count.");
            result[index++] = new Accessor(
                views[viewIndex],
                accessorOffset,
                count,
                value.GetProperty("componentType").GetInt32(),
                value.GetProperty("type").GetString()!,
                value.TryGetProperty("normalized", out var normalized) && normalized.GetBoolean());
        }
        return result;
    }

    private (List<GltfPrimitive>, List<int[]>) ReadMeshes(JsonElement root, Accessor[] accessors)
    {
        var result = new List<GltfPrimitive>();
        var mapping = new List<int[]>();
        if (!root.TryGetProperty("meshes", out var meshes)) return (result, mapping);
        try
        {
            foreach (var mesh in meshes.EnumerateArray())
            {
                var mapped = new List<int>();
                foreach (var primitive in mesh.GetProperty("primitives").EnumerateArray())
                {
                    var mode = primitive.TryGetProperty("mode", out var modeValue) ? modeValue.GetInt32() : 4;
                    if (mode != 4) throw new NotSupportedException("Only TRIANGLES glTF primitives are supported.");
                    var attributes = primitive.GetProperty("attributes");
                    if (attributes.TryGetProperty("JOINTS_1", out _) ||
                        attributes.TryGetProperty("WEIGHTS_1", out _))
                        throw new NotSupportedException("A second joint influence set is not supported in v0.2.");
                    var positions = ReadVector3(accessors[attributes.GetProperty("POSITION").GetInt32()]);
                    var normals = attributes.TryGetProperty("NORMAL", out var normalAccessor)
                        ? ReadVector3(accessors[normalAccessor.GetInt32()]) : null;
                    var uv = attributes.TryGetProperty("TEXCOORD_0", out var uvAccessor)
                        ? ReadVector2(accessors[uvAccessor.GetInt32()]) : null;
                    var hasJoints = attributes.TryGetProperty("JOINTS_0", out var jointAccessor);
                    var hasWeights = attributes.TryGetProperty("WEIGHTS_0", out var weightAccessor);
                    if (hasJoints != hasWeights)
                        throw new InvalidDataException("JOINTS_0 and WEIGHTS_0 must be present together.");
                    var joints = hasJoints ? ReadJointIndices(accessors[jointAccessor.GetInt32()]) : null;
                    var weights = hasWeights ? ReadWeights(accessors[weightAccessor.GetInt32()]) : null;
                    var indices = primitive.TryGetProperty("indices", out var indexAccessor)
                        ? ReadIndices(accessors[indexAccessor.GetInt32()])
                        : Enumerable.Range(0, positions.Length).ToArray();
                    if (indices.Length % 3 != 0) throw new InvalidDataException("Triangle index count is invalid.");
                    normals ??= GenerateNormals(positions, indices);
                    if (normals.Length != positions.Length || (uv is not null && uv.Length != positions.Length) ||
                        (joints is not null && joints.Length != positions.Length) ||
                        (weights is not null && weights.Length != positions.Length))
                        throw new InvalidDataException("Primitive attribute counts do not match POSITION.");
                    var material = primitive.TryGetProperty("material", out var materialValue)
                        ? materialValue.GetInt32() : (int?)null;
                    mapped.Add(result.Count);
                    var bounds = BoundingBox.CreateFromPoints(positions);
                    if (joints is null || weights is null)
                    {
                        var vertices = new GltfVertex[positions.Length];
                        for (var i = 0; i < vertices.Length; i++)
                            vertices[i] = new GltfVertex(positions[i], normals[i], uv?[i] ?? Vector2.Zero);
                        result.Add(new GltfPrimitive(Mesh.Create(_device, vertices, indices), material, bounds));
                    }
                    else
                    {
                        var palette = BuildLocalJointPalette(joints, weights, out var localJoints);
                        var vertices = new GltfSkinnedVertex[positions.Length];
                        for (var i = 0; i < vertices.Length; i++)
                            vertices[i] = new GltfSkinnedVertex(positions[i], normals[i],
                                uv?[i] ?? Vector2.Zero, localJoints[i], weights[i]);
                        var jointBounds = BuildJointBounds(positions, localJoints, weights, palette.Length);
                        result.Add(new GltfPrimitive(Mesh.Create(_device, vertices, indices), material,
                            bounds, palette, jointBounds));
                    }
                }
                mapping.Add(mapped.ToArray());
            }
            return (result, mapping);
        }
        catch
        {
            foreach (var primitive in result) primitive.Mesh.Dispose();
            throw;
        }
    }

    private static GltfMaterial[] ReadMaterials(JsonElement root)
    {
        if (!root.TryGetProperty("materials", out var materials)) return Array.Empty<GltfMaterial>();
        var result = new List<GltfMaterial>();
        foreach (var material in materials.EnumerateArray())
        {
            var pbr = material.TryGetProperty("pbrMetallicRoughness", out var value) ? value : default;
            var color = Vector4.One;
            if (pbr.ValueKind != JsonValueKind.Undefined && pbr.TryGetProperty("baseColorFactor", out var factor))
                color = ReadVector4(factor);
            int? texture = null;
            if (pbr.ValueKind != JsonValueKind.Undefined && pbr.TryGetProperty("baseColorTexture", out var textureInfo))
                texture = textureInfo.GetProperty("index").GetInt32();
            int? normalTexture = null;
            var normalScale = 1f;
            if (material.TryGetProperty("normalTexture", out var normalInfo))
            {
                normalTexture = normalInfo.GetProperty("index").GetInt32();
                if (normalInfo.TryGetProperty("scale", out var scale)) normalScale = scale.GetSingle();
            }
            int? metallicRoughnessTexture = null;
            if (pbr.ValueKind != JsonValueKind.Undefined && pbr.TryGetProperty("metallicRoughnessTexture", out var mrInfo))
                metallicRoughnessTexture = mrInfo.GetProperty("index").GetInt32();
            int? occlusionTexture = null;
            var occlusionStrength = 1f;
            if (material.TryGetProperty("occlusionTexture", out var occlusionInfo))
            {
                occlusionTexture = occlusionInfo.GetProperty("index").GetInt32();
                if (occlusionInfo.TryGetProperty("strength", out var strength)) occlusionStrength = strength.GetSingle();
            }
            result.Add(new GltfMaterial(
                material.TryGetProperty("name", out var name) ? name.GetString()! : $"Material {result.Count}",
                color,
                pbr.ValueKind != JsonValueKind.Undefined && pbr.TryGetProperty("metallicFactor", out var metallic) ? metallic.GetSingle() : 1f,
                pbr.ValueKind != JsonValueKind.Undefined && pbr.TryGetProperty("roughnessFactor", out var roughness) ? roughness.GetSingle() : 1f,
                texture,
                normalTexture,
                normalScale,
                metallicRoughnessTexture,
                occlusionTexture,
                occlusionStrength,
                material.TryGetProperty("doubleSided", out var doubleSided) && doubleSided.GetBoolean(),
                material.TryGetProperty("alphaMode", out var alphaMode) ? alphaMode.GetString()! : "OPAQUE",
                material.TryGetProperty("alphaCutoff", out var alphaCutoff) ? alphaCutoff.GetSingle() : 0.5f));
        }
        return result.ToArray();
    }

    private Texture2D[] ReadTextures(JsonElement root, string directory, View[] views)
    {
        if (!root.TryGetProperty("textures", out var textures)) return Array.Empty<Texture2D>();
        if (!root.TryGetProperty("images", out var images)) throw new InvalidDataException("Textures reference no images.");
        var decodedImages = new Texture2D[images.GetArrayLength()];
        try
        {
            var imageIndex = 0;
            foreach (var image in images.EnumerateArray())
            {
                byte[] bytes;
                if (image.TryGetProperty("uri", out var uri)) bytes = ReadUri(directory, uri.GetString()!);
                else if (image.TryGetProperty("bufferView", out var viewIndex))
                {
                    var view = views[viewIndex.GetInt32()];
                    bytes = view.Buffer.AsSpan(view.Offset, view.Length).ToArray();
                }
                else throw new InvalidDataException("Image has neither URI nor bufferView.");
                using var stream = new MemoryStream(bytes, writable: false);
                decodedImages[imageIndex++] = Texture2D.FromStream(_device, stream);
            }
            var result = new Texture2D[textures.GetArrayLength()];
            var textureIndex = 0;
            foreach (var texture in textures.EnumerateArray())
                result[textureIndex++] = decodedImages[texture.GetProperty("source").GetInt32()];
            foreach (var unreferenced in decodedImages.Where(image => image is not null && !result.Contains(image)))
                unreferenced.Dispose();
            return result;
        }
        catch
        {
            foreach (var texture in decodedImages.Where(texture => texture is not null).Distinct())
                texture.Dispose();
            throw;
        }
    }

    private static GltfNode[] ReadNodes(JsonElement root)
    {
        if (!root.TryGetProperty("nodes", out var nodes)) return Array.Empty<GltfNode>();
        var sources = new NodeSource[nodes.GetArrayLength()];
        var sourceIndex = 0;
        foreach (var node in nodes.EnumerateArray())
        {
            var hasMatrix = node.TryGetProperty("matrix", out var matrixValue);
            if (hasMatrix && (node.TryGetProperty("translation", out _) ||
                              node.TryGetProperty("rotation", out _) ||
                              node.TryGetProperty("scale", out _)))
                throw new InvalidDataException($"Node {sourceIndex} combines matrix and TRS transforms.");

            var translation = node.TryGetProperty("translation", out var translationValue)
                ? ReadVector3(translationValue) : Vector3.Zero;
            var rotation = node.TryGetProperty("rotation", out var rotationValue)
                ? NormalizeQuaternion(ReadQuaternion(rotationValue), $"node {sourceIndex}") : Quaternion.Identity;
            var scale = node.TryGetProperty("scale", out var scaleValue)
                ? ReadVector3(scaleValue) : Vector3.One;
            var localTransform = hasMatrix
                ? ReadMatrix(matrixValue)
                : Matrix.CreateScale(scale) * Matrix.CreateFromQuaternion(rotation) *
                  Matrix.CreateTranslation(translation);
            var children = node.TryGetProperty("children", out var childValues)
                ? childValues.EnumerateArray().Select(value => value.GetInt32()).ToArray()
                : Array.Empty<int>();
            sources[sourceIndex] = new NodeSource(
                node.TryGetProperty("name", out var name) ? name.GetString()! : $"Node {sourceIndex}",
                children, translation, rotation, scale, localTransform, hasMatrix,
                node.TryGetProperty("mesh", out var mesh) ? mesh.GetInt32() : null,
                node.TryGetProperty("skin", out var skin) ? skin.GetInt32() : null);
            sourceIndex++;
        }

        var parents = Enumerable.Repeat(-1, sources.Length).ToArray();
        for (var parent = 0; parent < sources.Length; parent++)
        {
            foreach (var child in sources[parent].Children)
            {
                if ((uint)child >= (uint)sources.Length)
                    throw new InvalidDataException($"Node {parent} references invalid child {child}.");
                if (parents[child] >= 0)
                    throw new InvalidDataException($"Node {child} has more than one parent.");
                parents[child] = parent;
            }
        }

        var visit = new byte[sources.Length];
        for (var i = 0; i < sources.Length; i++) ValidateHierarchy(i);

        var result = new GltfNode[sources.Length];
        for (var i = 0; i < sources.Length; i++)
        {
            var source = sources[i];
            result[i] = new GltfNode(source.Name, parents[i], source.Children, source.Translation,
                source.Rotation, source.Scale, source.LocalTransform, source.UsesMatrix,
                source.MeshIndex, source.SkinIndex);
        }
        return result;

        void ValidateHierarchy(int nodeIndex)
        {
            if (visit[nodeIndex] == 2) return;
            if (visit[nodeIndex] == 1)
                throw new InvalidDataException("The glTF node hierarchy contains a cycle.");
            visit[nodeIndex] = 1;
            foreach (var child in sources[nodeIndex].Children) ValidateHierarchy(child);
            visit[nodeIndex] = 2;
        }
    }

    private static GltfSkin[] ReadSkins(JsonElement root, Accessor[] accessors, int nodeCount)
    {
        if (!root.TryGetProperty("skins", out var skins)) return Array.Empty<GltfSkin>();
        var result = new GltfSkin[skins.GetArrayLength()];
        var skinIndex = 0;
        foreach (var skin in skins.EnumerateArray())
        {
            if (!skin.TryGetProperty("joints", out var jointValues) || jointValues.GetArrayLength() == 0)
                throw new InvalidDataException($"Skin {skinIndex} has no joints.");
            var joints = jointValues.EnumerateArray().Select(value => value.GetInt32()).ToArray();
            if (joints.Distinct().Count() != joints.Length)
                throw new InvalidDataException($"Skin {skinIndex} contains duplicate joints.");
            if (joints.Any(joint => (uint)joint >= (uint)nodeCount))
                throw new InvalidDataException($"Skin {skinIndex} references an invalid joint node.");

            var inverseBindMatrices = Enumerable.Repeat(Matrix.Identity, joints.Length).ToArray();
            if (skin.TryGetProperty("inverseBindMatrices", out var inverseAccessor))
            {
                var accessorIndex = inverseAccessor.GetInt32();
                if ((uint)accessorIndex >= (uint)accessors.Length)
                    throw new InvalidDataException($"Skin {skinIndex} references an invalid accessor.");
                inverseBindMatrices = ReadMatrices(accessors[accessorIndex]);
                if (inverseBindMatrices.Length != joints.Length)
                    throw new InvalidDataException($"Skin {skinIndex} inverse bind matrix count does not match its joints.");
            }

            int? skeletonRoot = skin.TryGetProperty("skeleton", out var skeleton)
                ? skeleton.GetInt32() : null;
            if (skeletonRoot is int rootNode && (uint)rootNode >= (uint)nodeCount)
                throw new InvalidDataException($"Skin {skinIndex} references an invalid skeleton root.");
            result[skinIndex] = new GltfSkin(
                skin.TryGetProperty("name", out var name) ? name.GetString()! : $"Skin {skinIndex}",
                joints, inverseBindMatrices, skeletonRoot);
            skinIndex++;
        }
        return result;
    }

    private static void ValidateSkinnedMeshes(GltfNode[] nodes, GltfSkin[] skins,
        List<GltfPrimitive> primitives, List<int[]> meshPrimitiveMap)
    {
        for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
        {
            var node = nodes[nodeIndex];
            if (node.MeshIndex is not int meshIndex) continue;
            if ((uint)meshIndex >= (uint)meshPrimitiveMap.Count)
                throw new InvalidDataException($"Node {nodeIndex} references invalid mesh {meshIndex}.");
            if (node.SkinIndex is int invalidSkin && (uint)invalidSkin >= (uint)skins.Length)
                throw new InvalidDataException($"Node {nodeIndex} references invalid skin {invalidSkin}.");

            foreach (var primitiveIndex in meshPrimitiveMap[meshIndex])
            {
                var primitive = primitives[primitiveIndex];
                if (primitive.IsSkinned && node.SkinIndex is null)
                    throw new InvalidDataException($"Node {nodeIndex} uses skinned vertices without a skin.");
                if (!primitive.IsSkinned && node.SkinIndex is not null)
                    throw new InvalidDataException($"Node {nodeIndex} applies a skin to a primitive without JOINTS_0/WEIGHTS_0.");
                if (node.SkinIndex is int skinIndex &&
                    primitive.JointPalette.Any(joint => (uint)joint >= (uint)skins[skinIndex].Joints.Count))
                    throw new InvalidDataException($"Primitive joint index exceeds skin {skinIndex}'s joint count.");
            }
        }
    }

    private static List<GltfInstance> ReadInstances(JsonElement root, GltfNode[] nodes,
        List<int[]> meshPrimitiveMap)
    {
        var result = new List<GltfInstance>();
        if (nodes.Length == 0) return result;
        var roots = new List<int>();
        if (root.TryGetProperty("scenes", out var scenes))
        {
            var sceneIndex = root.TryGetProperty("scene", out var selected) ? selected.GetInt32() : 0;
            if ((uint)sceneIndex >= (uint)scenes.GetArrayLength())
                throw new InvalidDataException($"Default scene index {sceneIndex} is invalid.");
            if (scenes[sceneIndex].TryGetProperty("nodes", out var sceneNodes))
                roots.AddRange(sceneNodes.EnumerateArray().Select(node => node.GetInt32()));
        }
        if (roots.Count == 0)
            roots.AddRange(Enumerable.Range(0, nodes.Length).Where(index => nodes[index].Parent < 0));
        if (roots.Any(index => (uint)index >= (uint)nodes.Length))
            throw new InvalidDataException("A scene references an invalid root node.");
        var visited = new bool[nodes.Length];
        foreach (var node in roots) VisitNode(node, Matrix.Identity);
        return result;

        void VisitNode(int nodeIndex, Matrix parent)
        {
            if (visited[nodeIndex])
                throw new InvalidDataException($"Node {nodeIndex} occurs more than once in the selected scene.");
            visited[nodeIndex] = true;
            var node = nodes[nodeIndex];
            var world = node.LocalTransform * parent;
            if (node.MeshIndex is int meshIndex)
                foreach (var primitiveIndex in meshPrimitiveMap[meshIndex])
                    result.Add(new GltfInstance(node.Name, primitiveIndex, world)
                    {
                        NodeIndex = nodeIndex,
                        SkinIndex = node.SkinIndex
                    });
            foreach (var child in node.Children) VisitNode(child, world);
        }
    }

    private static GltfAnimationClip[] ReadAnimations(JsonElement root, Accessor[] accessors,
        GltfNode[] nodes)
    {
        if (!root.TryGetProperty("animations", out var animations))
            return Array.Empty<GltfAnimationClip>();
        var result = new GltfAnimationClip[animations.GetArrayLength()];
        var animationIndex = 0;
        foreach (var animation in animations.EnumerateArray())
        {
            var samplerValues = animation.GetProperty("samplers").EnumerateArray().ToArray();
            var channels = new List<AnimationChannelSource>();
            foreach (var channel in animation.GetProperty("channels").EnumerateArray())
            {
                var samplerIndex = channel.GetProperty("sampler").GetInt32();
                if ((uint)samplerIndex >= (uint)samplerValues.Length)
                    throw new InvalidDataException($"Animation {animationIndex} references invalid sampler {samplerIndex}.");
                var target = channel.GetProperty("target");
                if (!target.TryGetProperty("node", out var targetNode))
                    throw new NotSupportedException("Animation channels without a target node are not supported.");
                var nodeIndex = targetNode.GetInt32();
                if ((uint)nodeIndex >= (uint)nodes.Length)
                    throw new InvalidDataException($"Animation {animationIndex} targets invalid node {nodeIndex}.");
                if (nodes[nodeIndex].UsesMatrix)
                    throw new InvalidDataException($"Animation {animationIndex} targets matrix node {nodeIndex}; animated nodes must use TRS.");
                var path = target.GetProperty("path").GetString() switch
                {
                    "translation" => GltfAnimationTargetPath.Translation,
                    "rotation" => GltfAnimationTargetPath.Rotation,
                    "scale" => GltfAnimationTargetPath.Scale,
                    "weights" => throw new NotSupportedException("Morph target animation is not supported."),
                    var value => throw new NotSupportedException($"Animation target path '{value}' is not supported.")
                };
                channels.Add(new AnimationChannelSource(samplerIndex, nodeIndex, path));
            }
            for (var i = 0; i < channels.Count; i++)
            for (var j = i + 1; j < channels.Count; j++)
                if (channels[i].NodeIndex == channels[j].NodeIndex &&
                    channels[i].TargetPath == channels[j].TargetPath)
                    throw new InvalidDataException(
                        $"Animation {animationIndex} targets the same node path more than once.");

            var parsedSamplers = new GltfAnimationSampler[samplerValues.Length];
            var duration = 0f;
            for (var samplerIndex = 0; samplerIndex < samplerValues.Length; samplerIndex++)
            {
                var sampler = samplerValues[samplerIndex];
                var referencedChannels = channels.Where(channel => channel.SamplerIndex == samplerIndex).ToArray();
                if (referencedChannels.Length == 0)
                    throw new InvalidDataException($"Animation {animationIndex} contains unused sampler {samplerIndex}.");
                var rotation = referencedChannels[0].TargetPath == GltfAnimationTargetPath.Rotation;
                if (referencedChannels.Any(channel =>
                        (channel.TargetPath == GltfAnimationTargetPath.Rotation) != rotation))
                    throw new InvalidDataException($"Animation sampler {samplerIndex} mixes VEC3 and rotation channels.");

                var inputIndex = sampler.GetProperty("input").GetInt32();
                var outputIndex = sampler.GetProperty("output").GetInt32();
                if ((uint)inputIndex >= (uint)accessors.Length || (uint)outputIndex >= (uint)accessors.Length)
                    throw new InvalidDataException($"Animation sampler {samplerIndex} references an invalid accessor.");
                var times = ReadAnimationTimes(accessors[inputIndex]);
                var values = rotation
                    ? ReadAnimationRotations(accessors[outputIndex])
                    : ReadAnimationVectors(accessors[outputIndex]);
                if (times.Length != values.Length)
                    throw new InvalidDataException($"Animation sampler {samplerIndex} input/output counts differ.");
                var interpolation = sampler.TryGetProperty("interpolation", out var interpolationValue)
                    ? interpolationValue.GetString() : "LINEAR";
                var interpolationMode = interpolation switch
                {
                    "LINEAR" => GltfAnimationInterpolation.Linear,
                    "STEP" => GltfAnimationInterpolation.Step,
                    "CUBICSPLINE" => throw new NotSupportedException("CUBICSPLINE animation is not supported in v0.2."),
                    _ => throw new NotSupportedException($"Animation interpolation '{interpolation}' is not supported.")
                };
                parsedSamplers[samplerIndex] = new GltfAnimationSampler(times, values, interpolationMode);
                duration = Math.Max(duration, times[^1]);
            }

            result[animationIndex] = new GltfAnimationClip(
                animation.TryGetProperty("name", out var name) ? name.GetString()! : $"Animation {animationIndex}",
                parsedSamplers,
                channels.Select(channel => new GltfAnimationChannel(
                    channel.SamplerIndex, channel.NodeIndex, channel.TargetPath)).ToArray(),
                duration);
            animationIndex++;
        }
        return result;
    }

    private static float[] ReadAnimationTimes(Accessor accessor)
    {
        Ensure(accessor, "SCALAR");
        if (accessor.ComponentType != 5126 || accessor.Normalized)
            throw new InvalidDataException("Animation input times must use non-normalized FLOAT components.");
        if (accessor.Count == 0) throw new InvalidDataException("Animation sampler contains no keyframes.");
        var result = new float[accessor.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = ReadComponent(accessor, i, 0);
            if (!float.IsFinite(result[i]) || result[i] < 0f || (i > 0 && result[i] <= result[i - 1]))
                throw new InvalidDataException("Animation input times must be finite, non-negative and strictly increasing.");
        }
        return result;
    }

    private static Vector4[] ReadAnimationVectors(Accessor accessor)
    {
        EnsureAnimationOutput(accessor, "VEC3");
        var result = new Vector4[accessor.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new Vector4(ReadComponent(accessor, i, 0), ReadComponent(accessor, i, 1),
                ReadComponent(accessor, i, 2), 0f);
            if (!IsFinite(result[i])) throw new InvalidDataException("Animation output contains a non-finite value.");
        }
        return result;
    }

    private static Vector4[] ReadAnimationRotations(Accessor accessor)
    {
        EnsureAnimationOutput(accessor, "VEC4");
        var result = new Vector4[accessor.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var value = new Quaternion(ReadComponent(accessor, i, 0), ReadComponent(accessor, i, 1),
                ReadComponent(accessor, i, 2), ReadComponent(accessor, i, 3));
            value = NormalizeQuaternion(value, "animation rotation");
            result[i] = new Vector4(value.X, value.Y, value.Z, value.W);
        }
        return result;
    }

    private static void EnsureAnimationOutput(Accessor accessor, string type)
    {
        Ensure(accessor, type);
        if (accessor.ComponentType != 5126 || accessor.Normalized)
            throw new InvalidDataException("Animation outputs must use non-normalized FLOAT components.");
    }

    private static Vector4[] ReadJointIndices(Accessor accessor)
    {
        Ensure(accessor, "VEC4");
        if (accessor.ComponentType is not (5121 or 5123) || accessor.Normalized)
            throw new InvalidDataException("JOINTS_0 must use non-normalized unsigned byte or unsigned short components.");
        var result = new Vector4[accessor.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = new Vector4(ReadComponent(accessor, i, 0), ReadComponent(accessor, i, 1),
                ReadComponent(accessor, i, 2), ReadComponent(accessor, i, 3));
        return result;
    }

    private static Vector4[] ReadWeights(Accessor accessor)
    {
        Ensure(accessor, "VEC4");
        var validFloat = accessor.ComponentType == 5126 && !accessor.Normalized;
        var validNormalizedInteger = accessor.Normalized && accessor.ComponentType is 5121 or 5123;
        if (!validFloat && !validNormalizedInteger)
            throw new InvalidDataException("WEIGHTS_0 must use FLOAT or normalized unsigned byte/short components.");
        var result = new Vector4[accessor.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var value = new Vector4(ReadComponent(accessor, i, 0), ReadComponent(accessor, i, 1),
                ReadComponent(accessor, i, 2), ReadComponent(accessor, i, 3));
            if (!IsFinite(value) || value.X < 0f || value.Y < 0f || value.Z < 0f || value.W < 0f)
                throw new InvalidDataException("WEIGHTS_0 contains invalid values.");
            var sum = value.X + value.Y + value.Z + value.W;
            if (sum <= 1e-8f) throw new InvalidDataException("A skinned vertex has no positive joint weight.");
            result[i] = value / sum;
        }
        return result;
    }

    private static int[] BuildLocalJointPalette(Vector4[] joints, Vector4[] weights,
        out Vector4[] localJoints)
    {
        var palette = new List<int>();
        var remap = new Dictionary<int, int>();
        localJoints = new Vector4[joints.Length];
        for (var vertex = 0; vertex < joints.Length; vertex++)
        {
            var mapped = Vector4.Zero;
            for (var influence = 0; influence < 4; influence++)
            {
                if (GetComponent(weights[vertex], influence) <= 0f) continue;
                var joint = checked((int)GetComponent(joints[vertex], influence));
                if (!remap.TryGetValue(joint, out var localIndex))
                {
                    if (palette.Count == MaximumJointsPerPrimitive)
                        throw new NotSupportedException(
                            $"A skinned primitive uses more than {MaximumJointsPerPrimitive} joints.");
                    localIndex = palette.Count;
                    palette.Add(joint);
                    remap.Add(joint, localIndex);
                }
                SetComponent(ref mapped, influence, localIndex);
            }
            localJoints[vertex] = mapped;
        }
        return palette.ToArray();
    }

    private static BoundingBox[] BuildJointBounds(Vector3[] positions, Vector4[] localJoints,
        Vector4[] weights, int paletteCount)
    {
        var minimum = Enumerable.Repeat(new Vector3(float.MaxValue), paletteCount).ToArray();
        var maximum = Enumerable.Repeat(new Vector3(float.MinValue), paletteCount).ToArray();
        var used = new bool[paletteCount];
        for (var vertex = 0; vertex < positions.Length; vertex++)
        for (var influence = 0; influence < 4; influence++)
        {
            if (GetComponent(weights[vertex], influence) <= 0f) continue;
            var joint = checked((int)GetComponent(localJoints[vertex], influence));
            minimum[joint] = Vector3.Min(minimum[joint], positions[vertex]);
            maximum[joint] = Vector3.Max(maximum[joint], positions[vertex]);
            used[joint] = true;
        }
        var result = new BoundingBox[paletteCount];
        for (var i = 0; i < result.Length; i++)
        {
            if (!used[i]) throw new InvalidDataException("A local joint palette entry has no influenced vertices.");
            result[i] = new BoundingBox(minimum[i], maximum[i]);
        }
        return result;
    }

    private static float GetComponent(Vector4 value, int component) => component switch
    {
        0 => value.X, 1 => value.Y, 2 => value.Z, 3 => value.W,
        _ => throw new ArgumentOutOfRangeException(nameof(component))
    };

    private static void SetComponent(ref Vector4 value, int component, float componentValue)
    {
        switch (component)
        {
            case 0: value.X = componentValue; break;
            case 1: value.Y = componentValue; break;
            case 2: value.Z = componentValue; break;
            case 3: value.W = componentValue; break;
            default: throw new ArgumentOutOfRangeException(nameof(component));
        }
    }

    private static Vector3[] ReadVector3(Accessor accessor)
    {
        Ensure(accessor, "VEC3");
        var result = new Vector3[accessor.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = new Vector3(ReadComponent(accessor, i, 0), ReadComponent(accessor, i, 1), ReadComponent(accessor, i, 2));
        return result;
    }

    private static Vector2[] ReadVector2(Accessor accessor)
    {
        Ensure(accessor, "VEC2");
        var result = new Vector2[accessor.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = new Vector2(ReadComponent(accessor, i, 0), ReadComponent(accessor, i, 1));
        return result;
    }

    private static Matrix[] ReadMatrices(Accessor accessor)
    {
        Ensure(accessor, "MAT4");
        if (accessor.ComponentType != 5126 || accessor.Normalized)
            throw new InvalidDataException("Inverse bind matrices must use non-normalized FLOAT components.");
        var result = new Matrix[accessor.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new Matrix(
                ReadComponent(accessor, i, 0), ReadComponent(accessor, i, 1),
                ReadComponent(accessor, i, 2), ReadComponent(accessor, i, 3),
                ReadComponent(accessor, i, 4), ReadComponent(accessor, i, 5),
                ReadComponent(accessor, i, 6), ReadComponent(accessor, i, 7),
                ReadComponent(accessor, i, 8), ReadComponent(accessor, i, 9),
                ReadComponent(accessor, i, 10), ReadComponent(accessor, i, 11),
                ReadComponent(accessor, i, 12), ReadComponent(accessor, i, 13),
                ReadComponent(accessor, i, 14), ReadComponent(accessor, i, 15));
            if (!IsFinite(result[i]))
                throw new InvalidDataException("An inverse bind matrix contains a non-finite value.");
        }
        return result;
    }

    private static int[] ReadIndices(Accessor accessor)
    {
        Ensure(accessor, "SCALAR");
        if (accessor.ComponentType is not (5121 or 5123 or 5125))
            throw new InvalidDataException("Indices must use unsigned byte, short or int components.");
        var result = new int[accessor.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var span = ComponentSpan(accessor, i, 0);
            result[i] = accessor.ComponentType switch
            {
                5121 => span[0],
                5123 => BinaryPrimitives.ReadUInt16LittleEndian(span),
                5125 => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(span)),
                _ => 0
            };
        }
        return result;
    }

    private static float ReadComponent(Accessor accessor, int element, int component)
    {
        var span = ComponentSpan(accessor, element, component);
        return accessor.ComponentType switch
        {
            5120 => accessor.Normalized ? Math.Max((sbyte)span[0] / 127f, -1f) : (sbyte)span[0],
            5121 => accessor.Normalized ? span[0] / 255f : span[0],
            5122 => accessor.Normalized ? Math.Max(BinaryPrimitives.ReadInt16LittleEndian(span) / 32767f, -1f) : BinaryPrimitives.ReadInt16LittleEndian(span),
            5123 => accessor.Normalized ? BinaryPrimitives.ReadUInt16LittleEndian(span) / 65535f : BinaryPrimitives.ReadUInt16LittleEndian(span),
            5125 => accessor.Normalized ? BinaryPrimitives.ReadUInt32LittleEndian(span) / (float)uint.MaxValue : BinaryPrimitives.ReadUInt32LittleEndian(span),
            5126 => BinaryPrimitives.ReadSingleLittleEndian(span),
            _ => throw new InvalidDataException($"Unsupported component type {accessor.ComponentType}.")
        };
    }

    private static ReadOnlySpan<byte> ComponentSpan(Accessor accessor, int element, int component)
    {
        var componentSize = ComponentSize(accessor.ComponentType);
        var componentCount = ComponentCount(accessor.Type);
        var stride = accessor.View.Stride ?? componentSize * componentCount;
        var offset = accessor.View.Offset + accessor.Offset + element * stride + component * componentSize;
        var end = accessor.View.Offset + accessor.View.Length;
        if (offset < accessor.View.Offset || offset + componentSize > end)
            throw new InvalidDataException("Accessor reads outside its buffer view.");
        return accessor.View.Buffer.AsSpan(offset, componentSize);
    }

    private static int ComponentSize(int type) => type switch
    {
        5120 or 5121 => 1,
        5122 or 5123 => 2,
        5125 or 5126 => 4,
        _ => throw new InvalidDataException($"Unsupported component type {type}.")
    };

    private static int ComponentCount(string type) => type switch
    {
        "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, "MAT4" => 16,
        _ => throw new NotSupportedException($"Accessor type {type} is not supported.")
    };

    private static void Ensure(Accessor accessor, string type)
    {
        if (accessor.Type != type) throw new InvalidDataException($"Expected {type}, got {accessor.Type}.");
    }

    private static Vector3[] GenerateNormals(Vector3[] positions, int[] indices)
    {
        var normals = new Vector3[positions.Length];
        for (var i = 0; i < indices.Length; i += 3)
        {
            var a = indices[i]; var b = indices[i + 1]; var c = indices[i + 2];
            var face = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            normals[a] += face; normals[b] += face; normals[c] += face;
        }
        for (var i = 0; i < normals.Length; i++)
            normals[i] = normals[i].LengthSquared() > 1e-12f ? Vector3.Normalize(normals[i]) : Vector3.Up;
        return normals;
    }

    private static byte[] ReadUri(string directory, string uri)
    {
        if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = uri.IndexOf(',');
            if (comma < 0 || !uri[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Only base64 data URIs are supported.");
            return Convert.FromBase64String(uri[(comma + 1)..]);
        }
        var decoded = Uri.UnescapeDataString(uri).Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(directory, decoded));
        return File.ReadAllBytes(fullPath);
    }

    private static Vector3 ReadVector3(JsonElement value)
    {
        var values = value.EnumerateArray().Select(item => item.GetSingle()).ToArray();
        if (values.Length != 3) throw new InvalidDataException("Expected a three-component vector.");
        return new Vector3(values[0], values[1], values[2]);
    }

    private static Vector4 ReadVector4(JsonElement value)
    {
        var values = value.EnumerateArray().Select(item => item.GetSingle()).ToArray();
        if (values.Length != 4) throw new InvalidDataException("Expected a four-component vector.");
        return new Vector4(values[0], values[1], values[2], values[3]);
    }

    private static Quaternion ReadQuaternion(JsonElement value)
    {
        var values = value.EnumerateArray().Select(item => item.GetSingle()).ToArray();
        if (values.Length != 4) throw new InvalidDataException("Expected a four-component quaternion.");
        return new Quaternion(values[0], values[1], values[2], values[3]);
    }

    private static Matrix ReadMatrix(JsonElement value)
    {
        var values = value.EnumerateArray().Select(item => item.GetSingle()).ToArray();
        if (values.Length != 16) throw new InvalidDataException("Expected a 4x4 matrix.");
        var result = new Matrix(values[0], values[1], values[2], values[3],
            values[4], values[5], values[6], values[7], values[8], values[9],
            values[10], values[11], values[12], values[13], values[14], values[15]);
        if (!IsFinite(result)) throw new InvalidDataException("A node matrix contains a non-finite value.");
        return result;
    }

    private static Quaternion NormalizeQuaternion(Quaternion value, string source)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) ||
            !float.IsFinite(value.Z) || !float.IsFinite(value.W) || value.LengthSquared() <= 1e-12f)
            throw new InvalidDataException($"Invalid quaternion in {source}.");
        return Quaternion.Normalize(value);
    }

    private static bool IsFinite(Vector4 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) && float.IsFinite(value.W);

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
