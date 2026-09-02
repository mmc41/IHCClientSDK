
using System;
using System.Collections.Frozen;

namespace Ihc.Vis.Schema
{
    /// <summary>
    /// Which end of a follow-link a pin may occupy. IHC Visual accepts a link on <b>data flow</b>, not on
    /// matching kinds: the source end must produce a signal, the sink end must consume one, and at least one
    /// end must belong to a function block.
    /// <para>
    /// Measured against IHC Visual over a 15-cell matrix (§C5 findings F-058/F-059/F-060; those legal and illegal
    /// cells are pinned by <c>tests/safe_project_tests/projects/LinkLegalityTests.cs</c>)
    /// and corroborated by every follow-link in the vendor-authored corpus (397 links,
    /// 21 projects): a <c>dataline_input</c>/<c>airlink_input</c> is a from-half owner 160/160 times,
    /// <c>resource_output</c> 237/237, while <c>resource_input</c> owns a to-half 314/314 and
    /// <c>dataline_output</c> 83/83. No tag is ever seen in both roles.
    /// </para>
    /// <para>
    /// Only those measured facts are encoded, as <b>negatives</b>. A kind nobody measured stays linkable: the
    /// wireless output family (<c>airlink_relay</c>, <c>airlink_dimming</c>, …) and <c>resource_flag</c> carry
    /// no link in the corpus, and refusing them on a guess would break real wiring that works today.
    /// </para>
    /// </summary>
    internal static class LinkRoles
    {
        /// <summary>Why a pin-to-itself follow-link (id == id) is refused (D06): the vendor never produces one, and a
        /// pin driving itself is not an authorable feedback link. The id equality is checked by the caller (which holds
        /// the ids); this is the shared wording the engine throw uses.</summary>
        internal const string SelfLinkReason = "a pin cannot be both the source and the target of a follow-link";

        /// <summary>
        /// Pins that never own a <c>link_from_resource</c> half: an FB input is a trigger the block consumes,
        /// so it cannot feed anything. (Vendor cells 7/8/T2/T4; 0 of 314 corpus halves.)
        /// </summary>
        private static readonly FrozenSet<string> NeverASource =
            FrozenSet.Create(StringComparer.Ordinal, "resource_input");

        /// <summary>
        /// Pins that never own a <c>link_to_resource</c> half. A product input is a physical button — the world
        /// drives it, software cannot (vendor cell 4; 0 of 160 corpus halves). An FB output is the block's own
        /// result, computed rather than driven (cells 5/6/T3; 0 of 237).
        /// </summary>
        internal static readonly FrozenSet<string> NeverASink =
            FrozenSet.Create(StringComparer.Ordinal, "dataline_input", "airlink_input", "resource_output");

        /// <summary>A function block's own pin — the <c>resource_*</c> family (input, output, flag, …).</summary>
        internal static bool IsFunctionBlockPin(string tag) => tag.StartsWith("resource_", StringComparison.Ordinal);

        /// <summary>
        /// Whether a follow-link may run from <paramref name="sourceTag"/> to <paramref name="sinkTag"/>:
        /// the source must be able to produce, the sink to consume, and one end must be a function-block pin —
        /// IHC routes every product-to-product path through a block, so two product pins never link directly.
        /// </summary>
        internal static bool CanLink(string sourceTag, string sinkTag) =>
            !NeverASource.Contains(sourceTag)
            && !NeverASink.Contains(sinkTag)
            && (IsFunctionBlockPin(sourceTag) || IsFunctionBlockPin(sinkTag));

        /// <summary>Why <see cref="CanLink"/> said no — phrased for a GUI to show the installer.</summary>
        internal static string Explain(string sourceTag, string sinkTag)
        {
            string reason;
            if (NeverASource.Contains(sourceTag))
                reason = $"a '{sourceTag}' cannot be the source of a link — it consumes a signal, it does not produce one";
            else if (NeverASink.Contains(sinkTag))
                reason = $"a '{sinkTag}' cannot be the target of a link — it produces a signal, it does not consume one";
            else if (!IsFunctionBlockPin(sourceTag) && !IsFunctionBlockPin(sinkTag))
                reason = "two product pins cannot be linked directly — connect them through a function block";
            else
                reason = $"'{sourceTag}' cannot be linked to '{sinkTag}'";
            return reason;
        }
    }
}
