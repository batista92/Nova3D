internal static class ProjectLocator
{
    public static bool TryResolve(string candidate, out string? projectPath, out string? error)
    {
        projectPath = null;
        error = null;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(candidate);
        }
        catch (Exception exception)
        {
            error = $"invalid path: {OneLine(exception.Message)}";
            return false;
        }

        if (File.Exists(fullPath))
        {
            if (!string.Equals(Path.GetExtension(fullPath), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                error = $"expected a .csproj file: {fullPath}";
                return false;
            }

            projectPath = fullPath;
            return true;
        }

        if (!Directory.Exists(fullPath))
        {
            error = $"path does not exist: {fullPath}";
            return false;
        }

        string[] projects = Directory.GetFiles(fullPath, "*.csproj", SearchOption.TopDirectoryOnly);
        if (projects.Length == 0)
        {
            error = $"no .csproj found in {fullPath}; pass the project path explicitly.";
            return false;
        }

        if (projects.Length > 1)
        {
            error = $"multiple .csproj files found in {fullPath}; pass one explicitly.";
            return false;
        }

        projectPath = projects[0];
        return true;
    }

    private static string OneLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
