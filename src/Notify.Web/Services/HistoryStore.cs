using Notify.Web.Models;

namespace Notify.Web.Services;

/// <summary>In-memory, bounded history of sent notifications (newest first).</summary>
public sealed class HistoryStore
{
    public const int Capacity = 200;

    private readonly object _lock = new();
    private readonly LinkedList<HistoryEntry> _entries = new();

    public void Add(HistoryEntry entry)
    {
        lock (_lock)
        {
            _entries.AddFirst(entry);
            while (_entries.Count > Capacity) _entries.RemoveLast();
        }
    }

    public IReadOnlyList<HistoryEntry> GetAll()
    {
        lock (_lock) return _entries.ToArray();
    }

    public void Clear()
    {
        lock (_lock) _entries.Clear();
    }

    public (int sent, int failed) Counts()
    {
        lock (_lock)
        {
            var failed = _entries.Count(e => e.Status == SendStatus.Failed);
            return (_entries.Count - failed, failed);
        }
    }
}
