using Dashboard.Api.Models;

namespace Dashboard.Api.Services;

public class CalendarDayInfoService
{
    private readonly CalendarDayInfoClient _client;
    private readonly CalendarDayInfoParser _parser;

    public CalendarDayInfoService(CalendarDayInfoClient client, CalendarDayInfoParser parser)
    {
        _client = client;
        _parser = parser;
    }

    public async Task<CalendarDayInfoResponse> GetTodayInfoAsync(CancellationToken cancellationToken = default)
    {
        var json = await _client.GetTodayInfoJsonAsync(DateTime.Today, cancellationToken);
        return _parser.Parse(json);
    }
}
