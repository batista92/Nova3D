using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static partial class DoctorCommand
{
    private const int RequiredSdkMajor = 8;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is ["--help"] or ["-h"])
        {
            WriteHelp(output);
            return CliExitCodes.Success;
        }
        if (args.Length > 1)
        {
            error.WriteLine("Invalid doctor usage: only one project or directory may be specified.");
            error.WriteLine("ACTION | Run 'nova3d doctor --help' for usage.");
            return CliExitCodes.UsageError;
        }

        DoctorReport report = new(output);
        report.Header();
        CheckDotNetSdk(report);

        string candidate = args.Length == 1 ? args[0] : Environment.CurrentDirectory;
        if (ProjectLocator.TryResolve(candidate, out string? projectPath, out string? resolutionError))
        {
            report.Pass("project", projectPath!);
            CheckProject(projectPath!, report);
        }
        else
        {
            report.Fail("project", resolutionError!);
        }

        return report.Complete();
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("Usage: nova3d doctor [PROJECT|DIRECTORY]");
        output.WriteLine();
        output.WriteLine("Checks the .NET SDK, Nova3D project reference, target framework,");
        output.WriteLine("restore assets and declared MonoGame content files.");
    }

    private static void CheckDotNetSdk(DoctorReport report)
    {
        try
        {
            ProcessStartInfo startInfo = new("dotnet", "--list-sdks")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("dotnet process did not start.");
            process.StandardInput.Close();
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                report.Fail("dotnet-sdk", $"dotnet --list-sdks failed: {OneLine(standardError)}");
                return;
            }

            string[] versions = standardOutput.Split(
                new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string? compatible = versions
                .Select(line => SdkVersionRegex().Match(line))
                .Where(match => match.Success &&
                    int.Parse(match.Groups["major"].Value) >= RequiredSdkMajor)
                .Select(match => match.Groups["version"].Value)
                .LastOrDefault();

            if (compatible is null)
            {
                report.Fail("dotnet-sdk", $".NET SDK {RequiredSdkMajor}+ was not found. Install the .NET 8 SDK or newer.");
                return;
            }

            report.Pass("dotnet-sdk", $"compatible SDK {compatible}");
        }
        catch (Exception exception)
        {
            report.Fail("dotnet-sdk", $"could not execute dotnet: {OneLine(exception.Message)}");
        }
    }

    private static void CheckProject(string projectPath, DoctorReport report)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(projectPath, LoadOptions.None);
        }
        catch (Exception exception)
        {
            report.Fail("project-xml", $"cannot read project: {OneLine(exception.Message)}");
            return;
        }

        XElement root = document.Root!;
        string[] frameworks = root.Descendants("TargetFramework")
            .Concat(root.Descendants("TargetFrameworks"))
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string? compatibleFramework = frameworks.FirstOrDefault(IsCompatibleFramework);
        if (compatibleFramework is null)
        {
            string found = frameworks.Length == 0 ? "none declared" : string.Join(", ", frameworks);
            report.Fail("target-framework", $"requires net8.0 or newer; found {found}.");
        }
        else
        {
            report.Pass("target-framework", compatibleFramework);
        }

        XElement? packageReference = root.Descendants("PackageReference")
            .FirstOrDefault(element => string.Equals(
                (string?)element.Attribute("Include"), "Nova3D", StringComparison.OrdinalIgnoreCase));
        XElement? projectReference = root.Descendants("ProjectReference")
            .FirstOrDefault(element => string.Equals(
                Path.GetFileNameWithoutExtension((string?)element.Attribute("Include")),
                "Nova3D",
                StringComparison.OrdinalIgnoreCase));
        if (packageReference is not null)
        {
            string version = (string?)packageReference.Attribute("Version")
                ?? packageReference.Element("Version")?.Value
                ?? "centrally managed";
            report.Pass("nova3d-reference", $"PackageReference Nova3D {version}");
        }
        else if (projectReference is not null)
        {
            report.Pass("nova3d-reference", "ProjectReference Nova3D");
        }
        else
        {
            report.Fail("nova3d-reference", "add a PackageReference to Nova3D.");
        }

        string projectDirectory = Path.GetDirectoryName(projectPath)!;
        string assetsPath = Path.Combine(projectDirectory, "obj", "project.assets.json");
        if (File.Exists(assetsPath))
        {
            report.Pass("restore-assets", assetsPath);
        }
        else
        {
            report.Fail("restore-assets", $"missing {assetsPath}; run 'dotnet restore'.");
        }

        XElement[] contentReferences = root.Descendants("MonoGameContentReference").ToArray();
        if (contentReferences.Length == 0)
        {
            report.Warn("content", "no MonoGameContentReference is declared; this is valid for tools and content-free projects.");
            return;
        }

        foreach (XElement reference in contentReferences)
        {
            string? include = (string?)reference.Attribute("Include");
            if (string.IsNullOrWhiteSpace(include))
            {
                report.Fail("content", "MonoGameContentReference has no Include path.");
                continue;
            }

            string contentPath = Path.GetFullPath(Path.Combine(projectDirectory, include));
            if (File.Exists(contentPath))
            {
                report.Pass("content", contentPath);
            }
            else
            {
                report.Fail("content", $"declared MGCB file does not exist: {contentPath}");
            }
        }
    }

    private static bool IsCompatibleFramework(string framework)
    {
        Match match = TargetFrameworkRegex().Match(framework);
        return match.Success && int.Parse(match.Groups["major"].Value) >= RequiredSdkMajor;
    }

    private static string OneLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    [GeneratedRegex(@"^(?<version>(?<major>\d+)\.\d+(?:\.\d+)?(?:-[^\s\[]+)?)\s*\[")]
    private static partial Regex SdkVersionRegex();

    [GeneratedRegex(@"^net(?<major>\d+)\.\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex TargetFrameworkRegex();
}

internal sealed class DoctorReport
{
    private readonly TextWriter output;
    private int checks;
    private int warnings;
    private int errors;

    public DoctorReport(TextWriter output) => this.output = output;

    public void Header() => output.WriteLine("Nova3D doctor");

    public void Pass(string name, string message)
    {
        checks++;
        output.WriteLine($"PASS {name} | {message}");
    }

    public void Warn(string name, string message)
    {
        checks++;
        warnings++;
        output.WriteLine($"WARN {name} | {message}");
    }

    public void Fail(string name, string message)
    {
        checks++;
        errors++;
        output.WriteLine($"FAIL {name} | {message}");
    }

    public int Complete()
    {
        string outcome = errors == 0 ? "PASS" : "FAIL";
        output.WriteLine($"DOCTOR {outcome} | checks {checks} | warnings {warnings} | errors {errors}");
        if (errors > 0)
        {
            output.WriteLine("ACTION | Fix the failed environment or project checks, then run 'nova3d doctor' again.");
        }
        return errors == 0 ? CliExitCodes.Success : CliExitCodes.CommandFailed;
    }
}
