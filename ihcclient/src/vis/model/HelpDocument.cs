using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// A parsed help document — a definition summary plus one text per resource <b>display name</b> — as read from a
    /// <c>syn_en*.md</c> sibling by <see cref="Ihc.Vis.Catalog.CatalogDocReader"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> a <see cref="DefinitionDocumentation"/>: a markdown bullet names its resource, while a
    /// definition keys help by position, because a name identifies no single resource (see <c>ResourceDocKey</c>).
    /// This is the reader's honest intermediate — the names it actually has — and
    /// <see cref="Ihc.Vis.Catalog.CatalogReader"/> resolves it against the parsed body, where the positions live. One
    /// type carrying both conventions would read back nothing for half its callers, silently.
    /// <para>A name matched by several resources documents <b>each</b> of them: one bullet is all the document can
    /// say about a repeated name, and giving those pins independent texts means editing the definition, not the
    /// document.</para>
    /// </remarks>
    /// <param name="Summary">The definition-level text (the leading paragraph), or <c>null</c> when none.</param>
    /// <param name="ByName">Per-resource text keyed by the display name its bullet spells; empty when none.</param>
    public sealed record HelpDocument(
        string? Summary,
        ImmutableDictionary<string, string> ByName)
    {
        /// <summary>The empty document — no summary, no bullets.</summary>
        public static HelpDocument Empty { get; } = new(null, ImmutableDictionary<string, string>.Empty);

        /// <summary>True when the document carries neither a summary nor any resource bullet.</summary>
        public bool IsEmpty => Summary is null && ByName.IsEmpty;

        /// <summary>The text the document gives for resource display name <paramref name="resourceName"/>, or
        /// <c>null</c> when it has no bullet for it.</summary>
        public string? ForName(string resourceName) =>
            ByName.TryGetValue(resourceName, out string? text) ? text : null;

        // Same reason as DefinitionDocumentation's pair: an ImmutableDictionary has no value Equals, so the
        // synthesized record equality would compare ByName by REFERENCE and two content-identical documents would
        // come out unequal. Shared with that record through OrdinalStringMap so the rule lives once.
        public bool Equals(HelpDocument? other) =>
            other is not null
            && string.Equals(Summary, other.Summary, StringComparison.Ordinal)
            && OrdinalStringMap.Equals(ByName, other.ByName);

        public override int GetHashCode() => OrdinalStringMap.GetHashCode(Summary, ByName);
    }

    /// <summary>Value equality for the ordinal string→string maps the two help records carry — order-independent
    /// over the entries, since a dictionary has no stable order.</summary>
    internal static class OrdinalStringMap
    {
        public static bool Equals(ImmutableDictionary<string, string> a, ImmutableDictionary<string, string> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (a.Count != b.Count)
            {
                return false;
            }
            foreach (KeyValuePair<string, string> entry in a)
            {
                if (!b.TryGetValue(entry.Key, out string? value) || !string.Equals(value, entry.Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        public static int GetHashCode(string? summary, ImmutableDictionary<string, string> map)
        {
            int hash = summary is null ? 0 : StringComparer.Ordinal.GetHashCode(summary);
            foreach (KeyValuePair<string, string> entry in map)
            {
                // XOR combine so the per-entry contribution is order-independent.
                hash ^= (StringComparer.Ordinal.GetHashCode(entry.Key) * 397) ^ StringComparer.Ordinal.GetHashCode(entry.Value);
            }
            return hash;
        }
    }
}
