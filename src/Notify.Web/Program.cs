using System.Text.Json.Serialization;
using Notify.Web.Endpoints;
using Notify.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Loopback only by default; Docker injects ASPNETCORE_URLS=http://+:4444.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://localhost:4444");
}

builder.Services.AddSingleton<TopicService>();
builder.Services.AddSingleton<HistoryStore>();
builder.Services.AddSingleton<SimulatedEvents>();

var cfg = builder.Configuration;
builder.Services.AddHttpClient<NtfyPublisher>(c =>
{
    c.BaseAddress = new Uri(cfg["Ntfy:BaseUrl"] ?? "https://ntfy.sh/");
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Notify-ntfy-tester/1.0");
});

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// Reject cross-site mutating calls: browsers always send Origin on non-GET fetches,
// so a request from any other page (or a DNS-rebound host) is refused.
app.Use(async (ctx, next) =>
{
    var isMutatingApi = ctx.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(ctx.Request.Method);
    if (isMutatingApi && ctx.Request.Headers.TryGetValue("Origin", out var origin))
    {
        var self = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
        if (!string.Equals(origin, self, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = "Cross-origin requests are not allowed" });
            return;
        }
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.Now }));
app.MapConfigEndpoints();
app.MapNotificationEndpoints();
app.MapHistoryEndpoints();

app.Run();
