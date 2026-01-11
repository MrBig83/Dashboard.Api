using MySqlConnector;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardCors", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",   // Vite / dev
                "http://localhost:3000"    // ev annan dev
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseCors("DashboardCors");




var connectionString = builder.Configuration.GetConnectionString("DashboardDb");

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

app.MapGet("/api/dashboard/overview", () =>
{
    return new
    {
        serverTime = DateTime.UtcNow,
        system = "Dashboard",
        message = "Backend alive and well"
    };
});

app.MapGet("/api/db/ping", async (IConfiguration config) =>
{
    var cs = config.GetConnectionString("DashboardDb");

    await using var conn = new MySqlConnection(cs);
    await conn.OpenAsync();

    await using var cmd = new MySqlCommand("SELECT 1", conn);
    var result = await cmd.ExecuteScalarAsync();

    return new
    {
        database = "MariaDB",
        result,
        time = DateTime.UtcNow
    };
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

app.MapGet("/api/dashboard/summary-db", async (IConfiguration config) =>
{
    var cs = config.GetConnectionString("DashboardDb");

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


app.Run();
