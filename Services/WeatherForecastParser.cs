using System.Text.Json;
using Dashboard.Api.Models;

namespace Dashboard.Api.Services;

public class WeatherForecastParser
{
    public WeatherForecastResponse Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var timeseries = document.RootElement
            .GetProperty("properties")
            .GetProperty("timeseries")
            .EnumerateArray()
            .Select(entry =>
            {
                var time = entry.TryGetProperty("time", out var timeElement)
                    ? timeElement.GetString() ?? string.Empty
                    : string.Empty;

                double? temperature = null;
                if (entry.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("instant", out var instantElement) &&
                    instantElement.TryGetProperty("details", out var detailsElement) &&
                    detailsElement.TryGetProperty("air_temperature", out var tempElement) &&
                    tempElement.TryGetDouble(out var temp))
                {
                    temperature = temp;
                }

                string? symbolCode = null;
                if (dataElement.TryGetProperty("next_1_hours", out var nextHoursElement) &&
                    nextHoursElement.TryGetProperty("summary", out var summaryElement) &&
                    summaryElement.TryGetProperty("symbol_code", out var symbolElement))
                {
                    symbolCode = symbolElement.GetString();
                }

                return new WeatherForecastEntry(time, temperature, symbolCode);
            })
            .ToArray();

        return new WeatherForecastResponse(timeseries);
    }
}
