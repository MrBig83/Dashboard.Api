namespace Dashboard.Api.Models;

public sealed class CellarScanResponse
{
    public CellarInventoryItem Item { get; init; } = new();
    public IReadOnlyList<CellarInventoryItem> Items { get; init; } = [];
}
