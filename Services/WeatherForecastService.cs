using Dashboard.Api.Models;

namespace Dashboard.Api.Services;

public class WeatherForecastService
{
    private readonly WeatherForecastClient _client;
    private readonly WeatherForecastParser _parser;

    public WeatherForecastService(WeatherForecastClient client, WeatherForecastParser parser)
    {
        _client = client;
        _parser = parser;
    }

    public async Task<WeatherForecastResponse> GetForecastAsync(
        double lat,
        double lon,
        CancellationToken cancellationToken = default)
    {
        var json = await _client.GetForecastJsonAsync(lat, lon, cancellationToken);
        return _parser.Parse(json);
    }
}
