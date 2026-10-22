namespace Notify.Web.Models;

public record NotificationRequest(string? Title, string? Message, int Priority, string[]? Tags);
