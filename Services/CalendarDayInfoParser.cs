using System.Text.Json;
using Dashboard.Api.Models;

namespace Dashboard.Api.Services;

public class CalendarDayInfoParser
{
    public CalendarDayInfoResponse Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var day = document.RootElement
            .GetProperty("dagar")[0];

        var names = day.TryGetProperty("namnsdag", out var namesElement) &&
                    namesElement.ValueKind == JsonValueKind.Array
            ? namesElement.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!)
                .ToArray()
            : [];

        var holiday = day.TryGetProperty("helgdag", out var holidayElement) &&
                      holidayElement.ValueKind == JsonValueKind.String
            ? holidayElement.GetString() ?? string.Empty
            : string.Empty;

        var isRedDay = day.TryGetProperty("röd dag", out var redDayElement)
            && string.Equals(redDayElement.GetString(), "Ja", StringComparison.OrdinalIgnoreCase);

        var resolvedHoliday = !string.IsNullOrWhiteSpace(holiday)
            ? holiday
            : isRedDay ? "Röd dag" : string.Empty;

        return new CalendarDayInfoResponse(names, resolvedHoliday);
    }
}
