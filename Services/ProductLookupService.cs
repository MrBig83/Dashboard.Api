using System.Text;
using System.Text.Json;
using Dashboard.Api.Models;

namespace Dashboard.Api.Services;

public sealed class ProductLookupService(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProductLookupResult?> LookupByEanAsync(string ean, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://productsearch.gs1.se/foodservice/tradeItem/search");

        request.Headers.Accept.ParseAdd("application/json");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                query = ean,
                sortby = 0,
                sortDirection = 0
            }, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var product = FindFirstTradeItem(document.RootElement);
        if (product is null)
        {
            return null;
        }

        var eanValue = GetFirstString(product.Value, "gtin", "ean", "barcode");
        var productName = GetFirstString(
            product.Value,
            "descriptionShort",
            "functionalName",
            "productName",
            "name",
            "description");
        var brand = GetFirstString(product.Value, "brandName", "brand", "brandOwnerName");
        var imageUrl = GetImageUrl(product.Value);

        if (string.IsNullOrWhiteSpace(eanValue) || string.IsNullOrWhiteSpace(productName))
        {
            return null;
        }

        return new ProductLookupResult
        {
            Ean = eanValue,
            Brand = brand ?? string.Empty,
            ProductName = productName,
            ImageUrl = imageUrl
        };
    }

    private static JsonElement? FindFirstTradeItem(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var looksLikeTradeItem =
                HasProperty(element, "gtin") ||
                HasProperty(element, "functionalName") ||
                HasProperty(element, "brandName") ||
                HasProperty(element, "productName");

            if (looksLikeTradeItem)
            {
                return element;
            }

            foreach (var property in element.EnumerateObject())
            {
                var found = FindFirstTradeItem(property.Value);
                if (found is not null)
                {
                    return found;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = FindFirstTradeItem(item);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static string? GetImageUrl(JsonElement element)
    {
        var direct = GetFirstString(element, "thumbnail", "imageUrl", "image", "imageKey");
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        if (TryGetPropertyIgnoreCase(element, "images", out var images) && images.ValueKind == JsonValueKind.Array)
        {
            foreach (var image in images.EnumerateArray())
            {
                var url = GetFirstString(image, "thumbnail", "imageUrl", "url", "image", "imageKey");
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }
        }

        return null;
    }

    private static string? GetFirstString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetPropertyIgnoreCase(element, propertyName, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        return item.GetString();
                    }
                }
            }
        }

        return null;
    }

    private static bool HasProperty(JsonElement element, string propertyName) =>
        TryGetPropertyIgnoreCase(element, propertyName, out _);

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
