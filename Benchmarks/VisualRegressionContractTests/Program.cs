using System.Text.Json;
using Nova3D.Production.VisualTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

string root = Path.Combine(Path.GetTempPath(), "nova3d-visual-regression-" + Guid.NewGuid().ToString("N"));
try
{
    string captureDirectory = Path.Combine(root, "captures");
    string baselineRoot = Path.Combine(root, "baselines");
    Directory.CreateDirectory(captureDirectory);
    string capturePath = Path.Combine(captureDirectory, "contract-scene.png");
    WriteCapture(capturePath, new[]
    {
        new Rgba32(10, 20, 30), new Rgba32(40, 50, 60),
        new Rgba32(70, 80, 90), new Rgba32(100, 110, 120)
    });

    CommandResult missingAccept = Run(
        "visual", "update-baseline", capturePath, "--baseline-root", baselineRoot);
    Require(missingAccept.ExitCode == 2 && missingAccept.Error.Contains("--accept"), "explicit update guard");

    CommandResult update = Run(
        "visual", "update-baseline", capturePath, "--baseline-root", baselineRoot,
        "--accept", "--channel-tolerance", "2", "--max-different-pixel-ratio", "0");
    Require(update.ExitCode == 0 && update.Output.Contains("VISUAL PASS"), "baseline update");
    string versionedBaseline = Path.Combine(baselineRoot, "contract-scene", "v1");
    Require(File.Exists(Path.Combine(versionedBaseline, "baseline.json")), "versioned scene manifest");

    CommandResult exact = Run("visual", "compare", capturePath, "--baseline-root", baselineRoot);
    Require(exact.ExitCode == 0 && exact.Output.Contains("different 0"), "exact comparison");

    WriteCapture(capturePath, new[]
    {
        new Rgba32(10, 20, 30), new Rgba32(40, 50, 60),
        new Rgba32(70, 80, 90), new Rgba32(120, 110, 120)
    });
    string diffPath = Path.Combine(root, "evidence", "diff.png");
    CommandResult regression = Run(
        "visual", "compare", capturePath, "--baseline-root", baselineRoot, "--diff", diffPath);
    Require(regression.ExitCode == 1 && regression.Error.Contains("VISUAL FAIL"), "regression failure");
    Require(File.Exists(diffPath), "visual diff");

    CommandResult overridePass = Run(
        "visual", "compare", capturePath, "--baseline-root", baselineRoot,
        "--channel-tolerance", "20", "--max-different-pixel-ratio", "0");
    Require(overridePass.ExitCode == 0, "configured tolerance override");

    CommandResult json = Run(
        "--format", "json", "visual", "compare", capturePath, "--baseline-root", baselineRoot,
        "--channel-tolerance", "20", "--max-different-pixel-ratio", "0");
    using JsonDocument document = JsonDocument.Parse(json.Output);
    Require(json.ExitCode == 0 && document.RootElement.GetProperty("status").GetString() == "pass", "JSON contract");

    File.AppendAllText(Path.Combine(versionedBaseline, "baseline.png"), "tamper");
    CommandResult tampered = Run("visual", "compare", capturePath, "--baseline-root", baselineRoot);
    Require(tampered.ExitCode == 1 && tampered.Error.Contains("hash does not match"), "tampered baseline guard");

    Console.WriteLine("Visual regression G8.2 PASS | versioned baseline | explicit update | tolerance | metrics | diff | hash | JSON");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

void WriteCapture(string path, Rgba32[] pixels)
{
    using (Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixels, 2, 2))
        image.SaveAsPng(path);
    VisualCaptureMetadata metadata = new(
        "nova3d.visual-capture", 1, "contract-scene", Path.GetFileName(path),
        2, 2, 123, TimeSpan.TicksPerSecond / 60, 3, 4,
        "DesktopGL", "Contract GPU", "HiDef", "Color", "Depth24", 0,
        "Contract OS", "X64");
    string metadataPath = Path.Combine(
        Path.GetDirectoryName(path)!,
        Path.GetFileNameWithoutExtension(path) + ".capture.json");
    File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    }));
}

CommandResult Run(params string[] args)
{
    using StringWriter output = new();
    using StringWriter error = new();
    int exitCode = Nova3DCli.Run(args, output, error);
    return new CommandResult(exitCode, output.ToString(), error.ToString());
}

static void Require(bool condition, string evidence)
{
    if (!condition) throw new InvalidOperationException($"Visual regression contract failed: {evidence}.");
}

internal sealed record CommandResult(int ExitCode, string Output, string Error);
