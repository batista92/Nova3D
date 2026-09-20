using System.Diagnostics;
using System.Text;

namespace Nova3D.Production.Logging;

public sealed class FileLogger : ILogger, IDisposable
{
    private readonly object _sync = new();
    private readonly StreamWriter _writer;

    public FileLogger(string path, LogLevel minimumLevel = LogLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        _writer = new StreamWriter(fullPath, append: true, Encoding.UTF8) { AutoFlush = true };
        MinimumLevel = minimumLevel;
        Path = fullPath;
    }

    public string Path { get; }
    public LogLevel MinimumLevel { get; set; }

    public void Log(LogLevel level, string category, string message, Exception? exception = null)
    {
        if (level < MinimumLevel) return;
        var line = $"{DateTimeOffset.Now:O} [{level}] [{category}] {message}";
        lock (_sync)
        {
            _writer.WriteLine(line);
            if (exception is not null) _writer.WriteLine(exception);
        }
        Debug.WriteLine(line);
    }

    public void Dispose()
    {
        lock (_sync) _writer.Dispose();
    }
}
