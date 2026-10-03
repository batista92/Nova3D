using System.Buffers.Binary;
using System.Text.Json;

internal static class InspectCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is ["--help"] or ["-h"])
        {
            WriteHelp(output);
            return CliExitCodes.Success;
        }

        if (args.Length != 1)
        {
            error.WriteLine("Invalid inspect usage: exactly one GLB or scene JSON path is required.");
            error.WriteLine("ACTION | Run 'nova3d inspect --help' for usage.");
            return CliExitCodes.UsageError;
        }

        string path;
        try
        {
            path = Path.GetFullPath(args[0]);
        }
        catch (Exception exception)
        {
            error.WriteLine($"INSPECTION FAIL | file | invalid path: {OneLine(exception.Message)}");
            error.WriteLine("ACTION | Pass an existing .glb or .scene.json path.");
            return CliExitCodes.CommandFailed;
        }

        if (path.EndsWith(".scene.json", StringComparison.OrdinalIgnoreCase))
        {
            return SceneInspectCommand.Run(path, output);
        }

        if (!string.Equals(Path.GetExtension(path), ".glb", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine($"INSPECTION FAIL | file | expected a .glb or .scene.json file: {path}");
            error.WriteLine("ACTION | Export a binary glTF .glb or pass a Nova3D .scene.json document.");
            return CliExitCodes.CommandFailed;
        }

        if (!File.Exists(path))
        {
            error.WriteLine($"INSPECTION FAIL | file | model does not exist: {path}");
            error.WriteLine("ACTION | Correct the model path and run inspection again.");
            return CliExitCodes.CommandFailed;
        }

        GlbInspectionReport report = new(output);
        output.WriteLine("Nova3D GLB inspection");
        try
        {
            Inspect(path, report);
        }
        catch (Exception exception)
        {
            report.Fail("model", OneLine(exception.Message));
        }

        return report.Complete();
    }

    private static void Inspect(string path, GlbInspectionReport report)
    {
        byte[] bytes = File.ReadAllBytes(path);
        report.Pass("file", $"{path} | {bytes.Length} bytes");
        if (bytes.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x46546C67)
        {
            throw new InvalidDataException("invalid GLB header; export the model as binary glTF 2.0.");
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        if (version != 2)
        {
            throw new InvalidDataException($"GLB version {version} is unsupported; export glTF 2.0.");
        }

        uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
        if (declaredLength != bytes.Length)
        {
            throw new InvalidDataException(
                $"header declares {declaredLength} bytes but the file contains {bytes.Length}.");
        }

        ReadOnlyMemory<byte>? jsonChunk = null;
        ReadOnlyMemory<byte>? binaryChunk = null;
        int chunkCount = 0;
        int cursor = 12;
        while (cursor < bytes.Length)
        {
            if (cursor + 8 > bytes.Length)
            {
                throw new InvalidDataException("truncated GLB chunk header.");
            }

            int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor)));
            uint type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor + 4));
            cursor += 8;
            if (length < 0 || cursor + length > bytes.Length)
            {
                throw new InvalidDataException($"chunk {chunkCount} extends beyond the file.");
            }

            if ((length & 3) != 0)
            {
                report.Fail("container", $"chunk {chunkCount} length {length} is not 4-byte aligned.");
            }

            ReadOnlyMemory<byte> data = bytes.AsMemory(cursor, length);
            if (type == 0x4E4F534A)
            {
                if (jsonChunk is not null)
                {
                    report.Fail("container", "more than one JSON chunk is present.");
                }
                else if (chunkCount != 0)
                {
                    report.Fail("container", "the JSON chunk must be first.");
                }
                else
                {
                    jsonChunk = data;
                }
            }
            else if (type == 0x004E4942)
            {
                if (binaryChunk is not null)
                {
                    report.Fail("container", "more than one BIN chunk is present.");
                }
                else
                {
                    binaryChunk = data;
                }
            }
            else
            {
                report.Warn("container", $"unknown chunk type 0x{type:X8} was ignored.");
            }

            cursor += length;
            chunkCount++;
        }

        if (jsonChunk is null)
        {
            throw new InvalidDataException("GLB has no JSON chunk.");
        }

        report.Pass("container", $"GLB 2.0 | chunks {chunkCount} | BIN {(binaryChunk is null ? "absent" : $"{binaryChunk.Value.Length} bytes")}");
        using JsonDocument document = JsonDocument.Parse(jsonChunk.Value);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("asset", out JsonElement asset) ||
            !asset.TryGetProperty("version", out JsonElement assetVersion) ||
            assetVersion.ValueKind != JsonValueKind.String ||
            !assetVersion.GetString()!.StartsWith("2.", StringComparison.Ordinal))
        {
            report.Fail("asset", "asset.version must identify glTF 2.x.");
        }
        else
        {
            string generator = asset.TryGetProperty("generator", out JsonElement generatorValue) &&
                generatorValue.ValueKind == JsonValueKind.String
                ? $" | generator {generatorValue.GetString()}"
                : string.Empty;
            report.Pass("asset", $"glTF {assetVersion.GetString()}{generator}");
        }

        ValidateExtensions(root, report);
        ValidateBuffers(root, binaryChunk, Path.GetDirectoryName(path)!, report);
        ValidateAccessors(root, report);
        (int primitiveCount, int skinnedPrimitiveCount) = ValidateMeshes(root, report);
        ValidateMaterials(root, report);
        ValidateAnimations(root, report);
        ValidateSkins(root, report);

        report.Info(
            "content",
            $"scenes {Count(root, "scenes")} | nodes {Count(root, "nodes")} | " +
            $"meshes {Count(root, "meshes")} | primitives {primitiveCount} | skinned {skinnedPrimitiveCount}");
        report.Info(
            "resources",
            $"materials {Count(root, "materials")} | textures {Count(root, "textures")} | " +
            $"images {Count(root, "images")} | skins {Count(root, "skins")} | animations {Count(root, "animations")}");
    }

    private static void ValidateExtensions(JsonElement root, GlbInspectionReport report)
    {
        if (root.TryGetProperty("extensionsRequired", out JsonElement required))
        {
            foreach (JsonElement extension in required.EnumerateArray())
            {
                report.Fail("extension", $"required extension '{extension.GetString()}' is not supported by Nova3D.");
            }
        }

        if (root.TryGetProperty("extensionsUsed", out JsonElement used))
        {
            foreach (JsonElement extension in used.EnumerateArray())
            {
                string? name = extension.GetString();
                bool isRequired = root.TryGetProperty("extensionsRequired", out JsonElement requiredValues) &&
                    requiredValues.EnumerateArray().Any(value => value.GetString() == name);
                if (!isRequired)
                {
                    report.Warn("extension", $"extension '{name}' is not interpreted by Nova3D.");
                }
            }
        }
    }

    private static void ValidateBuffers(
        JsonElement root,
        ReadOnlyMemory<byte>? binaryChunk,
        string directory,
        GlbInspectionReport report)
    {
        int embeddedBuffers = 0;
        ValidateExternalResources(root, "images", directory, report);
        if (!root.TryGetProperty("buffers", out JsonElement buffers))
        {
            return;
        }

        int index = 0;
        foreach (JsonElement buffer in buffers.EnumerateArray())
        {
            long expectedLength = buffer.TryGetProperty("byteLength", out JsonElement lengthValue)
                ? lengthValue.GetInt64()
                : -1;
            if (expectedLength < 0)
            {
                report.Fail("buffer", $"buffer {index} has no valid byteLength.");
            }

            if (!buffer.TryGetProperty("uri", out JsonElement uriValue))
            {
                embeddedBuffers++;
                if (binaryChunk is null)
                {
                    report.Fail("buffer", $"buffer {index} has no URI and the GLB has no BIN chunk.");
                }
                else if (expectedLength > binaryChunk.Value.Length)
                {
                    report.Fail("buffer", $"buffer {index} requires {expectedLength} bytes but BIN has {binaryChunk.Value.Length}.");
                }
            }
            else
            {
                ValidateUri("buffer", index, uriValue.GetString(), expectedLength, directory, report);
            }

            index++;
        }

        if (embeddedBuffers > 1)
        {
            report.Fail("buffer", "a GLB may contain only one buffer without a URI.");
        }
    }

    private static void ValidateExternalResources(
        JsonElement root,
        string property,
        string directory,
        GlbInspectionReport report)
    {
        if (!root.TryGetProperty(property, out JsonElement values))
        {
            return;
        }

        int index = 0;
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.TryGetProperty("uri", out JsonElement uri))
            {
                ValidateUri(property[..^1], index, uri.GetString(), null, directory, report);
            }
            index++;
        }
    }

    private static void ValidateUri(
        string kind,
        int index,
        string? uri,
        long? expectedLength,
        string directory,
        GlbInspectionReport report)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            report.Fail(kind, $"{kind} {index} has an empty URI.");
            return;
        }

        if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            int comma = uri.IndexOf(',');
            if (comma < 0 || !uri[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            {
                report.Fail(kind, $"{kind} {index} must use a base64 data URI.");
                return;
            }

            try
            {
                byte[] data = Convert.FromBase64String(uri[(comma + 1)..]);
                if (expectedLength.HasValue && data.LongLength < expectedLength.Value)
                {
                    report.Fail(kind, $"{kind} {index} data URI is shorter than byteLength.");
                }
            }
            catch (FormatException)
            {
                report.Fail(kind, $"{kind} {index} contains invalid base64 data.");
            }
            return;
        }

        string decoded = Uri.UnescapeDataString(uri).Replace('/', Path.DirectorySeparatorChar);
        string resourcePath = Path.GetFullPath(Path.Combine(directory, decoded));
        if (!File.Exists(resourcePath))
        {
            report.Fail(kind, $"{kind} {index} is missing: {resourcePath}");
            return;
        }

        long actualLength = new FileInfo(resourcePath).Length;
        if (expectedLength.HasValue && actualLength < expectedLength.Value)
        {
            report.Fail(kind, $"{kind} {index} has {actualLength} bytes; expected at least {expectedLength.Value}.");
        }
        else
        {
            report.Pass(kind, $"{kind} {index} external resource found: {resourcePath}");
        }
    }

    private static void ValidateAccessors(JsonElement root, GlbInspectionReport report)
    {
        if (!root.TryGetProperty("accessors", out JsonElement accessors))
        {
            return;
        }

        int index = 0;
        foreach (JsonElement accessor in accessors.EnumerateArray())
        {
            if (accessor.TryGetProperty("sparse", out _))
            {
                report.Fail("accessor", $"accessor {index} is sparse; Nova3D does not support sparse accessors.");
            }
            if (!accessor.TryGetProperty("bufferView", out _))
            {
                report.Fail("accessor", $"accessor {index} has no bufferView; this is not supported.");
            }
            index++;
        }
    }

    private static (int Primitives, int Skinned) ValidateMeshes(JsonElement root, GlbInspectionReport report)
    {
        if (!root.TryGetProperty("meshes", out JsonElement meshes))
        {
            return (0, 0);
        }

        HashSet<string> warnedAttributes = new(StringComparer.Ordinal);
        int primitiveCount = 0;
        int skinnedCount = 0;
        int meshIndex = 0;
        foreach (JsonElement mesh in meshes.EnumerateArray())
        {
            if (!mesh.TryGetProperty("primitives", out JsonElement primitives) ||
                primitives.ValueKind != JsonValueKind.Array)
            {
                report.Fail("mesh", $"mesh {meshIndex} has no primitives array.");
                meshIndex++;
                continue;
            }

            foreach (JsonElement primitive in primitives.EnumerateArray())
            {
                int mode = primitive.TryGetProperty("mode", out JsonElement modeValue)
                    ? modeValue.GetInt32()
                    : 4;
                if (mode != 4)
                {
                    report.Fail("primitive", $"mesh {meshIndex} primitive {primitiveCount} uses mode {mode}; only TRIANGLES (4) is supported.");
                }

                if (!primitive.TryGetProperty("attributes", out JsonElement attributes) ||
                    attributes.ValueKind != JsonValueKind.Object)
                {
                    report.Fail("primitive", $"mesh {meshIndex} primitive {primitiveCount} has no attributes object.");
                }
                else
                {
                    if (!attributes.TryGetProperty("POSITION", out _))
                    {
                        report.Fail("primitive", $"mesh {meshIndex} primitive {primitiveCount} has no POSITION.");
                    }

                    bool joints = attributes.TryGetProperty("JOINTS_0", out _);
                    bool weights = attributes.TryGetProperty("WEIGHTS_0", out _);
                    if (joints != weights)
                    {
                        report.Fail("primitive", $"mesh {meshIndex} primitive {primitiveCount} must contain JOINTS_0 and WEIGHTS_0 together.");
                    }
                    if (joints && weights)
                    {
                        skinnedCount++;
                    }
                    if (attributes.TryGetProperty("JOINTS_1", out _) ||
                        attributes.TryGetProperty("WEIGHTS_1", out _))
                    {
                        report.Fail("primitive", $"mesh {meshIndex} primitive {primitiveCount} uses a second influence set.");
                    }

                    foreach (JsonProperty attribute in attributes.EnumerateObject())
                    {
                        if (attribute.Name is not ("POSITION" or "NORMAL" or "TEXCOORD_0" or "JOINTS_0" or "WEIGHTS_0") &&
                            warnedAttributes.Add(attribute.Name))
                        {
                            report.Warn("attribute", $"'{attribute.Name}' is ignored by Nova3D.");
                        }
                    }
                }

                if (primitive.TryGetProperty("targets", out _))
                {
                    report.Fail("primitive", $"mesh {meshIndex} primitive {primitiveCount} uses unsupported morph targets.");
                }
                if (primitive.TryGetProperty("extensions", out JsonElement primitiveExtensions) &&
                    primitiveExtensions.TryGetProperty("KHR_draco_mesh_compression", out _))
                {
                    report.Fail("primitive", $"mesh {meshIndex} primitive {primitiveCount} uses unsupported Draco compression.");
                }
                primitiveCount++;
            }
            meshIndex++;
        }

        return (primitiveCount, skinnedCount);
    }

    private static void ValidateMaterials(JsonElement root, GlbInspectionReport report)
    {
        if (!root.TryGetProperty("materials", out JsonElement materials))
        {
            return;
        }

        int index = 0;
        foreach (JsonElement material in materials.EnumerateArray())
        {
            if (material.TryGetProperty("alphaMode", out JsonElement alphaMode) &&
                string.Equals(alphaMode.GetString(), "BLEND", StringComparison.Ordinal))
            {
                report.Warn("material", $"material {index} uses alpha BLEND, currently rendered opaque.");
            }
            index++;
        }
    }

    private static void ValidateAnimations(JsonElement root, GlbInspectionReport report)
    {
        if (!root.TryGetProperty("animations", out JsonElement animations))
        {
            return;
        }

        int animationIndex = 0;
        foreach (JsonElement animation in animations.EnumerateArray())
        {
            if (animation.TryGetProperty("samplers", out JsonElement samplers))
            {
                int samplerIndex = 0;
                foreach (JsonElement sampler in samplers.EnumerateArray())
                {
                    string interpolation = sampler.TryGetProperty("interpolation", out JsonElement value)
                        ? value.GetString() ?? string.Empty
                        : "LINEAR";
                    if (interpolation is not ("LINEAR" or "STEP"))
                    {
                        report.Fail("animation", $"animation {animationIndex} sampler {samplerIndex} uses unsupported {interpolation} interpolation.");
                    }
                    samplerIndex++;
                }
            }

            if (animation.TryGetProperty("channels", out JsonElement channels))
            {
                foreach (JsonElement channel in channels.EnumerateArray())
                {
                    if (channel.TryGetProperty("target", out JsonElement target) &&
                        target.TryGetProperty("path", out JsonElement path) && path.GetString() == "weights")
                    {
                        report.Fail("animation", $"animation {animationIndex} targets unsupported morph weights.");
                    }
                }
            }
            animationIndex++;
        }
    }

    private static void ValidateSkins(JsonElement root, GlbInspectionReport report)
    {
        if (!root.TryGetProperty("skins", out JsonElement skins))
        {
            return;
        }

        int index = 0;
        foreach (JsonElement skin in skins.EnumerateArray())
        {
            int joints = skin.TryGetProperty("joints", out JsonElement values)
                ? values.GetArrayLength()
                : 0;
            if (joints > 48)
            {
                report.Warn("skin", $"skin {index} has {joints} joints; each rendered primitive may reference at most 48.");
            }
            index++;
        }
    }

    private static int Count(JsonElement root, string property) =>
        root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("Usage: nova3d inspect <MODEL.glb|SCENE.scene.json>");
        output.WriteLine();
        output.WriteLine("Validates a GLB 2.0 model or Nova3D scene without a GPU.");
    }

    private static string OneLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}

internal sealed class GlbInspectionReport
{
    private readonly TextWriter output;
    private int warnings;
    private int errors;

    public GlbInspectionReport(TextWriter output) => this.output = output;

    public void Pass(string name, string message) => output.WriteLine($"PASS {name} | {message}");

    public void Info(string name, string message) => output.WriteLine($"INFO {name} | {message}");

    public void Warn(string name, string message)
    {
        warnings++;
        output.WriteLine($"WARN {name} | {message}");
    }

    public void Fail(string name, string message)
    {
        errors++;
        output.WriteLine($"FAIL {name} | {message}");
    }

    public int Complete()
    {
        string outcome = errors == 0 ? "PASS" : "FAIL";
        output.WriteLine($"INSPECTION {outcome} | warnings {warnings} | errors {errors}");
        if (errors > 0)
        {
            output.WriteLine("ACTION | Correct the reported GLB compatibility or resource errors, then inspect again.");
        }
        return errors == 0 ? CliExitCodes.Success : CliExitCodes.CommandFailed;
    }
}
