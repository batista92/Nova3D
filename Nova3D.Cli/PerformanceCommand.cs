using System.Globalization;
using System.Text.Json;

internal static class PerformanceCommand
{
    private const string ReportFormat = "nova3d.performance-report";
    private const string BudgetFormat = "nova3d.performance-budget";
    private const int ContractVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            output.WriteLine("Nova3D performance gate");
            output.WriteLine();
            output.WriteLine("Usage:");
            output.WriteLine("  nova3d performance check <REPORT.json> --budget <BUDGET.json>");
            return CliExitCodes.Success;
        }
        if (!string.Equals(args[0], "check", StringComparison.OrdinalIgnoreCase))
            return Usage(error, $"unknown performance operation '{args[0]}'.");
        if (!TryParse(args[1..], out string? reportPath, out string? budgetPath, out string? issue))
            return Usage(error, issue!);

        try
        {
            PerformanceReport report = Read<PerformanceReport>(reportPath!, ReportFormat);
            PerformanceBudget budget = Read<PerformanceBudget>(budgetPath!, BudgetFormat);
            ValidateReport(report);
            ValidateBudgetDocument(budget);
            if (!string.Equals(report.Scene, budget.Scene, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Report scene '{report.Scene}' does not match budget scene '{budget.Scene}'.");
            if (report.SampleFrames < budget.MinimumSampleFrames)
                throw new InvalidDataException(
                    $"Report has {report.SampleFrames} samples; budget requires {budget.MinimumSampleFrames}.");
            ValidateBudget(budget.Limits);

            List<string> failures = new();
            Check(failures, "frame-interval-p95-ms", report.Metrics.FrameIntervalMilliseconds.P95,
                budget.Limits.FrameIntervalP95Milliseconds);
            Check(failures, "update-cpu-p95-ms", report.Metrics.UpdateCpuMilliseconds.P95,
                budget.Limits.UpdateCpuP95Milliseconds);
            Check(failures, "draw-cpu-p95-ms", report.Metrics.DrawCpuMilliseconds.P95,
                budget.Limits.DrawCpuP95Milliseconds);
            Check(failures, "shadow-cpu-p95-ms", report.Metrics.ShadowCpuMilliseconds.P95,
                budget.Limits.ShadowCpuP95Milliseconds);
            Check(failures, "world-cpu-p95-ms", report.Metrics.WorldCpuMilliseconds.P95,
                budget.Limits.WorldCpuP95Milliseconds);
            Check(failures, "post-cpu-p95-ms", report.Metrics.PostCpuMilliseconds.P95,
                budget.Limits.PostCpuP95Milliseconds);
            Check(failures, "draw-calls-max", report.Metrics.DrawCalls.Maximum,
                budget.Limits.DrawCallsMaximum);
            Check(failures, "triangles-max", report.Metrics.Triangles.Maximum,
                budget.Limits.TrianglesMaximum);
            Check(failures, "shadow-triangles-max", report.Metrics.ShadowTriangles.Maximum,
                budget.Limits.ShadowTrianglesMaximum);
            Check(failures, "shadow-draw-calls-max", report.Metrics.ShadowDrawCalls.Maximum,
                budget.Limits.ShadowDrawCallsMaximum);
            Check(failures, "candidates-max", report.Metrics.Candidates.Maximum,
                budget.Limits.CandidatesMaximum);

            bool referenceEnvironment = EnvironmentMatches(report, budget.ReferenceEnvironment);
            output.WriteLine(Invariant(
                $"INFO performance | frame p95 {report.Metrics.FrameIntervalMilliseconds.P95:F3} ms | update {report.Metrics.UpdateCpuMilliseconds.P95:F3} ms | draw {report.Metrics.DrawCpuMilliseconds.P95:F3} ms | draws {report.Metrics.DrawCalls.Maximum} | tris {report.Metrics.Triangles.Maximum}"));
            if (!referenceEnvironment)
                output.WriteLine("WARN environment | report hardware/backend differs from the budget reference; result is auxiliary evidence.");

            if (failures.Count == 0)
            {
                output.WriteLine($"PERFORMANCE PASS | scene {report.Scene} | limits 11 | samples {report.SampleFrames}");
                return CliExitCodes.Success;
            }

            foreach (string failure in failures) error.WriteLine(failure);
            error.WriteLine($"PERFORMANCE FAIL | scene {report.Scene} | exceeded {failures.Count}/11 limits");
            error.WriteLine("ACTION | Inspect the first exceeded metric, reproduce on the reference environment and profile before changing the budget.");
            return CliExitCodes.CommandFailed;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            error.WriteLine($"FAIL performance | {OneLine(exception.Message)}");
            error.WriteLine("ACTION | Verify the report and budget contracts, paths and permissions, then run the command again.");
            return CliExitCodes.CommandFailed;
        }
    }

    private static bool TryParse(
        string[] args,
        out string? reportPath,
        out string? budgetPath,
        out string? issue)
    {
        reportPath = null;
        budgetPath = null;
        issue = null;
        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            issue = "a performance report path is required.";
            return false;
        }
        reportPath = Path.GetFullPath(args[0]);
        for (int index = 1; index < args.Length; index++)
        {
            string argument = args[index];
            if (!string.Equals(argument, "--budget", StringComparison.OrdinalIgnoreCase))
            {
                issue = $"unknown option '{argument}'.";
                return false;
            }
            if (budgetPath is not null)
            {
                issue = "--budget was specified more than once.";
                return false;
            }
            if (++index >= args.Length)
            {
                issue = "--budget requires a JSON path.";
                return false;
            }
            budgetPath = Path.GetFullPath(args[index]);
        }
        if (budgetPath is null)
        {
            issue = "--budget <BUDGET.json> is required.";
            return false;
        }
        return true;
    }

    private static T Read<T>(string path, string expectedFormat) where T : ContractDocument
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Performance contract file was not found.", path);
        T value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Performance contract document is empty.");
        if (value.Format != expectedFormat || value.Version != ContractVersion)
            throw new InvalidDataException(
                $"Unsupported performance contract '{value.Format}' version {value.Version}.");
        return value;
    }

    private static void ValidateBudget(PerformanceLimits limits)
    {
        foreach (double value in new[]
        {
            limits.FrameIntervalP95Milliseconds,
            limits.UpdateCpuP95Milliseconds,
            limits.DrawCpuP95Milliseconds,
            limits.ShadowCpuP95Milliseconds,
            limits.WorldCpuP95Milliseconds,
            limits.PostCpuP95Milliseconds,
            limits.DrawCallsMaximum,
            limits.TrianglesMaximum,
            limits.ShadowTrianglesMaximum,
            limits.ShadowDrawCallsMaximum,
            limits.CandidatesMaximum
        })
            if (!double.IsFinite(value) || value < 0)
                throw new InvalidDataException("Performance budget limits must be finite and non-negative.");
    }

    private static void ValidateReport(PerformanceReport report)
    {
        if (string.IsNullOrWhiteSpace(report.Scene) || report.Width < 1 || report.Height < 1 ||
            report.WarmupFrames < 1 || report.SampleFrames < 1 ||
            string.IsNullOrWhiteSpace(report.Backend) || string.IsNullOrWhiteSpace(report.GraphicsAdapter) ||
            string.IsNullOrWhiteSpace(report.GraphicsProfile) || report.MultiSampleCount < 0 ||
            string.IsNullOrWhiteSpace(report.OperatingSystem) || string.IsNullOrWhiteSpace(report.ProcessArchitecture) ||
            report.Metrics is null)
            throw new InvalidDataException("Performance report is missing required environment, sampling or metrics data.");

        ValidateTiming("frameIntervalMilliseconds", report.Metrics.FrameIntervalMilliseconds);
        ValidateTiming("updateCpuMilliseconds", report.Metrics.UpdateCpuMilliseconds);
        ValidateTiming("drawCpuMilliseconds", report.Metrics.DrawCpuMilliseconds);
        ValidateTiming("shadowCpuMilliseconds", report.Metrics.ShadowCpuMilliseconds);
        ValidateTiming("worldCpuMilliseconds", report.Metrics.WorldCpuMilliseconds);
        ValidateTiming("postCpuMilliseconds", report.Metrics.PostCpuMilliseconds);
        ValidateCount("drawCalls", report.Metrics.DrawCalls);
        ValidateCount("triangles", report.Metrics.Triangles);
        ValidateCount("shadowTriangles", report.Metrics.ShadowTriangles);
        ValidateCount("shadowDrawCalls", report.Metrics.ShadowDrawCalls);
        ValidateCount("instances", report.Metrics.Instances);
        ValidateCount("visibleChunks", report.Metrics.VisibleChunks);
        ValidateCount("candidates", report.Metrics.Candidates);
    }

    private static void ValidateBudgetDocument(PerformanceBudget budget)
    {
        if (string.IsNullOrWhiteSpace(budget.Scene) || budget.MinimumSampleFrames < 1 ||
            budget.ReferenceEnvironment is null || budget.Limits is null ||
            budget.ReferenceEnvironment.Width < 1 || budget.ReferenceEnvironment.Height < 1 ||
            string.IsNullOrWhiteSpace(budget.ReferenceEnvironment.Backend) ||
            string.IsNullOrWhiteSpace(budget.ReferenceEnvironment.GraphicsAdapter) ||
            string.IsNullOrWhiteSpace(budget.ReferenceEnvironment.GraphicsProfile) ||
            budget.ReferenceEnvironment.MultiSampleCount < 0)
            throw new InvalidDataException("Performance budget is missing required scene, environment or limits data.");
    }

    private static void ValidateTiming(string name, TimingSummary? timing)
    {
        if (timing is null || !double.IsFinite(timing.Average) || !double.IsFinite(timing.P50) ||
            !double.IsFinite(timing.P95) || !double.IsFinite(timing.Maximum) ||
            timing.Average < 0 || timing.P50 < 0 || timing.P95 < timing.P50 || timing.Maximum < timing.P95)
            throw new InvalidDataException($"Report timing '{name}' is invalid or incomplete.");
    }

    private static void ValidateCount(string name, CountSummary? count)
    {
        if (count is null || !double.IsFinite(count.Average) || count.Average < 0 || count.Maximum < 0)
            throw new InvalidDataException($"Report count '{name}' is invalid or incomplete.");
    }

    private static void Check(List<string> failures, string name, double actual, double limit)
    {
        if (!double.IsFinite(actual) || actual < 0)
            throw new InvalidDataException($"Report metric '{name}' must be finite and non-negative.");
        if (actual > limit)
            failures.Add(Invariant($"FAIL {name} | actual {actual:F4} exceeds budget {limit:F4}"));
    }

    private static bool EnvironmentMatches(PerformanceReport report, ReferenceEnvironment reference) =>
        report.Width == reference.Width && report.Height == reference.Height &&
        report.Backend == reference.Backend &&
        report.GraphicsAdapter == reference.GraphicsAdapter &&
        report.GraphicsProfile == reference.GraphicsProfile &&
        report.MultiSampleCount == reference.MultiSampleCount;

    private static int Usage(TextWriter error, string issue)
    {
        error.WriteLine($"Invalid performance command usage: {issue}");
        error.WriteLine("ACTION | Run 'nova3d performance --help' for usage.");
        return CliExitCodes.UsageError;
    }

    private static string Invariant(FormattableString value) =>
        value.ToString(CultureInfo.InvariantCulture);
    private static string OneLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private abstract record ContractDocument(string Format, int Version);
    private sealed record PerformanceReport(
        string Format,
        int Version,
        string Scene,
        int Width,
        int Height,
        int WarmupFrames,
        int SampleFrames,
        string Backend,
        string GraphicsAdapter,
        string GraphicsProfile,
        int MultiSampleCount,
        string OperatingSystem,
        string ProcessArchitecture,
        PerformanceMetrics Metrics) : ContractDocument(Format, Version);
    private sealed record PerformanceBudget(
        string Format,
        int Version,
        string Scene,
        int MinimumSampleFrames,
        ReferenceEnvironment ReferenceEnvironment,
        PerformanceLimits Limits) : ContractDocument(Format, Version);
    private sealed record ReferenceEnvironment(
        int Width,
        int Height,
        string Backend,
        string GraphicsAdapter,
        string GraphicsProfile,
        int MultiSampleCount);
    private sealed record PerformanceMetrics(
        TimingSummary FrameIntervalMilliseconds,
        TimingSummary UpdateCpuMilliseconds,
        TimingSummary DrawCpuMilliseconds,
        TimingSummary ShadowCpuMilliseconds,
        TimingSummary WorldCpuMilliseconds,
        TimingSummary PostCpuMilliseconds,
        CountSummary DrawCalls,
        CountSummary Triangles,
        CountSummary ShadowTriangles,
        CountSummary ShadowDrawCalls,
        CountSummary Instances,
        CountSummary VisibleChunks,
        CountSummary Candidates);
    private sealed record TimingSummary(double Average, double P50, double P95, double Maximum);
    private sealed record CountSummary(double Average, long Maximum);
    private sealed record PerformanceLimits(
        double FrameIntervalP95Milliseconds,
        double UpdateCpuP95Milliseconds,
        double DrawCpuP95Milliseconds,
        double ShadowCpuP95Milliseconds,
        double WorldCpuP95Milliseconds,
        double PostCpuP95Milliseconds,
        long DrawCallsMaximum,
        long TrianglesMaximum,
        long ShadowTrianglesMaximum,
        long ShadowDrawCallsMaximum,
        long CandidatesMaximum);
}
