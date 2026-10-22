using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Notify.Web.Services;

public record NtfyPublishOutcome(
    bool Success,
    int? HttpStatus,
    string? NtfyId,
    string? RawResponse,
    string? Error,
    long DurationMs);

/// <summary>Publishes to ntfy using the JSON API (POST to the server root with the topic in the body).</summary>
public sealed class NtfyPublisher
{
    private readonly HttpClient _http;
    private readonly ILogger<NtfyPublisher> _logger;

    public NtfyPublisher(HttpClient http, ILogger<NtfyPublisher> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<NtfyPublishOutcome> PublishAsync(
        string topic, string title, string message, int priority, string[] tags,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object> { ["topic"] = topic };
        if (!string.IsNullOrWhiteSpace(title)) payload["title"] = title;
        payload["message"] = message;
        payload["priority"] = priority;
        payload["tags"] = tags;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _http.PostAsJsonAsync("", payload, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            stopwatch.Stop();
            var status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                return new NtfyPublishOutcome(true, status, ReadString(body, "id"), body, null, stopwatch.ElapsedMilliseconds);
            }

            var error = ReadString(body, "error") ?? $"ntfy.sh returned HTTP {status}";
            _logger.LogWarning("ntfy.sh rejected publish: HTTP {Status} {Error}", status, error);
            return new NtfyPublishOutcome(false, status, null, body, error, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller (browser) went away: not a ntfy failure, do not record it as one.
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Could not reach ntfy.sh");
            return new NtfyPublishOutcome(false, null, null, null, $"Could not reach ntfy.sh: {ex.Message}", stopwatch.ElapsedMilliseconds);
        }
    }

    private static string? ReadString(string body, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty(property, out var value)
                   && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
