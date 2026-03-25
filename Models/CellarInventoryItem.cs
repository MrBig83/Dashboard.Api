namespace Dashboard.Api.Models;

public sealed record CellarInventoryItem
{
    public string Ean { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int Quantity { get; init; }
}
