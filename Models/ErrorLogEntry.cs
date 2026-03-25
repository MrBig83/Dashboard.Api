namespace Dashboard.Api.Models;

public sealed class ErrorLogEntry
{
    public DateTime TimestampUtc { get; init; }
    public string Path { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public string? Hint { get; init; }
}
