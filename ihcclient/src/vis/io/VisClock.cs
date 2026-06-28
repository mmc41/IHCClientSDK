#nullable enable
using System;
using System.Globalization;

namespace Ihc.Vis.Io
{
    /// <summary>
    /// Supplies the current time used to stamp <c>id1</c>/<c>id2</c>/<c>modified</c>, so saves are
    /// deterministic under test. This is a pure clock: the domain distinction between the creation
    /// time (<c>id1</c>, stamped once by <c>CreateNew</c>) and the current-save time (<c>id2</c>,
    /// re-stamped by <c>Save</c>) lives in the service, not here — it reads <see cref="Now"/> at each
    /// of those moments.
    /// </summary>
    public interface IVisClock
    {
        /// <summary>The current point in time.</summary>
        DateTimeOffset Now { get; }
    }

    /// <summary>The default wall-clock implementation of <see cref="IVisClock"/>.</summary>
    public sealed class SystemVisClock : IVisClock
    {
        /// <inheritdoc/>
        public DateTimeOffset Now => DateTimeOffset.Now;
    }

    /// <summary>
    /// A test clock with a settable <see cref="Now"/>, so id-stamping is deterministic. To reproduce a
    /// file whose creation (<c>id1</c>) and save (<c>id2</c>) times differ, set <see cref="Now"/> to
    /// the creation time before <c>CreateNew</c>, then advance it to the save time before <c>Save</c>.
    /// </summary>
    public sealed class TestVisClock : IVisClock
    {
        private const string Format = "yyyy-MM-dd HH:mm:ss";

        /// <summary>Creates a test clock pinned to the given moment.</summary>
        public TestVisClock(DateTimeOffset now) => Now = now;

        /// <summary>Creates a test clock pinned to a <c>yyyy-MM-dd HH:mm:ss</c> timestamp.</summary>
        public TestVisClock(string now)
            : this(DateTimeOffset.ParseExact(now, Format, CultureInfo.InvariantCulture))
        {
        }

        /// <inheritdoc/>
        public DateTimeOffset Now { get; set; }
    }
}
