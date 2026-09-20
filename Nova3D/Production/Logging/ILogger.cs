namespace Nova3D.Production.Logging;

public interface ILogger
{
    void Log(LogLevel level, string category, string message, Exception? exception = null);
}
