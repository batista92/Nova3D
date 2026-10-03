using System.Text.Json;
using System.Text.Json.Serialization;

internal static class CliExitCodes
{
    public const int Success = 0;
    public const int CommandFailed = 1;
    public const int UsageError = 2;
    public const int InternalError = 3;
}

internal enum CliFormat
{
    Human,
    Json
}

internal static class CliGlobalOptions
{
    public static bool TryParse(
        string[] args,
        out string[] commandArgs,
        ref CliFormat format,
        out string? error)
    {
        List<string> remaining = new(args.Length);
        bool formatSeen = false;
        error = null;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            string? value = null;
            if (string.Equals(argument, "--format", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Length)
                {
                    commandArgs = remaining.ToArray();
                    error = "--format requires 'human' or 'json'.";
                    return false;
                }
                value = args[index];
            }
            else if (argument.StartsWith("--format=", StringComparison.OrdinalIgnoreCase))
            {
                value = argument["--format=".Length..];
            }
            else
            {
                remaining.Add(argument);
                continue;
            }

            if (formatSeen)
            {
                commandArgs = remaining.ToArray();
                error = "--format was specified more than once.";
                return false;
            }

            formatSeen = true;
            if (string.Equals(value, "json", StringComparison.OrdinalIgnoreCase))
            {
                format = CliFormat.Json;
            }
            else if (string.Equals(value, "human", StringComparison.OrdinalIgnoreCase))
            {
                format = CliFormat.Human;
            }
            else
            {
                commandArgs = remaining.ToArray();
                error = $"unsupported format '{value}'; use 'human' or 'json'.";
                return false;
            }
        }

        commandArgs = remaining.ToArray();
        return true;
    }
}

internal static class CliRenderer
{
    private const int SchemaVersion = 1;

    public static int Render(
        CliFormat format,
        string command,
        int exitCode,
        string standardOutput,
        string standardError,
        TextWriter output,
        TextWriter error)
    {
        List<CliLine> lines = ParseLines(standardOutput, "stdout")
            .Concat(ParseLines(standardError, "stderr"))
            .ToList();
        string[] actions = lines
            .Where(line => line.Kind == "action")
            .Select(line => line.Message)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (exitCode != CliExitCodes.Success && actions.Length == 0)
        {
            actions = new[] { exitCode == CliExitCodes.UsageError
                ? "Run 'nova3d --help' for usage."
                : "Review the reported diagnostics, correct the first failure and run the command again." };
        }

        if (format == CliFormat.Json)
        {
            CliJsonResult result = new(
                SchemaVersion,
                command,
                Status(exitCode),
                exitCode,
                FindSummary(lines, exitCode),
                lines.Where(line => line.Kind != "action").Select(line => new CliJsonMessage(
                    line.Kind,
                    line.Code,
                    line.Message,
                    line.Stream)).ToArray(),
                actions);
            output.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return exitCode;
        }

        if (exitCode == CliExitCodes.Success && command is not ("help" or "version"))
        {
            foreach (CliLine line in lines.Where(KeepSuccessfulHumanLine))
            {
                output.WriteLine(line.Original);
            }
        }
        else
        {
            WriteOriginal(output, standardOutput);
            WriteOriginal(error, standardError);
        }

        return exitCode;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static bool KeepSuccessfulHumanLine(CliLine line) =>
        line.Original.StartsWith("WARN ", StringComparison.Ordinal) ||
        line.Original.StartsWith("INFO ", StringComparison.Ordinal) ||
        line.Original.StartsWith("RUN  ", StringComparison.Ordinal) ||
        IsSummary(line.Original);

    private static string FindSummary(IReadOnlyList<CliLine> lines, int exitCode) =>
        lines.LastOrDefault(line => IsSummary(line.Original))?.Original
        ?? lines.FirstOrDefault(line => line.Kind is "error" or "warning")?.Message
        ?? (exitCode == CliExitCodes.Success ? "Command completed successfully." : "Command failed.");

    private static bool IsSummary(string line) =>
        line.StartsWith("DOCTOR ", StringComparison.Ordinal) ||
        line.StartsWith("VALIDATION ", StringComparison.Ordinal) ||
        line.StartsWith("INSPECTION ", StringComparison.Ordinal) ||
        line.StartsWith("PUBLISH ", StringComparison.Ordinal) ||
        line.StartsWith("VISUAL ", StringComparison.Ordinal) ||
        line.StartsWith("PERFORMANCE ", StringComparison.Ordinal);

    private static string Status(int exitCode) => exitCode switch
    {
        CliExitCodes.Success => "pass",
        CliExitCodes.CommandFailed => "fail",
        CliExitCodes.UsageError => "usage_error",
        _ => "internal_error"
    };

    private static IEnumerable<CliLine> ParseLines(string value, string stream)
    {
        using StringReader reader = new(value);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            string kind = Classify(line, stream);
            string? code = null;
            string message = line;
            int separator = line.IndexOf('|');
            if (separator >= 0)
            {
                string prefix = line[..separator].Trim();
                message = line[(separator + 1)..].Trim();
                string[] prefixParts = prefix.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (prefixParts.Length > 1 && prefixParts[0] is "PASS" or "WARN" or "FAIL" or "INFO")
                {
                    code = string.Join('-', prefixParts.Skip(1)).ToLowerInvariant();
                }
                else if (prefixParts.Length > 1 && prefixParts[^1] is "PASS" or "FAIL")
                {
                    int detailSeparator = message.IndexOf('|');
                    if (detailSeparator >= 0)
                    {
                        code = message[..detailSeparator].Trim().ToLowerInvariant();
                        message = message[(detailSeparator + 1)..].Trim();
                    }
                }
            }
            else if (line.StartsWith("ACTION |", StringComparison.Ordinal))
            {
                message = line["ACTION |".Length..].Trim();
            }

            yield return new CliLine(kind, code, message, stream, line);
        }
    }

    private static string Classify(string line, string stream)
    {
        if (line.StartsWith("ACTION |", StringComparison.Ordinal)) return "action";
        if (line.StartsWith("WARN ", StringComparison.Ordinal) || line.Contains(": warning ", StringComparison.OrdinalIgnoreCase)) return "warning";
        if (line.StartsWith("FAIL ", StringComparison.Ordinal) || line.Contains(": error ", StringComparison.OrdinalIgnoreCase)) return "error";
        if (line.StartsWith("PASS ", StringComparison.Ordinal)) return "success";
        if (line.StartsWith("INFO ", StringComparison.Ordinal) || line.StartsWith("RUN  ", StringComparison.Ordinal)) return "info";
        return stream == "stderr" ? "error" : "detail";
    }

    private static void WriteOriginal(TextWriter writer, string value)
    {
        string trimmed = value.TrimEnd('\r', '\n');
        if (trimmed.Length > 0)
        {
            writer.WriteLine(trimmed);
        }
    }

    private sealed record CliLine(string Kind, string? Code, string Message, string Stream, string Original);
    private sealed record CliJsonResult(
        int SchemaVersion,
        string Command,
        string Status,
        int ExitCode,
        string Summary,
        CliJsonMessage[] Messages,
        string[] RecommendedActions);
    private sealed record CliJsonMessage(string Level, string? Code, string Message, string Stream);
}
