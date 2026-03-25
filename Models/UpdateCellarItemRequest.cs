namespace Dashboard.Api.Models;

public sealed class UpdateCellarItemRequest
{
    public int Quantity { get; init; }
    public int RestockLevel { get; init; }
}
