using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ihc
{
    /// <summary>
    /// The one way a wire list becomes a domain list, and the one place the entries it loses are counted.
    /// </summary>
    /// <remarks>
    /// <para>Every list-returning service call maps its answer entry by entry and discards whatever it cannot
    /// map — a null entry the wire left empty, or a mapper that answers null for one. The discard is the
    /// contract and does not change here: a caller asking for a log wants the entries that could be read. What
    /// was missing is any record that the returned list is SHORTER than the answer, so a controller with fewer
    /// users and one whose answer was partly unreadable looked alike.</para>
    /// <para>Shared rather than written per site, because it was written per site: seventeen copies of one LINQ
    /// chain, and a warning added to two of them would have made the absence of a warning mean nothing at the
    /// other fifteen.</para>
    /// <para>A NULL wire array is not this helper's case and is not guarded: the call
    /// sites that can receive one already answer an empty list for it, and swallowing it here would hide the
    /// difference between "the controller sent no list" and "the controller sent an empty one" at the sites
    /// that do not.</para>
    /// </remarks>
    internal static class WireList
    {
        /// <summary>
        /// Every entry of <paramref name="entries"/> that maps, in wire order, with a span warning when that is
        /// fewer than the controller sent.
        /// </summary>
        /// <typeparam name="TWire">The generated SOAP entry type.</typeparam>
        /// <typeparam name="TDomain">The domain type the entry maps to.</typeparam>
        /// <param name="entries">The wire list, whose entries may individually be null.</param>
        /// <param name="map">The per-entry mapping; may answer null for an entry it cannot read.</param>
        /// <param name="activity">The service's span, or null when the host exports no tracing.</param>
        /// <param name="field">The operation being answered, for the warning's <c>field</c> tag.</param>
        internal static List<TDomain> MapPresent<TWire, TDomain>(
            IReadOnlyList<TWire> entries, Func<TWire, TDomain?> map, Activity? activity, string field)
            where TWire : class
            where TDomain : class
        {
            ArgumentNullException.ThrowIfNull(map);

            var mapped = new List<TDomain>(entries.Count);
            foreach (TWire entry in entries)
            {
                if (entry is not null && map(entry) is { } domain)
                {
                    mapped.Add(domain);
                }
            }

            if (mapped.Count != entries.Count)
            {
                activity.AddWarning(
                    $"The controller answered {entries.Count} {field} entries of which {mapped.Count} could be read; "
                    + "the rest were empty and are not in the returned list.",
                    ("type", "DroppedWireEntries"),
                    ("field", field),
                    ("received", entries.Count),
                    ("returned", mapped.Count));
            }

            return mapped;
        }
    }
}
