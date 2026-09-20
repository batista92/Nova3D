using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering;

namespace Nova3D.Production.Assets.Gltf;

/// <summary>Runtime glTF 2.0 geometry importer. GPU resources are created on the calling thread.</summary>
public sealed class GltfImporter
{
    private sealed record Source(JsonDocument Json, string Directory, byte[]? GlbBuffer);
    private sealed record View(byte[] Buffer, int Offset, int Length, int? Stride);
    private sealed record Accessor(View View, int Offset, int Count, int ComponentType,
        string Type, bool Normalized);

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
            var instances = ReadInstances(root, meshPrimitiveMap);
            return new GltfModel(primitives.ToArray(), materials, instances.ToArray(), textures);
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
            result[index++] = new Accessor(
                views[view.GetInt32()],
                value.TryGetProperty("byteOffset", out var offset) ? offset.GetInt32() : 0,
                value.GetProperty("count").GetInt32(),
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
                    var positions = ReadVector3(accessors[attributes.GetProperty("POSITION").GetInt32()]);
                    var normals = attributes.TryGetProperty("NORMAL", out var normalAccessor)
                        ? ReadVector3(accessors[normalAccessor.GetInt32()]) : null;
                    var uv = attributes.TryGetProperty("TEXCOORD_0", out var uvAccessor)
                        ? ReadVector2(accessors[uvAccessor.GetInt32()]) : null;
                    var indices = primitive.TryGetProperty("indices", out var indexAccessor)
                        ? ReadIndices(accessors[indexAccessor.GetInt32()])
                        : Enumerable.Range(0, positions.Length).ToArray();
                    if (indices.Length % 3 != 0) throw new InvalidDataException("Triangle index count is invalid.");
                    normals ??= GenerateNormals(positions, indices);
                    if (normals.Length != positions.Length || (uv is not null && uv.Length != positions.Length))
                        throw new InvalidDataException("Primitive attribute counts do not match POSITION.");
                    var vertices = new GltfVertex[positions.Length];
                    for (var i = 0; i < vertices.Length; i++)
                        vertices[i] = new GltfVertex(positions[i], normals[i], uv?[i] ?? Vector2.Zero);
                    var material = primitive.TryGetProperty("material", out var materialValue)
                        ? materialValue.GetInt32() : (int?)null;
                    mapped.Add(result.Count);
                    result.Add(new GltfPrimitive(Mesh.Create(_device, vertices, indices), material,
                        BoundingBox.CreateFromPoints(positions)));
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

    private static List<GltfInstance> ReadInstances(JsonElement root, List<int[]> meshPrimitiveMap)
    {
        var result = new List<GltfInstance>();
        if (!root.TryGetProperty("nodes", out var nodes)) return result;
        var nodeArray = nodes.EnumerateArray().ToArray();
        var roots = new List<int>();
        if (root.TryGetProperty("scenes", out var scenes))
        {
            var sceneIndex = root.TryGetProperty("scene", out var selected) ? selected.GetInt32() : 0;
            if (sceneIndex < scenes.GetArrayLength() && scenes[sceneIndex].TryGetProperty("nodes", out var sceneNodes))
                roots.AddRange(sceneNodes.EnumerateArray().Select(node => node.GetInt32()));
        }
        if (roots.Count == 0)
        {
            var children = new HashSet<int>();
            foreach (var node in nodeArray)
                if (node.TryGetProperty("children", out var values))
                    foreach (var child in values.EnumerateArray()) children.Add(child.GetInt32());
            roots.AddRange(Enumerable.Range(0, nodeArray.Length).Where(index => !children.Contains(index)));
        }
        foreach (var node in roots) VisitNode(node, Matrix.Identity);
        return result;

        void VisitNode(int nodeIndex, Matrix parent)
        {
            var node = nodeArray[nodeIndex];
            var world = ReadTransform(node) * parent;
            var name = node.TryGetProperty("name", out var nodeName) ? nodeName.GetString()! : $"Node {nodeIndex}";
            if (node.TryGetProperty("mesh", out var mesh))
                foreach (var primitiveIndex in meshPrimitiveMap[mesh.GetInt32()])
                    result.Add(new GltfInstance(name, primitiveIndex, world));
            if (node.TryGetProperty("children", out var nodeChildren))
                foreach (var child in nodeChildren.EnumerateArray()) VisitNode(child.GetInt32(), world);
        }
    }

    private static Matrix ReadTransform(JsonElement node)
    {
        if (node.TryGetProperty("matrix", out var matrix))
        {
            var v = matrix.EnumerateArray().Select(value => value.GetSingle()).ToArray();
            return new Matrix(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7],
                v[8], v[9], v[10], v[11], v[12], v[13], v[14], v[15]);
        }
        var scale = node.TryGetProperty("scale", out var s) ? ReadVector3(s) : Vector3.One;
        var translation = node.TryGetProperty("translation", out var t) ? ReadVector3(t) : Vector3.Zero;
        var rotation = node.TryGetProperty("rotation", out var r) ? ReadQuaternion(r) : Quaternion.Identity;
        return Matrix.CreateScale(scale) * Matrix.CreateFromQuaternion(rotation) * Matrix.CreateTranslation(translation);
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
        "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4,
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
        return new Vector3(values[0], values[1], values[2]);
    }

    private static Vector4 ReadVector4(JsonElement value)
    {
        var values = value.EnumerateArray().Select(item => item.GetSingle()).ToArray();
        return new Vector4(values[0], values[1], values[2], values[3]);
    }

    private static Quaternion ReadQuaternion(JsonElement value)
    {
        var values = value.EnumerateArray().Select(item => item.GetSingle()).ToArray();
        return new Quaternion(values[0], values[1], values[2], values[3]);
    }
}
