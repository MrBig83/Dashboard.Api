namespace Dashboard.Api.Models;

public record LightCommand(
    string LampId,
    bool State // true = on, false = off
);
