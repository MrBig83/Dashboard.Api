namespace Dashboard.Api.Services;

public class SchoolMenuClient
{
    private const string SchoolMenuUrl =
        "https://skolmaten.se/api/4/rss/week/diserodsskolan?locale=sv";

    private readonly HttpClient _http;

    public SchoolMenuClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> GetWeeklyMenuXmlAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync(SchoolMenuUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
