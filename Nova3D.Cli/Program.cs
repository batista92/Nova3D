using System.Reflection;

return Nova3DCli.Run(args, Console.Out, Console.Error);

internal static class Nova3DCli
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        CliFormat format = CliFormat.Human;
        if (!CliGlobalOptions.TryParse(args, out string[] commandArgs, ref format, out string? optionError))
        {
            return CliRenderer.Render(
                format,
                "cli",
                CliExitCodes.UsageError,
                string.Empty,
                $"Invalid usage: {optionError}{Environment.NewLine}ACTION | Run 'nova3d --help' for usage.{Environment.NewLine}",
                output,
                error);
        }

        string command = GetCommandName(commandArgs);
        using StringWriter capturedOutput = new(System.Globalization.CultureInfo.InvariantCulture);
        using StringWriter capturedError = new(System.Globalization.CultureInfo.InvariantCulture);
        int exitCode;
        try
        {
            exitCode = Dispatch(commandArgs, capturedOutput, capturedError);
        }
        catch (Exception exception)
        {
            capturedError.WriteLine($"INTERNAL FAIL | cli | {OneLine(exception.Message)}");
            capturedError.WriteLine("ACTION | Re-run with the same arguments and report this output as a Nova3D.Cli defect.");
            exitCode = CliExitCodes.InternalError;
        }

        return CliRenderer.Render(
            format,
            command,
            exitCode,
            capturedOutput.ToString(),
            capturedError.ToString(),
            output,
            error);
    }

    private static int Dispatch(string[] args, TextWriter output, TextWriter error)
    {

        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            WriteHelp(output);
            return CliExitCodes.Success;
        }

        if (args is ["--version"] or ["-v"])
        {
            output.WriteLine(GetVersion());
            return CliExitCodes.Success;
        }

        if (args.Length >= 1 && string.Equals(args[0], "doctor", StringComparison.OrdinalIgnoreCase))
        {
            return DoctorCommand.Run(args[1..], output, error);
        }

        if (args.Length >= 1 && string.Equals(args[0], "validate", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateCommand.Run(args[1..], output, error);
        }

        if (args.Length >= 1 && string.Equals(args[0], "inspect", StringComparison.OrdinalIgnoreCase))
        {
            return InspectCommand.Run(args[1..], output, error);
        }

        if (args.Length >= 1 && string.Equals(args[0], "publish", StringComparison.OrdinalIgnoreCase))
        {
            return PublishCommand.Run(args[1..], output, error);
        }

        if (args.Length >= 1 && string.Equals(args[0], "visual", StringComparison.OrdinalIgnoreCase))
        {
            return VisualCommand.Run(args[1..], output, error);
        }

        if (args.Length >= 1 && string.Equals(args[0], "performance", StringComparison.OrdinalIgnoreCase))
        {
            return PerformanceCommand.Run(args[1..], output, error);
        }

        error.WriteLine($"Unknown command or option '{args[0]}'.");
        error.WriteLine("ACTION | Run 'nova3d --help' for usage.");
        return CliExitCodes.UsageError;
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("Nova3D CLI");
        output.WriteLine();
        output.WriteLine("Usage:");
        output.WriteLine("  nova3d [--help] [--version]");
        output.WriteLine("  nova3d doctor [PROJECT|DIRECTORY]");
        output.WriteLine("  nova3d validate [PROJECT|DIRECTORY] [--no-restore]");
        output.WriteLine("  nova3d inspect <MODEL.glb|SCENE.scene.json>");
        output.WriteLine("  nova3d publish --runtime <RID> [PROJECT|DIRECTORY]");
        output.WriteLine("  nova3d visual compare <CAPTURE.png> --baseline-root <DIRECTORY>");
        output.WriteLine("  nova3d visual update-baseline <CAPTURE.png> --baseline-root <DIRECTORY> --accept");
        output.WriteLine("  nova3d performance check <REPORT.json> --budget <BUDGET.json>");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  -h, --help       Show help");
        output.WriteLine("  -v, --version    Show tool version");
        output.WriteLine("  --format <value> Output format: human (default) or json");
    }

    private static string GetVersion()
    {
        Version version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static string GetCommandName(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
            args.Contains("-h", StringComparer.OrdinalIgnoreCase))
        {
            return "help";
        }
        if (args is ["--version"] or ["-v"])
        {
            return "version";
        }
        return args[0].ToLowerInvariant();
    }

    private static string OneLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
