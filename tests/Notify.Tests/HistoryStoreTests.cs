using Notify.Web.Models;
using Notify.Web.Services;

namespace Notify.Tests;

public class HistoryStoreTests
{
    private static HistoryEntry Entry(string title, SendStatus status = SendStatus.Sent, DateTimeOffset? at = null) =>
        new(Guid.NewGuid(), at ?? DateTimeOffset.UtcNow, "topic", title, "msg", 3, [], status, 200, "id", null, null, 10, "manual");

    [Fact]
    public void GetAll_ReturnsNewestFirst()
    {
        var sut = new HistoryStore();
        sut.Add(Entry("first"));
        sut.Add(Entry("second"));
        sut.Add(Entry("third"));

        var titles = sut.GetAll().Select(e => e.Title).ToArray();

        Assert.Equal(["third", "second", "first"], titles);
    }

    [Fact]
    public void Add_TrimsOldestWhenExceedingCapacity()
    {
        var sut = new HistoryStore();
        for (var i = 0; i < 205; i++) sut.Add(Entry($"n{i}"));

        var all = sut.GetAll();

        Assert.Equal(HistoryStore.Capacity, all.Count);
        Assert.Equal(200, all.Count);
        Assert.Equal("n204", all[0].Title);
        Assert.Equal("n5", all[^1].Title);
    }

    [Fact]
    public void Clear_EmptiesTheStore()
    {
        var sut = new HistoryStore();
        sut.Add(Entry("a"));

        sut.Clear();

        Assert.Empty(sut.GetAll());
        Assert.Equal((0, 0), sut.Counts());
    }

    [Fact]
    public void Counts_SplitsSentAndFailed()
    {
        var sut = new HistoryStore();
        sut.Add(Entry("a"));
        sut.Add(Entry("b", SendStatus.Failed));
        sut.Add(Entry("c"));

        Assert.Equal((2, 1), sut.Counts());
    }
}
