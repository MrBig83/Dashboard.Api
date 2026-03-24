using Dashboard.Api.Models;

namespace Dashboard.Api.Services;

public class SchoolMenuService
{
    private readonly SchoolMenuClient _client;
    private readonly SchoolMenuParser _parser;

    public SchoolMenuService(SchoolMenuClient client, SchoolMenuParser parser)
    {
        _client = client;
        _parser = parser;
    }

    public async Task<SchoolMenuResponse> GetWeeklyMenuAsync(CancellationToken cancellationToken = default)
    {
        var xml = await _client.GetWeeklyMenuXmlAsync(cancellationToken);
        return _parser.Parse(xml);
    }
}
