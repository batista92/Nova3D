using System.Text.RegularExpressions;

internal static partial class PublishCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is ["--help"] or ["-h"])
        {
            WriteHelp(output);
            return CliExitCodes.Success;
        }

        string? runtime = null;
        string? candidate = null;
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (string.Equals(argument, "--runtime", StringComparison.OrdinalIgnoreCase))
            {
                if (runtime is not null)
                {
                    return UsageError("--runtime was specified more than once.", error);
                }
                if (++index >= args.Length)
                {
                    return UsageError("--runtime requires a RID such as win-x64.", error);
                }
                runtime = args[index];
            }
            else if (argument.StartsWith("-", StringComparison.Ordinal))
            {
                return UsageError($"unknown option '{argument}'.", error);
            }
            else if (candidate is null)
            {
                candidate = argument;
            }
            else
            {
                return UsageError("only one project or directory may be specified.", error);
            }
        }

        if (runtime is null)
        {
            return UsageError("--runtime is required.", error);
        }
        if (!RuntimeIdentifierRegex().IsMatch(runtime))
        {
            return UsageError(
                $"runtime '{runtime}' is invalid; use a concrete lowercase RID such as win-x64, linux-x64 or osx-arm64.",
                error);
        }

        candidate ??= Environment.CurrentDirectory;
        if (!ProjectLocator.TryResolve(candidate, out string? projectPath, out string? resolutionError))
        {
            error.WriteLine($"PUBLISH FAIL | project | {resolutionError}");
            error.WriteLine("ACTION | Pass a directory containing exactly one project or pass the .csproj path explicitly.");
            return CliExitCodes.CommandFailed;
        }

        string projectDirectory = Path.GetDirectoryName(projectPath)!;
        int restoreExitCode = ValidateCommand.RunDotNet(
            $"restore {runtime}",
            new[] { "restore", projectPath!, "--runtime", runtime, "--nologo", "--verbosity", "minimal" },
            projectDirectory,
            output,
            error);
        if (restoreExitCode != 0)
        {
            error.WriteLine("PUBLISH FAIL | restore | Fix the reported runtime-pack or package-feed error, then run again.");
            error.WriteLine("ACTION | Restore package-feed access or the requested runtime pack, then publish again.");
            return CliExitCodes.CommandFailed;
        }

        int doctorExitCode = DoctorCommand.Run(new[] { projectPath! }, output, error);
        if (doctorExitCode != 0)
        {
            error.WriteLine("PUBLISH FAIL | preflight | Fix the failed doctor checks, then run again.");
            error.WriteLine("ACTION | Run 'nova3d doctor' and correct every failed check before publishing again.");
            return CliExitCodes.CommandFailed;
        }

        string distRoot = Path.GetFullPath(Path.Combine(projectDirectory, "dist"));
        string finalOutput = Path.GetFullPath(Path.Combine(distRoot, runtime));
        EnsureContained(distRoot, finalOutput);
        string stagingOutput = Path.Combine(
            distRoot,
            $".nova3d-publish-{runtime}-{Guid.NewGuid():N}");
        string? previousOutput = null;

        try
        {
            Directory.CreateDirectory(distRoot);
            int publishExitCode = ValidateCommand.RunDotNet(
                $"publish {runtime}",
                new[]
                {
                    "publish", projectPath!, "--configuration", "Release",
                    "--runtime", runtime, "--self-contained", "true", "--no-restore",
                    "--output", stagingOutput, "--nologo", "--verbosity", "minimal"
                },
                projectDirectory,
                output,
                error);
            if (publishExitCode != 0)
            {
                error.WriteLine("PUBLISH FAIL | dotnet | Fix the first build, MGCB or runtime-pack error, then run again.");
                error.WriteLine("ACTION | Correct the first build, MGCB or runtime-pack error shown above, then publish again.");
                return CliExitCodes.CommandFailed;
            }

            string[] files = Directory.Exists(stagingOutput)
                ? Directory.GetFiles(stagingOutput, "*", SearchOption.AllDirectories)
                : Array.Empty<string>();
            if (files.Length == 0)
            {
                error.WriteLine("PUBLISH FAIL | output | dotnet publish produced no files.");
                error.WriteLine("ACTION | Review the project publish items and SDK settings, then publish again.");
                return CliExitCodes.CommandFailed;
            }

            string assemblyName = ReadAssemblyName(projectPath!);
            bool hasEntryPoint = files.Any(file =>
                string.Equals(Path.GetFileName(file), assemblyName + ".exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(file), assemblyName, StringComparison.Ordinal) ||
                string.Equals(Path.GetFileName(file), assemblyName + ".dll", StringComparison.OrdinalIgnoreCase));
            if (!hasEntryPoint)
            {
                error.WriteLine($"PUBLISH FAIL | output | entry point for '{assemblyName}' was not produced.");
                error.WriteLine("ACTION | Ensure the game project has OutputType Exe and a valid entry point.");
                return CliExitCodes.CommandFailed;
            }

            if (Directory.Exists(finalOutput))
            {
                previousOutput = Path.Combine(
                    distRoot,
                    $".nova3d-previous-{runtime}-{Guid.NewGuid():N}");
                Directory.Move(finalOutput, previousOutput);
            }

            try
            {
                Directory.Move(stagingOutput, finalOutput);
            }
            catch
            {
                if (previousOutput is not null && Directory.Exists(previousOutput) &&
                    !Directory.Exists(finalOutput))
                {
                    Directory.Move(previousOutput, finalOutput);
                    previousOutput = null;
                }
                throw;
            }

            if (previousOutput is not null && Directory.Exists(previousOutput))
            {
                Directory.Delete(previousOutput, recursive: true);
                previousOutput = null;
            }

            long bytes = files.Sum(file => new FileInfo(
                Path.Combine(finalOutput, Path.GetRelativePath(stagingOutput, file))).Length);
            output.WriteLine(
                $"PUBLISH PASS | runtime {runtime} | files {files.Length} | bytes {bytes} | {finalOutput}");
            return CliExitCodes.Success;
        }
        catch (Exception exception)
        {
            error.WriteLine($"PUBLISH FAIL | output | {OneLine(exception.Message)}");
            error.WriteLine("ACTION | Check write access to dist/<RID>, close processes using it and publish again.");
            return CliExitCodes.CommandFailed;
        }
        finally
        {
            if (Directory.Exists(stagingOutput))
            {
                Directory.Delete(stagingOutput, recursive: true);
            }
            if (previousOutput is not null && Directory.Exists(previousOutput) &&
                !Directory.Exists(finalOutput))
            {
                Directory.Move(previousOutput, finalOutput);
            }
        }
    }

    private static string ReadAssemblyName(string projectPath)
    {
        using FileStream stream = File.OpenRead(projectPath);
        System.Xml.Linq.XDocument project = System.Xml.Linq.XDocument.Load(stream);
        string? assemblyName = project.Root?
            .Descendants("AssemblyName")
            .Select(element => element.Value.Trim())
            .FirstOrDefault(value => value.Length > 0);
        return assemblyName ?? Path.GetFileNameWithoutExtension(projectPath);
    }

    private static void EnsureContained(string root, string target)
    {
        string rootWithSeparator = Path.EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!target.StartsWith(rootWithSeparator, comparison))
        {
            throw new InvalidOperationException("publish output escaped the project dist directory.");
        }
    }

    private static int UsageError(string message, TextWriter error)
    {
        error.WriteLine($"Invalid publish usage: {message}");
        error.WriteLine("ACTION | Run 'nova3d publish --help' for usage.");
        return CliExitCodes.UsageError;
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("Usage: nova3d publish --runtime <RID> [PROJECT|DIRECTORY]");
        output.WriteLine();
        output.WriteLine("Publishes a self-contained Release build to dist/<RID>.");
        output.WriteLine("The final directory is replaced only after a successful staged publish.");
    }

    private static string OneLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    [GeneratedRegex(@"^[a-z0-9]+(?:[.-][a-z0-9]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeIdentifierRegex();
}
