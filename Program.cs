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
builder.Services.AddHttpClient<ProductLookupService>();
builder.Services.AddSingleton<CalendarDayInfoParser>();
builder.Services.AddSingleton<SchoolMenuParser>();
builder.Services.AddSingleton<WeatherForecastParser>();
builder.Services.AddSingleton<JsonErrorLogger>();
builder.Services.AddScoped<CalendarDayInfoService>();
builder.Services.AddScoped<SchoolMenuService>();
builder.Services.AddScoped<WeatherForecastService>();
var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<JsonErrorLogger>();
        await logger.LogAsync(context, ex, context.RequestAborted);

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            message = "Ett internt fel uppstod."
        });
    }
});

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

app.MapGet("/api/cellar/items", async Task<IResult> (IConfiguration config, CancellationToken cancellationToken) =>
{
    var cs = config.GetConnectionString("DashboardDb");
    if (string.IsNullOrWhiteSpace(cs))
    {
        return Results.Problem(
            detail: "ConnectionStrings:DashboardDb is not configured.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    await using var conn = new MySqlConnection(cs);
    await conn.OpenAsync(cancellationToken);

    const string sql = """
        SELECT Barcode, ImageUrl, Name, Brand, Quantity
        FROM cellar
        ORDER BY Name, Brand, id
    """;

    await using var cmd = new MySqlCommand(sql, conn);
    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

    var items = new List<CellarInventoryItem>();
    var imageUrlOrdinal = reader.GetOrdinal("ImageUrl");
    while (await reader.ReadAsync(cancellationToken))
    {
        items.Add(new CellarInventoryItem
        {
            Ean = reader.GetString("Barcode"),
            ImageUrl = reader.IsDBNull(imageUrlOrdinal) ? null : reader.GetString("ImageUrl"),
            ProductName = reader.GetString("Name"),
            Brand = reader.GetString("Brand"),
            Quantity = reader.GetInt32("Quantity")
        });
    }

    return Results.Ok(new CellarItemsResponse
    {
        Items = items
    });
})
.WithTags("Cellar");

app.MapPost("/api/cellar/scan", async Task<IResult> (
    CellarScanRequest request,
    IConfiguration config,
    ProductLookupService productLookupService,
    CancellationToken cancellationToken) =>
{
    var ean = request.Ean?.Trim();
    if (!TryNormalizeBarcode(request.Ean, out var normalizedBarcode))
    {
        return Results.BadRequest(new { message = "EAN-kod saknas eller ar ogiltig." });
    }

    var mode = request.Mode?.Trim();
    var isAddMode = string.Equals(mode, "add", StringComparison.OrdinalIgnoreCase);

    var cs = config.GetConnectionString("DashboardDb");
    if (string.IsNullOrWhiteSpace(cs))
    {
        return Results.Problem(
            detail: "ConnectionStrings:DashboardDb is not configured.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    await using var conn = new MySqlConnection(cs);
    await conn.OpenAsync(cancellationToken);
    await using var tx = await conn.BeginTransactionAsync(cancellationToken);

    var existingItem = await GetCellarItemByBarcodeAsync(conn, tx, normalizedBarcode, cancellationToken);

    if (!isAddMode)
    {
        if (existingItem is null)
        {
            return Results.NotFound(new { message = "Produkten finns inte i inventariet." });
        }

        var newQuantity = Math.Max(existingItem.Quantity - 1, 0);

        await UpdateCellarQuantityAsync(conn, tx, existingItem.Ean, newQuantity, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var updatedItem = existingItem with { Quantity = newQuantity };
        var itemsAfterRemove = await GetAllCellarItemsAsync(cs, cancellationToken);
        return Results.Ok(new CellarScanResponse
        {
            Item = updatedItem,
            Items = itemsAfterRemove
        });
    }

    if (existingItem is not null)
    {
        var increasedItem = existingItem with { Quantity = existingItem.Quantity + 1 };
        await UpdateCellarQuantityAsync(conn, tx, existingItem.Ean, increasedItem.Quantity, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var itemsAfterAdd = await GetAllCellarItemsAsync(cs, cancellationToken);
        return Results.Ok(new CellarScanResponse
        {
            Item = increasedItem,
            Items = itemsAfterAdd
        });
    }

    var lookedUpProduct = await productLookupService.LookupByEanAsync(normalizedBarcode, cancellationToken);
    if (lookedUpProduct is null)
    {
        return Results.NotFound(new { message = "Ingen produkt hittades for den skannade EAN-koden." });
    }

    var lookedUpBarcode = NormalizeBarcode(lookedUpProduct.Ean);
    var existingAfterLookup = await GetCellarItemByBarcodeAsync(conn, tx, lookedUpBarcode, cancellationToken);
    if (existingAfterLookup is not null)
    {
        var increasedExistingItem = existingAfterLookup with { Quantity = existingAfterLookup.Quantity + 1 };
        await UpdateCellarQuantityAsync(conn, tx, existingAfterLookup.Ean, increasedExistingItem.Quantity, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var itemsAfterLookupMatch = await GetAllCellarItemsAsync(cs, cancellationToken);
        return Results.Ok(new CellarScanResponse
        {
            Item = increasedExistingItem,
            Items = itemsAfterLookupMatch
        });
    }

    var newItem = new CellarInventoryItem
    {
        Ean = lookedUpBarcode,
        Brand = lookedUpProduct.Brand,
        ProductName = lookedUpProduct.ProductName,
        ImageUrl = lookedUpProduct.ImageUrl,
        Quantity = 1
    };

    await InsertCellarItemAsync(conn, tx, newItem, cancellationToken);
    await tx.CommitAsync(cancellationToken);

    var itemsAfterInsert = await GetAllCellarItemsAsync(cs, cancellationToken);
    return Results.Ok(new CellarScanResponse
    {
        Item = newItem,
        Items = itemsAfterInsert
    });
})
.WithTags("Cellar");

//Hamta Elpris
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

    // 3. Region (hardkodad nu, kan bli parameter senare)
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

static async Task<List<CellarInventoryItem>> GetAllCellarItemsAsync(string connectionString, CancellationToken cancellationToken)
{
    await using var conn = new MySqlConnection(connectionString);
    await conn.OpenAsync(cancellationToken);

    const string sql = """
        SELECT Barcode, ImageUrl, Name, Brand, Quantity
        FROM cellar
        ORDER BY Name, Brand, id
    """;

    await using var cmd = new MySqlCommand(sql, conn);
    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

    var items = new List<CellarInventoryItem>();
    var imageUrlOrdinal = reader.GetOrdinal("ImageUrl");
    while (await reader.ReadAsync(cancellationToken))
    {
        items.Add(new CellarInventoryItem
        {
            Ean = reader.GetString("Barcode"),
            ImageUrl = reader.IsDBNull(imageUrlOrdinal) ? null : reader.GetString("ImageUrl"),
            ProductName = reader.GetString("Name"),
            Brand = reader.GetString("Brand"),
            Quantity = reader.GetInt32("Quantity")
        });
    }

    return items;
}

static async Task<CellarInventoryItem?> GetCellarItemByBarcodeAsync(
    MySqlConnection conn,
    MySqlTransaction transaction,
    string barcode,
    CancellationToken cancellationToken)
{
    var alternateBarcode = GetAlternateBarcode(barcode);
    const string sql = """
        SELECT Barcode, ImageUrl, Name, Brand, Quantity
        FROM cellar
        WHERE Barcode = @barcode OR Barcode = @alternateBarcode
        LIMIT 1
    """;

    await using var cmd = new MySqlCommand(sql, conn, transaction);
    cmd.Parameters.AddWithValue("@barcode", barcode);
    cmd.Parameters.AddWithValue("@alternateBarcode", alternateBarcode);

    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
    if (!await reader.ReadAsync(cancellationToken))
    {
        return null;
    }

    var imageUrlOrdinal = reader.GetOrdinal("ImageUrl");
    return new CellarInventoryItem
    {
        Ean = reader.GetString("Barcode"),
        ImageUrl = reader.IsDBNull(imageUrlOrdinal) ? null : reader.GetString("ImageUrl"),
        ProductName = reader.GetString("Name"),
        Brand = reader.GetString("Brand"),
        Quantity = reader.GetInt32("Quantity")
    };
}

static bool TryNormalizeBarcode(string? value, out string normalizedBarcode)
{
    normalizedBarcode = string.Empty;
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
    if (digitsOnly.Length is not 13 and not 14)
    {
        return false;
    }

    normalizedBarcode = NormalizeBarcode(digitsOnly);
    return true;
}

static string NormalizeBarcode(string barcode)
{
    var digitsOnly = new string(barcode.Where(char.IsDigit).ToArray());
    return digitsOnly.Length == 13 ? $"0{digitsOnly}" : digitsOnly;
}

static string GetAlternateBarcode(string barcode) =>
    barcode.Length == 14 && barcode.StartsWith('0')
        ? barcode[1..]
        : barcode;

static async Task InsertCellarItemAsync(
    MySqlConnection conn,
    MySqlTransaction transaction,
    CellarInventoryItem item,
    CancellationToken cancellationToken)
{
    const string sql = """
        INSERT INTO cellar (Barcode, ImageUrl, Name, Brand, Quantity)
        VALUES (@ean, @imageUrl, @name, @brand, @quantity)
    """;

    await using var cmd = new MySqlCommand(sql, conn, transaction);
    cmd.Parameters.AddWithValue("@ean", item.Ean);
    cmd.Parameters.AddWithValue("@imageUrl", item.ImageUrl);
    cmd.Parameters.AddWithValue("@name", item.ProductName);
    cmd.Parameters.AddWithValue("@brand", item.Brand);
    cmd.Parameters.AddWithValue("@quantity", item.Quantity);
    await cmd.ExecuteNonQueryAsync(cancellationToken);
}

static async Task UpdateCellarQuantityAsync(
    MySqlConnection conn,
    MySqlTransaction transaction,
    string ean,
    int quantity,
    CancellationToken cancellationToken)
{
    const string sql = """
        UPDATE cellar
        SET Quantity = @quantity
        WHERE Barcode = @ean
    """;

    await using var cmd = new MySqlCommand(sql, conn, transaction);
    cmd.Parameters.AddWithValue("@ean", ean);
    cmd.Parameters.AddWithValue("@quantity", quantity);
    await cmd.ExecuteNonQueryAsync(cancellationToken);
}
