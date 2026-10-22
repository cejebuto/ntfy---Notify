using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Notify.Web.Services;

namespace Notify.Tests;

public class NtfyPublisherTests
{
    private static readonly Uri BaseUri = new("https://ntfy.example.test/");

    private static NtfyPublisher Create(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = BaseUri }, NullLogger<NtfyPublisher>.Instance);

    [Fact]
    public async Task PublishAsync_PostsJsonToBaseAddressWithExpectedBody()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, """{"id":"abc123","event":"message"}""");
        var sut = Create(handler);

        await sut.PublishAsync("my-topic", "Hello", "World", 4, ["warning", "rocket"]);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(BaseUri, handler.LastRequest.RequestUri);
        Assert.Equal("application/json", handler.LastContentType);

        using var doc = JsonDocument.Parse(handler.LastBody!);
        var root = doc.RootElement;
        Assert.Equal("my-topic", root.GetProperty("topic").GetString());
        Assert.Equal("Hello", root.GetProperty("title").GetString());
        Assert.Equal("World", root.GetProperty("message").GetString());
        Assert.Equal(4, root.GetProperty("priority").GetInt32());
        Assert.Equal(new[] { "warning", "rocket" }, root.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!).ToArray());
    }

    [Fact]
    public async Task PublishAsync_OmitsTitleWhenEmptyAndAlwaysSendsTagsArray()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, """{"id":"x"}""");
        var sut = Create(handler);

        await sut.PublishAsync("t", "", "msg", 3, []);

        using var doc = JsonDocument.Parse(handler.LastBody!);
        Assert.False(doc.RootElement.TryGetProperty("title", out _));
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("tags").ValueKind);
    }

    [Fact]
    public async Task PublishAsync_OnSuccessReturnsNtfyId()
    {
        var body = """{"id":"abc123","time":1673542291,"event":"message","topic":"t"}""";
        var sut = Create(FakeHttpMessageHandler.Returning(HttpStatusCode.OK, body));

        var outcome = await sut.PublishAsync("t", "T", "M", 3, []);

        Assert.True(outcome.Success);
        Assert.Equal(200, outcome.HttpStatus);
        Assert.Equal("abc123", outcome.NtfyId);
        Assert.Equal(body, outcome.RawResponse);
        Assert.Null(outcome.Error);
    }

    [Fact]
    public async Task PublishAsync_OnRateLimitReturnsNtfyError()
    {
        var body = """{"code":42901,"http":429,"error":"limit reached","link":"https://ntfy.sh/docs/publish/#limitations"}""";
        var sut = Create(FakeHttpMessageHandler.Returning(HttpStatusCode.TooManyRequests, body));

        var outcome = await sut.PublishAsync("t", "T", "M", 3, []);

        Assert.False(outcome.Success);
        Assert.Equal(429, outcome.HttpStatus);
        Assert.Equal("limit reached", outcome.Error);
        Assert.Equal(body, outcome.RawResponse);
    }

    [Fact]
    public async Task PublishAsync_OnNonJsonErrorFallsBackToGenericMessage()
    {
        var sut = Create(FakeHttpMessageHandler.Returning(HttpStatusCode.BadGateway, "<html>bad gateway</html>"));

        var outcome = await sut.PublishAsync("t", "T", "M", 3, []);

        Assert.False(outcome.Success);
        Assert.Equal(502, outcome.HttpStatus);
        Assert.Equal("ntfy.sh returned HTTP 502", outcome.Error);
    }

    [Fact]
    public async Task PublishAsync_WhenNetworkFailsNeverThrows()
    {
        var sut = Create(FakeHttpMessageHandler.Throwing(new HttpRequestException("dns failure")));

        var outcome = await sut.PublishAsync("t", "T", "M", 3, []);

        Assert.False(outcome.Success);
        Assert.Null(outcome.HttpStatus);
        Assert.Contains("Could not reach", outcome.Error);
        Assert.Contains("dns failure", outcome.Error);
    }

    [Fact]
    public async Task PublishAsync_WhenTimeoutNeverThrows()
    {
        var sut = Create(FakeHttpMessageHandler.Throwing(new TaskCanceledException("timeout")));

        var outcome = await sut.PublishAsync("t", "T", "M", 3, []);

        Assert.False(outcome.Success);
        Assert.Null(outcome.HttpStatus);
        Assert.Contains("Could not reach", outcome.Error);
    }
}
