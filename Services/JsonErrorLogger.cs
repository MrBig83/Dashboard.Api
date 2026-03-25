using System.Text.Json;
using Dashboard.Api.Models;

namespace Dashboard.Api.Services;

public sealed class JsonErrorLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _filePath;

    public JsonErrorLogger(IWebHostEnvironment environment)
    {
        _filePath = Path.Combine(environment.ContentRootPath, "error-log.json");
    }

    public async Task LogAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var entry = new ErrorLogEntry
        {
            TimestampUtc = now,
            Path = context.Request.Path.Value ?? "/",
            Error = exception.Message,
            Hint = CreateHint(exception)
        };

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadEntriesAsync(cancellationToken);
            var cutoff = now.AddDays(-7);
            entries.RemoveAll(x => x.TimestampUtc < cutoff);
            entries.Add(entry);

            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<ErrorLogEntry>> ReadEntriesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var entries = await JsonSerializer.DeserializeAsync<List<ErrorLogEntry>>(stream, JsonOptions, cancellationToken);
        return entries ?? [];
    }

    private static string? CreateHint(Exception exception) =>
        exception switch
        {
            HttpRequestException => "External HTTP request failed.",
            MySqlConnector.MySqlException => "Database operation failed.",
            JsonException => "Response parsing failed.",
            InvalidOperationException => "Unexpected application state.",
            _ => null
        };
}
