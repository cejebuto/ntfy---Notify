using Notify.Web.Services;

namespace Notify.Tests;

public class TopicServiceTests
{
    [Fact]
    public void Generate_ReturnsPrefixedTopicOfExpectedLength()
    {
        var topic = TopicService.Generate();

        Assert.StartsWith("notify-", topic);
        Assert.Equal(31, topic.Length);
        Assert.Matches(TopicService.TopicRegex, topic);
    }

    [Fact]
    public void Generate_TwoCallsProduceDifferentTopics()
    {
        Assert.NotEqual(TopicService.Generate(), TopicService.Generate());
    }

    [Fact]
    public void Current_IsInitializedWithValidTopic()
    {
        var sut = new TopicService();

        Assert.Matches(TopicService.TopicRegex, sut.Current);
    }

    [Fact]
    public void Regenerate_ChangesCurrentAndReturnsIt()
    {
        var sut = new TopicService();
        var before = sut.Current;

        var after = sut.Regenerate();

        Assert.NotEqual(before, after);
        Assert.Equal(after, sut.Current);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TrySet_RejectsEmptyValues(string? value)
    {
        var sut = new TopicService();
        var before = sut.Current;

        var ok = sut.TrySet(value, out var error);

        Assert.False(ok);
        Assert.Equal("Topic is required", error);
        Assert.Equal(before, sut.Current);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("ñandu")]
    public void TrySet_RejectsInvalidCharacters(string value)
    {
        var sut = new TopicService();

        var ok = sut.TrySet(value, out var error);

        Assert.False(ok);
        Assert.Contains("letters, numbers, dashes and underscores", error);
    }

    [Fact]
    public void TrySet_RejectsTooLongTopic()
    {
        var sut = new TopicService();

        var ok = sut.TrySet(new string('a', 65), out var error);

        Assert.False(ok);
        Assert.Contains("max 64", error);
    }

    [Fact]
    public void TrySet_AcceptsValidTopicAndTrims()
    {
        var sut = new TopicService();

        var ok = sut.TrySet("  my_topic-1 ", out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal("my_topic-1", sut.Current);
    }
}
