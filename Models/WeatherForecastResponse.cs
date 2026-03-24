namespace Dashboard.Api.Models;

public record WeatherForecastEntry(
    string Time,
    double? Temperature,
    string? SymbolCode);

public record WeatherForecastResponse(WeatherForecastEntry[] Timeseries);
