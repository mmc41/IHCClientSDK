using System.Collections.Immutable;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The one rule every long poll follows: with something queued it answers at once; otherwise it holds for the
    /// client's timeout, at most <see cref="LongestHold"/>, waking early when something arrives. The hold is an
    /// await, never a held lock, so every other call is answered while a poll is open.
    /// </summary>
    internal static class LongPoll
    {
        /// <summary>The longest an idle poll is held. A client polls in a loop, so answering an idle poll at once
        /// would make it spin; a real controller holds for the client's whole timeout, which a test would wait out.</summary>
        private static readonly TimeSpan LongestHold = TimeSpan.FromSeconds(1);

        /// <summary>The oldest item in <paramref name="queue"/> once the hold ends, or null when nothing came.</summary>
        internal static async Task<T?> TakeOne<T>(PendingQueue<T> queue, int? timeoutSeconds) where T : class
        {
            await Hold(queue.Queued(), timeoutSeconds).ConfigureAwait(false);
            return queue.TakeOne();
        }

        /// <summary>Everything in <paramref name="queue"/> once the hold ends, which is empty when nothing came.</summary>
        internal static async Task<ImmutableList<T>> TakeAll<T>(PendingQueue<T> queue, int? timeoutSeconds) where T : class
        {
            await Hold(queue.Queued(), timeoutSeconds).ConfigureAwait(false);
            return queue.TakeAll();
        }

        private static async Task Hold(Task queued, int? timeoutSeconds)
        {
            if (!queued.IsCompleted)
            {
                TimeSpan hold = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds ?? 0, 0, LongestHold.TotalSeconds));
                await Task.WhenAny(queued, Task.Delay(hold)).ConfigureAwait(false);
            }
        }
    }
}
