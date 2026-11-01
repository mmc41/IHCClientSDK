using System.Collections.Immutable;

namespace Ihc.Stubbing
{
    /// <summary>
    /// What a long poll has not reported yet, oldest first, with a signal that wakes a poll holding for the next
    /// item. It locks the stub's one gate, which is reentrant, so a group can queue inside its own hold of it.
    /// </summary>
    internal sealed class PendingQueue<T>(Lock gate) where T : class
    {
        private ImmutableList<T> _items = [];

        /// <summary>Completed when an item is queued, so a poll that is holding answers at once.</summary>
        private TaskCompletionSource _added = NewSignal();

        internal ImmutableList<T> Items => Gated.Read(gate, ref _items);

        /// <summary>Replaces what is queued, waking a holding poll when anything is.</summary>
        internal void Replace(IEnumerable<T> items)
        {
            lock (gate)
            {
                _items = [];
                Add(items);
            }
        }

        internal void Add(IEnumerable<T> items)
        {
            lock (gate)
            {
                _items = _items.AddRange(items);
                if (!_items.IsEmpty)
                {
                    _added.TrySetResult();
                    _added = NewSignal();
                }
            }
        }

        /// <summary>Completed at once while something is queued; otherwise when the next item is.</summary>
        internal Task Queued()
        {
            lock (gate)
            {
                return _items.IsEmpty ? _added.Task : Task.CompletedTask;
            }
        }

        /// <summary>Everything queued, which a poll reports once.</summary>
        internal ImmutableList<T> TakeAll()
        {
            lock (gate)
            {
                ImmutableList<T> items = _items;
                _items = [];
                return items;
            }
        }

        /// <summary>The oldest item, which a poll reports once, or null when nothing is queued.</summary>
        internal T? TakeOne()
        {
            lock (gate)
            {
                if (_items.IsEmpty)
                {
                    return null;
                }
                T item = _items[0];
                _items = _items.RemoveAt(0);
                return item;
            }
        }

        /// <summary>A poll resumes off the gate, so queuing never runs a poll's answer while it holds the lock.</summary>
        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
