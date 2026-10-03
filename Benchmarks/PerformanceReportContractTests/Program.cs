using System.Text.Json;

string root = Path.Combine(Path.GetTempPath(), "nova3d-performance-contract-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string reportPath = Path.Combine(root, "report.json");
    string budgetPath = Path.Combine(root, "budget.json");
    WriteReport(reportPath, frameP95: 4.2, adapter: "Contract GPU");
    WriteBudget(budgetPath, frameLimit: 6.0, adapter: "Contract GPU");

    CommandResult pass = Run("performance", "check", reportPath, "--budget", budgetPath);
    Require(pass.ExitCode == 0 && pass.Output.Contains("PERFORMANCE PASS"), "passing budget");

    WriteReport(reportPath, frameP95: 4.2, adapter: "Other GPU");
    CommandResult auxiliary = Run("performance", "check", reportPath, "--budget", budgetPath);
    Require(auxiliary.ExitCode == 0 && auxiliary.Output.Contains("WARN environment"), "environment warning");

    WriteReport(reportPath, frameP95: 8.0, adapter: "Contract GPU");
    CommandResult regression = Run("performance", "check", reportPath, "--budget", budgetPath);
    Require(regression.ExitCode == 1, "budget failure exit code");
    Require(regression.Error.Contains("FAIL frame-interval-p95-ms"), "failed metric");
    Require(regression.Error.Contains("ACTION |"), "actionable failure");

    CommandResult json = Run("--format", "json", "performance", "check", reportPath, "--budget", budgetPath);
    using JsonDocument document = JsonDocument.Parse(json.Output);
    Require(json.ExitCode == 1, "JSON failure exit code");
    Require(document.RootElement.GetProperty("status").GetString() == "fail", "JSON status");
    Require(document.RootElement.GetProperty("summary").GetString()!.StartsWith("PERFORMANCE FAIL"), "JSON summary");
    Require(document.RootElement.GetProperty("recommendedActions").GetArrayLength() == 1, "JSON action");
    Require(string.IsNullOrEmpty(json.Error), "JSON stderr");

    File.WriteAllText(reportPath,
        "{\"format\":\"nova3d.performance-report\",\"version\":1,\"scene\":\"contract-scene\"}");
    CommandResult incomplete = Run("performance", "check", reportPath, "--budget", budgetPath);
    Require(incomplete.ExitCode == 1 && incomplete.Error.Contains("ACTION |"), "incomplete report diagnostic");

    Console.WriteLine("Performance G8.4 PASS | budget | environment | failure | action | JSON | incomplete report");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

void WriteReport(string path, double frameP95, string adapter)
{
    object timing(double p95) => new { average = p95 * 0.8, p50 = p95 * 0.75, p95, maximum = p95 * 1.2 };
    object count(long maximum) => new { average = maximum * 0.95, maximum };
    var report = new
    {
        format = "nova3d.performance-report", version = 1, scene = "contract-scene",
        width = 1280, height = 720, warmupFrames = 180, sampleFrames = 240,
        backend = "DesktopGL", graphicsAdapter = adapter, graphicsProfile = "HiDef",
        multiSampleCount = 0, operatingSystem = "Contract OS", processArchitecture = "X64",
        metrics = new
        {
            frameIntervalMilliseconds = timing(frameP95), updateCpuMilliseconds = timing(2),
            drawCpuMilliseconds = timing(1), shadowCpuMilliseconds = timing(0.2),
            worldCpuMilliseconds = timing(0.8), postCpuMilliseconds = timing(0.05),
            drawCalls = count(450), triangles = count(62000), shadowTriangles = count(255000),
            shadowDrawCalls = count(20), instances = count(12000), visibleChunks = count(420),
            candidates = count(11500)
        }
    };
    File.WriteAllText(path, JsonSerializer.Serialize(report));
}

void WriteBudget(string path, double frameLimit, string adapter)
{
    var budget = new
    {
        format = "nova3d.performance-budget", version = 1, scene = "contract-scene", minimumSampleFrames = 240,
        referenceEnvironment = new { width = 1280, height = 720, backend = "DesktopGL", graphicsAdapter = adapter, graphicsProfile = "HiDef", multiSampleCount = 0 },
        limits = new
        {
            frameIntervalP95Milliseconds = frameLimit, updateCpuP95Milliseconds = 3.0,
            drawCpuP95Milliseconds = 1.5, shadowCpuP95Milliseconds = 0.3,
            worldCpuP95Milliseconds = 1.2, postCpuP95Milliseconds = 0.1,
            drawCallsMaximum = 500, trianglesMaximum = 70000, shadowTrianglesMaximum = 280000,
            shadowDrawCallsMaximum = 24, candidatesMaximum = 12000
        }
    };
    File.WriteAllText(path, JsonSerializer.Serialize(budget));
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
    if (!condition) throw new InvalidOperationException($"Performance contract failed: {evidence}.");
}

internal sealed record CommandResult(int ExitCode, string Output, string Error);
