using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Production.VisualTesting;

/// <summary>
/// Performs one explicit back-buffer readback after a known warm-up and writes
/// a PNG plus versioned metadata. Construct and call it on the graphics thread.
/// </summary>
public sealed class VisualCaptureSession
{
    private const string MetadataFormat = "nova3d.visual-capture";
    private const int MetadataVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly GraphicsDevice _device;
    private readonly VisualCapturePlan _plan;
    private readonly string _outputDirectory;
    private readonly string _backend;
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private int _renderedFrames;

    public VisualCaptureSession(
        GraphicsDevice device,
        VisualCapturePlan plan,
        string outputDirectory,
        string backend)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(backend);
        _device = device;
        _plan = plan;
        _outputDirectory = Path.GetFullPath(outputDirectory);
        _backend = backend.Trim();
    }

    public VisualCapturePlan Plan => _plan;
    public int RenderedFrames => _renderedFrames;
    public bool IsComplete { get; private set; }
    public VisualCaptureResult? Result { get; private set; }

    /// <summary>
    /// Call exactly once after the final back-buffer composition of each frame.
    /// Returns a result only on the capture frame.
    /// </summary>
    public VisualCaptureResult? CompleteFrame()
    {
        VerifyOwnerThread();
        if (IsComplete)
        {
            return null;
        }

        _renderedFrames = checked(_renderedFrames + 1);
        if (_renderedFrames < _plan.CaptureFrame)
        {
            return null;
        }

        PresentationParameters presentation = _device.PresentationParameters;
        if (presentation.BackBufferWidth != _plan.Width ||
            presentation.BackBufferHeight != _plan.Height)
        {
            throw new InvalidOperationException(
                $"Capture expected {_plan.Width}x{_plan.Height}, but the back buffer is " +
                $"{presentation.BackBufferWidth}x{presentation.BackBufferHeight}.");
        }

        Directory.CreateDirectory(_outputDirectory);
        string imageName = _plan.SceneName + ".png";
        string imagePath = Path.Combine(_outputDirectory, imageName);
        string metadataPath = Path.Combine(_outputDirectory, _plan.SceneName + ".capture.json");
        string imageTemporary = imagePath + ".tmp-" + Guid.NewGuid().ToString("N");
        string metadataTemporary = metadataPath + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            Color[] pixels = GC.AllocateUninitializedArray<Color>(
                checked(_plan.Width * _plan.Height));
            _device.GetBackBufferData(pixels);
            using (Texture2D image = new(_device, _plan.Width, _plan.Height, false, SurfaceFormat.Color))
            {
                image.SetData(pixels);
                using FileStream stream = new(imageTemporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                image.SaveAsPng(stream, _plan.Width, _plan.Height);
            }

            VisualCaptureMetadata metadata = CreateMetadata(presentation, imageName);
            string json = JsonSerializer.Serialize(metadata, JsonOptions) + Environment.NewLine;
            File.WriteAllText(metadataTemporary, json, new UTF8Encoding(false));
            File.Move(imageTemporary, imagePath, overwrite: true);
            File.Move(metadataTemporary, metadataPath, overwrite: true);

            Result = new VisualCaptureResult(imagePath, metadataPath, metadata);
            IsComplete = true;
            return Result;
        }
        finally
        {
            TryDelete(imageTemporary);
            TryDelete(metadataTemporary);
        }
    }

    private VisualCaptureMetadata CreateMetadata(PresentationParameters presentation, string imageName) => new(
        MetadataFormat,
        MetadataVersion,
        _plan.SceneName,
        imageName,
        _plan.Width,
        _plan.Height,
        _plan.Seed,
        _plan.FixedTimeStep.Ticks,
        _plan.WarmupFrames,
        _plan.CaptureFrame,
        _backend,
        _device.Adapter.Description ?? "unknown",
        _device.GraphicsProfile.ToString(),
        presentation.BackBufferFormat.ToString(),
        presentation.DepthStencilFormat.ToString(),
        presentation.MultiSampleCount,
        RuntimeInformation.OSDescription,
        RuntimeInformation.ProcessArchitecture.ToString());

    private void VerifyOwnerThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
        {
            throw new InvalidOperationException(
                "Visual capture must run on the graphics thread that created the session.");
        }
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
