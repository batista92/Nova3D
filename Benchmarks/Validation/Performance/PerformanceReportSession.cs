using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Benchmarks.Validation.Performance;

internal sealed class PerformanceReportSession
{
    public const string FormatName = "nova3d.performance-report";
    public const int CurrentVersion = 1;
    private readonly GraphicsDevice _device;
    private readonly string _outputPath;
    private readonly List<double> _frameIntervals = new();
    private readonly List<double> _updates = new();
    private readonly List<double> _draws = new();
    private readonly List<double> _shadows = new();
    private readonly List<double> _world = new();
    private readonly List<double> _post = new();
    private readonly List<long> _drawCalls = new();
    private readonly List<long> _triangles = new();
    private readonly List<long> _shadowTriangles = new();
    private readonly List<long> _shadowDrawCalls = new();
    private readonly List<long> _instances = new();
    private readonly List<long> _visibleChunks = new();
    private readonly List<long> _candidates = new();
    private int _frame;

    public PerformanceReportSession(
        GraphicsDevice device,
        string outputPath,
        int warmupFrames = 180,
        int sampleFrames = 240)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (warmupFrames < 1) throw new ArgumentOutOfRangeException(nameof(warmupFrames));
        if (sampleFrames < 1) throw new ArgumentOutOfRangeException(nameof(sampleFrames));
        _outputPath = Path.GetFullPath(outputPath);
        WarmupFrames = warmupFrames;
        SampleFrames = sampleFrames;
    }

    public int WarmupFrames { get; }
    public int SampleFrames { get; }
    public bool IsComplete => _frame >= WarmupFrames + SampleFrames;

    public PerformanceReport? Record(
        double frameIntervalMilliseconds,
        double updateCpuMilliseconds,
        double drawCpuMilliseconds,
        CityPerformanceSnapshot snapshot)
    {
        if (IsComplete) return null;
        _frame++;
        if (_frame <= WarmupFrames) return null;

        Add(_frameIntervals, frameIntervalMilliseconds);
        Add(_updates, updateCpuMilliseconds);
        Add(_draws, drawCpuMilliseconds);
        Add(_shadows, snapshot.ShadowCpuMilliseconds);
        Add(_world, snapshot.WorldCpuMilliseconds);
        Add(_post, snapshot.PostCpuMilliseconds);
        _drawCalls.Add(snapshot.DrawCalls);
        _triangles.Add(snapshot.Triangles);
        _shadowTriangles.Add(snapshot.ShadowTriangles);
        _shadowDrawCalls.Add(snapshot.ShadowDrawCalls);
        _instances.Add(snapshot.Instances);
        _visibleChunks.Add(snapshot.VisibleChunks);
        _candidates.Add(snapshot.Candidates);
        if (!IsComplete) return null;

        PresentationParameters presentation = _device.PresentationParameters;
        PerformanceReport report = new(
            FormatName,
            CurrentVersion,
            "city-benchmark",
            presentation.BackBufferWidth,
            presentation.BackBufferHeight,
            WarmupFrames,
            SampleFrames,
            "DesktopGL",
            GraphicsAdapter.DefaultAdapter.Description,
            _device.GraphicsProfile.ToString(),
            presentation.MultiSampleCount,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            new PerformanceMetrics(
                Summarize(_frameIntervals),
                Summarize(_updates),
                Summarize(_draws),
                Summarize(_shadows),
                Summarize(_world),
                Summarize(_post),
                SummarizeCounts(_drawCalls),
                SummarizeCounts(_triangles),
                SummarizeCounts(_shadowTriangles),
                SummarizeCounts(_shadowDrawCalls),
                SummarizeCounts(_instances),
                SummarizeCounts(_visibleChunks),
                SummarizeCounts(_candidates)));
        Write(report);
        return report;
    }

    private static void Add(List<double> values, double value)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new InvalidOperationException("Performance timing must be finite and non-negative.");
        values.Add(value);
    }

    private void Write(PerformanceReport report)
    {
        string? directory = Path.GetDirectoryName(_outputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        string temporary = _outputPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(report, JsonOptions));
            File.Move(temporary, _outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static TimingSummary Summarize(List<double> source)
    {
        double[] sorted = source.Order().ToArray();
        return new TimingSummary(
            source.Average(),
            Percentile(sorted, 0.50),
            Percentile(sorted, 0.95),
            sorted[^1]);
    }

    private static CountSummary SummarizeCounts(List<long> source) =>
        new(source.Average(value => (double)value), source.Max());

    private static double Percentile(double[] sorted, double percentile)
    {
        double position = (sorted.Length - 1) * percentile;
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        if (lower == upper) return sorted[lower];
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
}

internal sealed record PerformanceReport(
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
    PerformanceMetrics Metrics);

internal sealed record PerformanceMetrics(
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

internal sealed record TimingSummary(double Average, double P50, double P95, double Maximum);
internal sealed record CountSummary(double Average, long Maximum);
