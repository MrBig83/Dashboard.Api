namespace Dashboard.Api.Models;

public record LightStatusDto(
    string EntityId,
    bool IsOn,
    string Friendly_name,
    int? Brightness
);
