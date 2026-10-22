using Notify.Web.Services;

namespace Notify.Web.Endpoints;

public static class HistoryEndpoints
{
    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/history", (HistoryStore history) =>
        {
            var (sent, failed) = history.Counts();
            return Results.Ok(new { items = history.GetAll(), sent, failed });
        });

        app.MapDelete("/api/history", (HistoryStore history) =>
        {
            history.Clear();
            return Results.NoContent();
        });

        return app;
    }
}
