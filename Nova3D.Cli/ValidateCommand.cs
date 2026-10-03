using System.Diagnostics;

internal static class ValidateCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is ["--help"] or ["-h"])
        {
            WriteHelp(output);
            return CliExitCodes.Success;
        }

        string? candidate = null;
        bool noRestore = false;
        foreach (string argument in args)
        {
            if (string.Equals(argument, "--no-restore", StringComparison.OrdinalIgnoreCase))
            {
                if (noRestore)
                {
                    return UsageError("--no-restore was specified more than once.", error);
                }

                noRestore = true;
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

        candidate ??= Environment.CurrentDirectory;
        if (!ProjectLocator.TryResolve(candidate, out string? projectPath, out string? resolutionError))
        {
            error.WriteLine($"VALIDATION FAIL | project | {resolutionError}");
            error.WriteLine("ACTION | Pass a directory containing exactly one project or pass the .csproj path explicitly.");
            return CliExitCodes.CommandFailed;
        }

        string projectDirectory = Path.GetDirectoryName(projectPath)!;
        if (!noRestore)
        {
            int restoreExitCode = RunDotNet(
                "restore",
                new[] { "restore", projectPath!, "--nologo", "--verbosity", "minimal" },
                projectDirectory,
                output,
                error);
            if (restoreExitCode != 0)
            {
                error.WriteLine("VALIDATION FAIL | restore | Fix the reported package or feed error, then run again.");
                error.WriteLine("ACTION | Restore package-feed access or cached packages, then run the command again.");
                return CliExitCodes.CommandFailed;
            }
        }

        int doctorExitCode = DoctorCommand.Run(new[] { projectPath! }, output, error);
        if (doctorExitCode != 0)
        {
            error.WriteLine("VALIDATION FAIL | preflight | Fix the failed doctor checks, then run again.");
            error.WriteLine("ACTION | Run 'nova3d doctor' and correct every failed check before validating again.");
            return CliExitCodes.CommandFailed;
        }

        int buildExitCode = RunDotNet(
            "Release build + MGCB",
            new[]
            {
                "build", projectPath!, "--configuration", "Release", "--no-restore",
                "--nologo", "--verbosity", "minimal"
            },
            projectDirectory,
            output,
            error);
        if (buildExitCode != 0)
        {
            error.WriteLine("VALIDATION FAIL | build | Fix the first compiler or content-pipeline error, then run again.");
            error.WriteLine("ACTION | Correct the first compiler or MGCB error shown above, then validate again.");
            return CliExitCodes.CommandFailed;
        }

        output.WriteLine($"VALIDATION PASS | project {Path.GetFileName(projectPath)} | configuration Release");
        return CliExitCodes.Success;
    }

    internal static int RunDotNet(
        string step,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TextWriter output,
        TextWriter error)
    {
        output.WriteLine($"RUN  {step}");
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            ProcessStartInfo startInfo = new("dotnet")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.Environment["DOTNET_NOLOGO"] = "1";
            startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("dotnet process did not start.");
            process.StandardInput.Close();
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(standardOutput, standardError);

            WriteCaptured(output, standardOutput.Result);
            WriteCaptured(error, standardError.Result);
            stopwatch.Stop();
            if (process.ExitCode == 0)
            {
                output.WriteLine($"PASS {step} ({stopwatch.Elapsed.TotalSeconds:F2}s)");
            }
            else
            {
                error.WriteLine($"FAIL {step} | dotnet exited with code {process.ExitCode} ({stopwatch.Elapsed.TotalSeconds:F2}s)");
            }

            return process.ExitCode;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            error.WriteLine($"FAIL {step} | could not execute dotnet: {OneLine(exception.Message)}");
            return CliExitCodes.CommandFailed;
        }
    }

    private static void WriteCaptured(TextWriter writer, string value)
    {
        string trimmed = value.TrimEnd('\r', '\n');
        if (trimmed.Length > 0)
        {
            writer.WriteLine(trimmed);
        }
    }

    private static int UsageError(string message, TextWriter error)
    {
        error.WriteLine($"Invalid validate usage: {message}");
        error.WriteLine("ACTION | Run 'nova3d validate --help' for usage.");
        return CliExitCodes.UsageError;
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("Usage: nova3d validate [PROJECT|DIRECTORY] [--no-restore]");
        output.WriteLine();
        output.WriteLine("Restores and builds one Nova3D project in Release, including MGCB content.");
        output.WriteLine("Use --no-restore only when the project is already restored.");
    }

    private static string OneLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
