using Notify.Web.Models;
using Notify.Web.Services;

namespace Notify.Web.Endpoints;

public static class NotificationEndpoints
{
    private const int MaxTags = 10;

    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/notifications", async (
            NotificationRequest request,
            TopicService topics,
            NtfyPublisher publisher,
            HistoryStore history,
            CancellationToken ct) =>
        {
            var message = request.Message?.Trim();
            if (string.IsNullOrEmpty(message))
                return Results.BadRequest(new { error = "Message is required" });
            if (request.Priority is < 1 or > 5)
                return Results.BadRequest(new { error = "Priority must be between 1 and 5" });

            var result = await PublishAndRecord(
                topics, publisher, history,
                request.Title?.Trim() ?? string.Empty, message, request.Priority, NormalizeTags(request.Tags),
                "manual", ct);
            return Results.Ok(result);
        });

        app.MapPost("/api/simulate/{event}", async (
            string @event,
            SimulatedEvents events,
            TopicService topics,
            NtfyPublisher publisher,
            HistoryStore history,
            CancellationToken ct) =>
        {
            if (!events.TryGet(@event, out var preset))
                return Results.NotFound(new { error = $"Unknown event '{@event}'" });

            var result = await PublishAndRecord(
                topics, publisher, history,
                preset.Title, preset.Message, preset.Priority, preset.Tags,
                @event, ct);
            return Results.Ok(result);
        });

        return app;
    }

    private static string[] NormalizeTags(string[]? tags) =>
        (tags ?? [])
            .Select(t => t?.Trim() ?? string.Empty)
            .Where(t => t.Length > 0)
            .Take(MaxTags)
            .ToArray();

    private static async Task<SendResult> PublishAndRecord(
        TopicService topics, NtfyPublisher publisher, HistoryStore history,
        string title, string message, int priority, string[] tags, string source, CancellationToken ct)
    {
        var topic = topics.Current;
        var sentAt = DateTimeOffset.Now;
        var outcome = await publisher.PublishAsync(topic, title, message, priority, tags, ct);

        var entry = new HistoryEntry(
            Guid.NewGuid(), sentAt, topic, title, message, priority, tags,
            outcome.Success ? SendStatus.Sent : SendStatus.Failed,
            outcome.HttpStatus, outcome.NtfyId, outcome.Error, outcome.RawResponse, outcome.DurationMs, source);
        history.Add(entry);

        return new SendResult(
            outcome.Success, outcome.HttpStatus, outcome.NtfyId, outcome.RawResponse,
            outcome.Error, outcome.DurationMs, entry.Id);
    }
}
