namespace Nova3D.Production.Scenes;

/// <summary>Reports a scene JSON failure with document and JSON-path context.</summary>
public sealed class SceneDocumentParseException : Exception
{
    public SceneDocumentParseException(
        string message,
        string documentPath,
        string jsonPath,
        long? lineNumber = null,
        long? bytePositionInLine = null,
        Exception? innerException = null)
        : base(FormatMessage(message, documentPath, jsonPath, lineNumber, bytePositionInLine), innerException)
    {
        DocumentPath = documentPath;
        JsonPath = jsonPath;
        LineNumber = lineNumber;
        BytePositionInLine = bytePositionInLine;
    }

    public string DocumentPath { get; }
    public string JsonPath { get; }
    public long? LineNumber { get; }
    public long? BytePositionInLine { get; }

    private static string FormatMessage(
        string message,
        string documentPath,
        string jsonPath,
        long? lineNumber,
        long? bytePositionInLine)
    {
        var location = lineNumber.HasValue
            ? $" line {lineNumber.Value + 1}, byte {bytePositionInLine.GetValueOrDefault() + 1}"
            : string.Empty;
        return $"Scene document '{documentPath}' failed at '{jsonPath}'{location}: {message}";
    }
}
