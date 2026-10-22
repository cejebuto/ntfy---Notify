namespace Notify.Web.Models;

public enum SendStatus { Sent, Failed }

/// <summary>Source is "manual" or the simulated event key (server-down, db-updated, process-completed).</summary>
public record HistoryEntry(
    Guid Id,
    DateTimeOffset SentAt,
    string Topic,
    string Title,
    string Message,
    int Priority,
    string[] Tags,
    SendStatus Status,
    int? HttpStatus,
    string? NtfyId,
    string? Error,
    string? RawResponse,
    long DurationMs,
    string Source);
