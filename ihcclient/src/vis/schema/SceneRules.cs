using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Ihc.Vis.Schema
{
    /// <summary>
    /// The scene-capable output families and their pinned member kinds (US-024, spec ch. 08 §8.4): relays/sockets
    /// take <c>scene_relay</c>, dimmer regulation takes <c>scene_dimmer</c>. A peer of <see cref="ReciprocalTags"/>
    /// — one table for every consumer (today the editor's LinkScene guard; a validator kind-rule or a GUI's
    /// member-insert menu reads the same fact). Unknown families answer <c>null</c> — the open-world CanInsert
    /// convention: only a known mismatch is a hard error.
    /// </summary>
    internal static class SceneRules
    {
        /// <summary>The member tag the bound output family pins, or <c>null</c> for unknown (permissive) families.</summary>
        /// <remarks>
        /// <see cref="ReciprocalTags.SceneMemberTags"/> lists <c>scene_shutter</c> as a possible member tag, but NO
        /// output family maps to it here (review E4): the peer tables are intentionally out of step. No committed vendor
        /// <c>.vis</c> instances a shutter scene member, so mapping some output family to <c>scene_shutter</c> would be a
        /// guess — and this table encodes only MEASURED facts, answering <c>null</c> (permissive) for the unmeasured,
        /// exactly like the <see cref="LinkRoles"/> convention. A shutter-bound container therefore gets no kind
        /// validation until a real output→<c>scene_shutter</c> binding is observed and added here.
        /// </remarks>
        public static string? PinnedMemberTagFor(string boundOutputTag) =>
            PinnedByOutputTag.TryGetValue(boundOutputTag, out string? pinned) ? pinned : null;

        /// <summary>
        /// The output families this table measures — the domain of <see cref="PinnedMemberTagFor"/>, and therefore
        /// the shipped statement of which product pins a scenario can drive.
        /// <para>
        /// Exposed so the one other consumer that needs the SET rather than the mapping reads it from here instead
        /// of retyping the members. A hand-copied list beside a switch cannot be kept in step by anything; a
        /// derivation is in step by construction.
        /// </para>
        /// </summary>
        public static IReadOnlySet<string> OutputTagsWithPinnedMember => outputTags;

        /// <summary>The measured output-family → pinned-member-tag mapping; see the remarks on the accessor above.</summary>
        private static readonly FrozenDictionary<string, string> PinnedByOutputTag =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dataline_output"] = "scene_relay",
                ["airlink_relay"] = "scene_relay",
                ["airlink_dimming"] = "scene_dimmer",
            }.ToFrozenDictionary(StringComparer.Ordinal);

        // Declared after PinnedByOutputTag on purpose: static initializers run in declaration order, so a set
        // derived from the map has to come second or it reads a null map.
        private static readonly FrozenSet<string> outputTags =
            PinnedByOutputTag.Keys.ToFrozenSet(StringComparer.Ordinal);
    }
}
