namespace Notify.Web.Models;

public record SendResult(
    bool Success,
    int? HttpStatus,
    string? NtfyId,
    string? RawResponse,
    string? Error,
    long DurationMs,
    Guid HistoryId);
