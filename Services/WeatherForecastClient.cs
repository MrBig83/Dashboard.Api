using System.Globalization;

namespace Dashboard.Api.Services;

public class WeatherForecastClient
{
    private readonly HttpClient _http;

    public WeatherForecastClient(HttpClient http)
    {
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Dashboard.Api/1.0");
    }

    public async Task<string> GetForecastJsonAsync(double lat, double lon, CancellationToken cancellationToken = default)
    {
        var latText = lat.ToString(CultureInfo.InvariantCulture);
        var lonText = lon.ToString(CultureInfo.InvariantCulture);
        var url = $"https://api.met.no/weatherapi/locationforecast/2.0/compact?lat={latText}&lon={lonText}";
        var response = await _http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
