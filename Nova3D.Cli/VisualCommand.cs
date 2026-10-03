using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nova3D.Production.VisualTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

internal static class VisualCommand
{
    private const string CaptureFormat = "nova3d.visual-capture";
    private const string BaselineFormat = "nova3d.visual-baseline";
    private const int ContractVersion = 1;
    private const int DefaultChannelTolerance = 2;
    private const double DefaultDifferentPixelRatio = 0.001;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            WriteHelp(output);
            return CliExitCodes.Success;
        }

        string operation = args[0].ToLowerInvariant();
        return operation switch
        {
            "compare" => Compare(args[1..], output, error),
            "update-baseline" => UpdateBaseline(args[1..], output, error),
            _ => Usage(error, $"unknown visual operation '{args[0]}'.")
        };
    }

    private static int Compare(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParse(args, requireAccept: false, out Options? options, out string? issue))
            return Usage(error, issue!);

        try
        {
            Capture capture = LoadCapture(options!.CapturePath);
            string baselineDirectory = GetBaselineDirectory(options.BaselineRoot, capture.Metadata);
            string manifestPath = Path.Combine(baselineDirectory, "baseline.json");
            if (!File.Exists(manifestPath))
            {
                error.WriteLine($"FAIL baseline | no baseline exists for scene '{capture.Metadata.Scene}' version {capture.Metadata.Version}.");
                error.WriteLine($"ACTION | Run 'nova3d visual update-baseline \"{capture.ImagePath}\" --baseline-root \"{options.BaselineRoot}\" --accept' after reviewing the capture.");
                return CliExitCodes.CommandFailed;
            }

            VisualBaselineManifest manifest = ReadManifest(manifestPath);
            ValidateManifest(manifest, capture.Metadata);
            string baselineImagePath = ResolveContainedFile(baselineDirectory, manifest.Image, "baseline image");
            if (!File.Exists(baselineImagePath))
                throw new InvalidDataException($"Baseline image is missing: {baselineImagePath}");
            if (!string.Equals(Sha256(baselineImagePath), manifest.ImageSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Baseline image hash does not match baseline.json.");
            string baselineMetadataPath = ResolveContainedFile(
                baselineDirectory,
                manifest.CaptureMetadata,
                "baseline capture metadata");
            if (!File.Exists(baselineMetadataPath) ||
                !string.Equals(Sha256(baselineMetadataPath), manifest.CaptureMetadataSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Baseline capture metadata is missing or its hash does not match baseline.json.");

            int channelTolerance = options.ChannelTolerance ?? manifest.ChannelTolerance;
            double allowedRatio = options.MaxDifferentPixelRatio ?? manifest.MaxDifferentPixelRatio;
            ValidateTolerances(channelTolerance, allowedRatio);

            using Image<Rgba32> baseline = Image.Load<Rgba32>(baselineImagePath);
            using Image<Rgba32> actual = Image.Load<Rgba32>(capture.ImagePath);
            if (baseline.Width != actual.Width || baseline.Height != actual.Height)
            {
                error.WriteLine($"FAIL dimensions | baseline {baseline.Width}x{baseline.Height}; capture {actual.Width}x{actual.Height}.");
                error.WriteLine("ACTION | Verify the capture plan, or explicitly review and update the baseline.");
                return CliExitCodes.CommandFailed;
            }

            Rgba32[] baselinePixels = new Rgba32[baseline.Width * baseline.Height];
            Rgba32[] actualPixels = new Rgba32[actual.Width * actual.Height];
            Rgba32[] diffPixels = new Rgba32[actualPixels.Length];
            baseline.CopyPixelDataTo(baselinePixels);
            actual.CopyPixelDataTo(actualPixels);
            Metrics metrics = ComparePixels(baselinePixels, actualPixels, diffPixels, channelTolerance);

            string diffPath = options.DiffPath ?? Path.Combine(
                Path.GetDirectoryName(capture.ImagePath)!,
                Path.GetFileNameWithoutExtension(capture.ImagePath) + ".diff.png");
            diffPath = Path.GetFullPath(diffPath);
            Directory.CreateDirectory(Path.GetDirectoryName(diffPath)!);
            using (Image<Rgba32> diff = Image.LoadPixelData<Rgba32>(diffPixels, actual.Width, actual.Height))
                diff.SaveAsPng(diffPath);

            bool passed = metrics.DifferentPixelRatio <= allowedRatio;
            output.WriteLine(Invariant(
                $"INFO metrics | pixels {metrics.PixelCount} | different {metrics.DifferentPixels} ({metrics.DifferentPixelRatio:P4}) | max delta {metrics.MaxChannelDelta} | MAE {metrics.MeanAbsoluteChannelError:F4} | RMSE {metrics.RootMeanSquareChannelError:F4}"));
            output.WriteLine($"INFO diff | {diffPath}");
            if (!EnvironmentMatches(manifest.Environment, capture.Metadata))
                output.WriteLine("WARN environment | capture GPU/backend differs from the baseline; result is auxiliary evidence.");

            if (passed)
            {
                output.WriteLine(Invariant($"VISUAL PASS | scene {capture.Metadata.Scene} v{capture.Metadata.Version} | tolerance {channelTolerance} | allowed {allowedRatio:P4}"));
                return CliExitCodes.Success;
            }

            error.WriteLine(Invariant($"VISUAL FAIL | scene {capture.Metadata.Scene} v{capture.Metadata.Version} | different {metrics.DifferentPixelRatio:P4} exceeds {allowedRatio:P4}"));
            error.WriteLine($"ACTION | Inspect the diff at '{diffPath}'; fix the regression or explicitly update the reviewed baseline.");
            return CliExitCodes.CommandFailed;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or NotSupportedException)
        {
            error.WriteLine($"FAIL visual | {OneLine(exception.Message)}");
            error.WriteLine("ACTION | Verify the capture, metadata, baseline root and file permissions, then run the command again.");
            return CliExitCodes.CommandFailed;
        }
    }

    private static int UpdateBaseline(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParse(args, requireAccept: true, out Options? options, out string? issue))
            return Usage(error, issue!);

        try
        {
            Capture capture = LoadCapture(options!.CapturePath);
            int channelTolerance = options.ChannelTolerance ?? DefaultChannelTolerance;
            double allowedRatio = options.MaxDifferentPixelRatio ?? DefaultDifferentPixelRatio;
            ValidateTolerances(channelTolerance, allowedRatio);

            string baselineDirectory = GetBaselineDirectory(options.BaselineRoot, capture.Metadata);
            Directory.CreateDirectory(baselineDirectory);
            string imageName = "baseline.png";
            string metadataName = "baseline.capture.json";
            string destinationImage = Path.Combine(baselineDirectory, imageName);
            string destinationMetadata = Path.Combine(baselineDirectory, metadataName);
            AtomicCopy(capture.ImagePath, destinationImage);
            AtomicCopy(capture.MetadataPath, destinationMetadata);

            VisualBaselineManifest manifest = new(
                BaselineFormat,
                ContractVersion,
                capture.Metadata.Scene,
                capture.Metadata.Version,
                imageName,
                metadataName,
                Sha256(destinationImage),
                Sha256(destinationMetadata),
                channelTolerance,
                allowedRatio,
                VisualEnvironment.From(capture.Metadata));
            AtomicWrite(Path.Combine(baselineDirectory, "baseline.json"),
                JsonSerializer.Serialize(manifest, JsonOptions));

            output.WriteLine($"INFO baseline | {baselineDirectory}");
            output.WriteLine(Invariant($"VISUAL PASS | baseline updated explicitly | scene {capture.Metadata.Scene} v{capture.Metadata.Version} | tolerance {channelTolerance} | allowed {allowedRatio:P4}"));
            return CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or NotSupportedException)
        {
            error.WriteLine($"FAIL visual | {OneLine(exception.Message)}");
            error.WriteLine("ACTION | Verify the capture, metadata, baseline root and file permissions, then run the command again.");
            return CliExitCodes.CommandFailed;
        }
    }

    private static bool TryParse(
        string[] args,
        bool requireAccept,
        out Options? options,
        out string? issue)
    {
        options = null;
        issue = null;
        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            issue = "a capture PNG path is required.";
            return false;
        }

        string capture = args[0];
        string? baselineRoot = null;
        string? diff = null;
        int? channelTolerance = null;
        double? ratio = null;
        bool accept = false;
        for (int index = 1; index < args.Length; index++)
        {
            string argument = args[index];
            if (string.Equals(argument, "--accept", StringComparison.OrdinalIgnoreCase))
            {
                if (accept) { issue = "--accept was specified more than once."; return false; }
                accept = true;
                continue;
            }

            if (++index >= args.Length)
            {
                issue = $"{argument} requires a value.";
                return false;
            }
            string value = args[index];
            switch (argument.ToLowerInvariant())
            {
                case "--baseline-root":
                    if (baselineRoot is not null) { issue = "--baseline-root was specified more than once."; return false; }
                    baselineRoot = value;
                    break;
                case "--diff":
                    if (diff is not null) { issue = "--diff was specified more than once."; return false; }
                    diff = value;
                    break;
                case "--channel-tolerance":
                    if (channelTolerance is not null || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedTolerance))
                    { issue = "--channel-tolerance requires one integer from 0 through 255."; return false; }
                    channelTolerance = parsedTolerance;
                    break;
                case "--max-different-pixel-ratio":
                    if (ratio is not null || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedRatio))
                    { issue = "--max-different-pixel-ratio requires one number from 0 through 1."; return false; }
                    ratio = parsedRatio;
                    break;
                default:
                    issue = $"unknown option '{argument}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(baselineRoot))
        { issue = "--baseline-root <DIRECTORY> is required."; return false; }
        if (requireAccept && !accept)
        { issue = "baseline updates require the explicit --accept option."; return false; }
        if (!requireAccept && accept)
        { issue = "--accept is valid only for update-baseline."; return false; }
        if (requireAccept && diff is not null)
        { issue = "--diff is valid only for compare."; return false; }
        if (channelTolerance is not null && (channelTolerance < 0 || channelTolerance > 255))
        { issue = "--channel-tolerance must be from 0 through 255."; return false; }
        if (ratio is not null && (!double.IsFinite(ratio.Value) || ratio < 0 || ratio > 1))
        { issue = "--max-different-pixel-ratio must be from 0 through 1."; return false; }

        options = new Options(
            Path.GetFullPath(capture),
            Path.GetFullPath(baselineRoot),
            diff is null ? null : Path.GetFullPath(diff),
            channelTolerance,
            ratio);
        return true;
    }

    private static Capture LoadCapture(string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Capture image was not found.", imagePath);
        if (!string.Equals(Path.GetExtension(imagePath), ".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Visual captures must be PNG files.");

        string metadataPath = Path.Combine(
            Path.GetDirectoryName(imagePath)!,
            Path.GetFileNameWithoutExtension(imagePath) + ".capture.json");
        if (!File.Exists(metadataPath))
            throw new FileNotFoundException("Capture metadata was not found beside the PNG.", metadataPath);

        VisualCaptureMetadata metadata = JsonSerializer.Deserialize<VisualCaptureMetadata>(File.ReadAllText(metadataPath), JsonOptions)
            ?? throw new InvalidDataException("Capture metadata is empty.");
        if (metadata.Format != CaptureFormat || metadata.Version != ContractVersion)
            throw new InvalidDataException($"Unsupported capture contract '{metadata.Format}' version {metadata.Version}.");
        if (metadata.Scene is "." or ".." || metadata.Scene.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            metadata.Scene.Contains(Path.DirectorySeparatorChar) || metadata.Scene.Contains(Path.AltDirectorySeparatorChar))
            throw new InvalidDataException("Capture scene must be one safe path segment.");
        if (metadata.Width <= 0 || metadata.Height <= 0)
            throw new InvalidDataException("Capture metadata dimensions must be positive.");
        if (!string.Equals(metadata.Image, Path.GetFileName(imagePath), StringComparison.Ordinal))
            throw new InvalidDataException("Capture metadata image name does not match the PNG file name.");

        using Image<Rgba32> image = Image.Load<Rgba32>(imagePath);
        if (image.Width != metadata.Width || image.Height != metadata.Height)
            throw new InvalidDataException("Capture PNG dimensions do not match its metadata.");
        return new Capture(Path.GetFullPath(imagePath), Path.GetFullPath(metadataPath), metadata);
    }

    private static VisualBaselineManifest ReadManifest(string path)
    {
        VisualBaselineManifest manifest = JsonSerializer.Deserialize<VisualBaselineManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Baseline manifest is empty.");
        if (manifest.Format != BaselineFormat || manifest.Version != ContractVersion)
            throw new InvalidDataException($"Unsupported baseline contract '{manifest.Format}' version {manifest.Version}.");
        return manifest;
    }

    private static void ValidateManifest(VisualBaselineManifest manifest, VisualCaptureMetadata capture)
    {
        if (manifest.Scene != capture.Scene || manifest.CaptureVersion != capture.Version)
            throw new InvalidDataException("Baseline scene or capture contract version does not match the capture.");
        ValidateTolerances(manifest.ChannelTolerance, manifest.MaxDifferentPixelRatio);
    }

    private static void ValidateTolerances(int channelTolerance, double ratio)
    {
        if (channelTolerance is < 0 or > 255)
            throw new InvalidDataException("Baseline channelTolerance must be from 0 through 255.");
        if (!double.IsFinite(ratio) || ratio is < 0 or > 1)
            throw new InvalidDataException("Baseline maxDifferentPixelRatio must be from 0 through 1.");
    }

    private static Metrics ComparePixels(
        Rgba32[] baseline,
        Rgba32[] actual,
        Rgba32[] diff,
        int channelTolerance)
    {
        long different = 0;
        long absoluteSum = 0;
        long squareSum = 0;
        int max = 0;
        for (int index = 0; index < baseline.Length; index++)
        {
            Rgba32 left = baseline[index];
            Rgba32 right = actual[index];
            int dr = Math.Abs(left.R - right.R);
            int dg = Math.Abs(left.G - right.G);
            int db = Math.Abs(left.B - right.B);
            int da = Math.Abs(left.A - right.A);
            int pixelMax = Math.Max(Math.Max(dr, dg), Math.Max(db, da));
            if (pixelMax > channelTolerance) different++;
            max = Math.Max(max, pixelMax);
            absoluteSum += dr + dg + db + da;
            squareSum += (long)dr * dr + (long)dg * dg + (long)db * db + (long)da * da;
            diff[index] = pixelMax <= channelTolerance
                ? new Rgba32(0, 0, 0, 255)
                : new Rgba32(Amplify(dr), Amplify(dg), Amplify(db), 255);
        }

        long channelCount = checked((long)baseline.Length * 4);
        return new Metrics(
            baseline.Length,
            different,
            baseline.Length == 0 ? 0 : (double)different / baseline.Length,
            max,
            channelCount == 0 ? 0 : (double)absoluteSum / channelCount,
            channelCount == 0 ? 0 : Math.Sqrt((double)squareSum / channelCount));
    }

    private static byte Amplify(int value) => (byte)Math.Min(255, value * 4);

    private static string GetBaselineDirectory(string root, VisualCaptureMetadata metadata) =>
        Path.Combine(Path.GetFullPath(root), metadata.Scene, $"v{metadata.Version}");

    private static string ResolveContainedFile(string directory, string relativeName, string description)
    {
        if (string.IsNullOrWhiteSpace(relativeName) || Path.IsPathRooted(relativeName))
            throw new InvalidDataException($"The {description} path must be relative.");
        string root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(directory, relativeName));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {description} escapes its baseline directory.");
        return path;
    }

    private static bool EnvironmentMatches(VisualEnvironment baseline, VisualCaptureMetadata actual) =>
        baseline.Backend == actual.Backend &&
        baseline.GraphicsAdapter == actual.GraphicsAdapter &&
        baseline.GraphicsProfile == actual.GraphicsProfile &&
        baseline.BackBufferFormat == actual.BackBufferFormat &&
        baseline.DepthStencilFormat == actual.DepthStencilFormat &&
        baseline.MultiSampleCount == actual.MultiSampleCount &&
        baseline.OperatingSystem == actual.OperatingSystem &&
        baseline.ProcessArchitecture == actual.ProcessArchitecture;

    private static void AtomicCopy(string source, string destination)
    {
        string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(source, temporary, overwrite: false);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void AtomicWrite(string destination, string contents)
    {
        string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, contents);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static int Usage(TextWriter error, string issue)
    {
        error.WriteLine($"Invalid visual command usage: {issue}");
        error.WriteLine("ACTION | Run 'nova3d visual --help' for usage.");
        return CliExitCodes.UsageError;
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("Nova3D visual regression");
        output.WriteLine();
        output.WriteLine("Usage:");
        output.WriteLine("  nova3d visual compare <CAPTURE.png> --baseline-root <DIRECTORY> [options]");
        output.WriteLine("  nova3d visual update-baseline <CAPTURE.png> --baseline-root <DIRECTORY> --accept [options]");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  --channel-tolerance <0..255>          Per-channel threshold (default baseline: 2)");
        output.WriteLine("  --max-different-pixel-ratio <0..1>   Allowed changed-pixel ratio (default baseline: 0.001)");
        output.WriteLine("  --diff <PATH>                         Compare diff output path");
        output.WriteLine("  --accept                              Required explicit baseline update confirmation");
    }

    private static string OneLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string Invariant(FormattableString value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private sealed record Options(
        string CapturePath,
        string BaselineRoot,
        string? DiffPath,
        int? ChannelTolerance,
        double? MaxDifferentPixelRatio);

    private sealed record Capture(
        string ImagePath,
        string MetadataPath,
        VisualCaptureMetadata Metadata);

    private sealed record Metrics(
        long PixelCount,
        long DifferentPixels,
        double DifferentPixelRatio,
        int MaxChannelDelta,
        double MeanAbsoluteChannelError,
        double RootMeanSquareChannelError);

    private sealed record VisualBaselineManifest(
        string Format,
        int Version,
        string Scene,
        int CaptureVersion,
        string Image,
        string CaptureMetadata,
        string ImageSha256,
        string CaptureMetadataSha256,
        int ChannelTolerance,
        double MaxDifferentPixelRatio,
        VisualEnvironment Environment);

    private sealed record VisualEnvironment(
        string Backend,
        string GraphicsAdapter,
        string GraphicsProfile,
        string BackBufferFormat,
        string DepthStencilFormat,
        int MultiSampleCount,
        string OperatingSystem,
        string ProcessArchitecture)
    {
        public static VisualEnvironment From(VisualCaptureMetadata metadata) => new(
            metadata.Backend,
            metadata.GraphicsAdapter,
            metadata.GraphicsProfile,
            metadata.BackBufferFormat,
            metadata.DepthStencilFormat,
            metadata.MultiSampleCount,
            metadata.OperatingSystem,
            metadata.ProcessArchitecture);
    }
}
