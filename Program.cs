using Dashboard.Api.Services;
using Dashboard.Api.Models;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins =
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardCors", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<HomeAssistantService>();
builder.Services.AddHttpClient<CalendarDayInfoClient>();
builder.Services.AddHttpClient<SchoolMenuClient>();
builder.Services.AddHttpClient<WeatherForecastClient>();
builder.Services.AddSingleton<CalendarDayInfoParser>();
builder.Services.AddSingleton<SchoolMenuParser>();
builder.Services.AddSingleton<WeatherForecastParser>();
builder.Services.AddScoped<CalendarDayInfoService>();
builder.Services.AddScoped<SchoolMenuService>();
builder.Services.AddScoped<WeatherForecastService>();
var app = builder.Build();
app.UseCors("DashboardCors");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => "Hello World!");

app.MapGet("/api/status", () => new
{
    name = "Dashboard.Api",
    status = "ok",
    time = DateTime.UtcNow
});

app.MapGet("/api/calendar/today", async Task<IResult> (
    CalendarDayInfoService calendarDayInfoService,
    CancellationToken cancellationToken) =>
{
    var info = await calendarDayInfoService.GetTodayInfoAsync(cancellationToken);
    return Results.Ok(info);
});

app.MapGet("/api/dashboard/overview", () =>
{
    return new
    {
        serverTime = DateTime.UtcNow,
        system = "Dashboard",
        message = "Backend alive and well"
    };
});

app.MapGet("/api/db/ping", async Task<IResult> (IConfiguration config) =>
{
    var cs = config.GetConnectionString("DashboardDb");
    if (string.IsNullOrWhiteSpace(cs))
    {
        return Results.Problem(
            detail: "ConnectionStrings:DashboardDb is not configured.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    await using var conn = new MySqlConnection(cs);
    await conn.OpenAsync();

    await using var cmd = new MySqlCommand("SELECT 1", conn);
    var result = await cmd.ExecuteScalarAsync();

    return Results.Ok(new
    {
        database = "MariaDB",
        result,
        time = DateTime.UtcNow
    });
});

app.MapGet("/api/dashboard/summary", () =>
{
    return new
    {
        title = "Hemma Dashboard",
        status = "Online",
        serverTime = DateTime.UtcNow,
        services = new[]
        {
            new { name = "Dashboard.Api", ok = true },
            new { name = "MariaDB", ok = true }
        }
    };
});

app.MapGet("/api/dashboard/summary-db", async Task<IResult> (IConfiguration config) =>
{
    var cs = config.GetConnectionString("DashboardDb");
    if (string.IsNullOrWhiteSpace(cs))
    {
        return Results.Problem(
            detail: "ConnectionStrings:DashboardDb is not configured.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    await using var conn = new MySqlConnection(cs);
    await conn.OpenAsync();

    const string sql = """
        SELECT title, status, updated_at
        FROM dashboard_status
        ORDER BY id DESC
        LIMIT 1
    """;

    await using var cmd = new MySqlCommand(sql, conn);
    await using var reader = await cmd.ExecuteReaderAsync();

    if (!await reader.ReadAsync())
    {
        return Results.NotFound(new { message = "No dashboard status found" });
    }

    return Results.Ok(new
    {
        title = reader.GetString("title"),
        status = reader.GetString("status"),
        updatedAt = reader.GetDateTime("updated_at")
    });
});

//Hämta Elpris
app.MapGet("/api/school/menu", async Task<IResult> (
    SchoolMenuService schoolMenuService,
    CancellationToken cancellationToken) =>
{
    var menu = await schoolMenuService.GetWeeklyMenuAsync(cancellationToken);
    return Results.Ok(menu);
});

app.MapGet("/api/weather/forecast", async Task<IResult> (
    double lat,
    double lon,
    WeatherForecastService weatherForecastService,
    CancellationToken cancellationToken) =>
{
    var forecast = await weatherForecastService.GetForecastAsync(lat, lon, cancellationToken);
    return Results.Ok(forecast);
});

app.MapGet("/api/elpris/today", async () =>
{
       // 1. Dagens datum (lokal tid)
    var today = DateTime.Today;

    // 2. Bygg datumdelar enligt API:ets format
    var year = today.ToString("yyyy");
    var monthDay = today.ToString("MM-dd");

    // 3. Region (hårdkodad nu, kan bli parameter senare)
    var region = "SE3";

    // 4. Bygg URL
    var url = $"https://www.elprisetjustnu.se/api/v1/prices/{year}/{monthDay}_{region}.json";
    // var url = "https://www.elprisetjustnu.se/api/v1/prices/2026/01-08_SE3.json";
    // var url = "https://www.elprisetjustnu.se/api/v1/prices/SE3/current.json";

    using var http = new HttpClient();
    var response = await http.GetAsync(url);

    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();
    return Results.Content(json, "application/json");
});

app.MapPost("/api/lights/command", async (
    LightCommand command,
    HomeAssistantService haService) =>
{
    Console.WriteLine(
        $"[LIGHT CMD] Toggle lamp: {command.LampId}"
    );

    await haService.ToggleLightAsync(command.LampId);

    return Results.Ok(new
    {
        success = true,
        lampId = command.LampId,
        toggledAt = DateTime.UtcNow
    });
})
.WithTags("Lights");

app.MapGet("/api/lights/status", async (HomeAssistantService haService) =>
{
    var lights = await haService.GetAllLightsAsync();

    return Results.Ok(lights);
})
.WithTags("Lights");




app.Run();
