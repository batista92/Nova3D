namespace Nova3D.Production.VisualTesting;

/// <summary>Versioned environment and deterministic inputs stored beside a capture.</summary>
public sealed record VisualCaptureMetadata(
    string Format,
    int Version,
    string Scene,
    string Image,
    int Width,
    int Height,
    int Seed,
    long FixedTimeStepTicks,
    int WarmupFrames,
    int CaptureFrame,
    string Backend,
    string GraphicsAdapter,
    string GraphicsProfile,
    string BackBufferFormat,
    string DepthStencilFormat,
    int MultiSampleCount,
    string OperatingSystem,
    string ProcessArchitecture);

/// <summary>Paths and metadata produced by one completed visual capture.</summary>
public sealed record VisualCaptureResult(
    string ImagePath,
    string MetadataPath,
    VisualCaptureMetadata Metadata);
