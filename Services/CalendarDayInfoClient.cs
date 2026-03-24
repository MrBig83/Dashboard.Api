namespace Dashboard.Api.Services;

public class CalendarDayInfoClient
{
    private readonly HttpClient _http;

    public CalendarDayInfoClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> GetTodayInfoJsonAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var url = $"https://sholiday.faboul.se/dagar/v2.1/{date.Year}/{date.Month:D2}/{date.Day:D2}";
        var response = await _http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
