using Notify.Web.Models;
using Notify.Web.Services;

namespace Notify.Web.Endpoints;

public static class ConfigEndpoints
{
    public record SetTopicRequest(string? Topic);

    public static IEndpointRouteBuilder MapConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/config");

        group.MapGet("", (TopicService topics, IConfiguration cfg) => Results.Ok(Build(topics.Current, cfg)));

        group.MapPut("", (SetTopicRequest request, TopicService topics, IConfiguration cfg) =>
            topics.TrySet(request.Topic, out var error)
                ? Results.Ok(Build(topics.Current, cfg))
                : Results.BadRequest(new { error }));

        group.MapPost("/regenerate", (TopicService topics, IConfiguration cfg) =>
            Results.Ok(Build(topics.Regenerate(), cfg)));

        return app;
    }

    private static TopicConfig Build(string topic, IConfiguration cfg)
    {
        var baseUrl = cfg["Ntfy:BaseUrl"] ?? "https://ntfy.sh/";
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        return new TopicConfig(topic, baseUrl, baseUrl + topic);
    }
}
