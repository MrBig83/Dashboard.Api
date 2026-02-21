using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dashboard.Api.Models;
using Microsoft.Extensions.Configuration;

namespace Dashboard.Api.Services;


public class HomeAssistantService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public HomeAssistantService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _baseUrl = config["HomeAssistant:BaseUrl"]!;
        var token = config["HomeAssistant:Token"]!;

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task ToggleLightAsync(string entityId)
    {
        var url = $"{_baseUrl}/api/services/light/toggle";

        var payload = new
        {
            entity_id = entityId
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IEnumerable<LightStatusDto>> GetAllLightsAsync()
{
    var url = $"{_baseUrl}/api/states";
    var response = await _http.GetAsync(url);
    response.EnsureSuccessStatusCode();

    using var stream = await response.Content.ReadAsStreamAsync();
    using var doc = await JsonDocument.ParseAsync(stream);

    var result = new List<LightStatusDto>();

    foreach (var el in doc.RootElement.EnumerateArray())
    {
        var entityId = el.GetProperty("entity_id").GetString();

        if (!entityId!.StartsWith("light."))
            continue;

        var state = el.GetProperty("state").GetString();
        var Friendly_name = el.GetProperty("attributes").GetProperty("friendly_name").GetString();
        var isOn = state == "on";

        int? brightness = null;

        if (el.TryGetProperty("attributes", out var attrs) &&
            attrs.TryGetProperty("brightness", out var b) &&
            b.ValueKind == JsonValueKind.Number)
        {
            brightness = b.GetInt32();
        }

        result.Add(new LightStatusDto(entityId, isOn, Friendly_name, brightness));
    }

    return result;
}

}
