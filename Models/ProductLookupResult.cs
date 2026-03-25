namespace Dashboard.Api.Models;

public sealed class ProductLookupResult
{
    public string Ean { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
}
