namespace Dashboard.Api.Models;

public sealed class CellarItemsResponse
{
    public IReadOnlyList<CellarInventoryItem> Items { get; init; } = [];
}
