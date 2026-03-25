namespace Dashboard.Api.Models;

public sealed class CellarScanRequest
{
    public string? Ean { get; init; }
    public string? Mode { get; init; }
}
