#nullable enable
namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The capability limits of a TARGET CONTROLLER — facts about the machine a project is destined for, which
    /// the project file itself cannot supply.
    /// <para>
    /// A rule that needs these is not evaluated unless a caller supplies them. That is an EVALUABILITY axis, not
    /// a strictness one: a verdict that depends on the machine is not a property of the project file, so such a
    /// rule is absent from a project-only run rather than evaluated against a guess.
    /// </para>
    /// </summary>
    /// <param name="InputModules">
    /// Input data LINES — modules, not terminals — the controller carries. Vendor datasheet: 8. The distinction is
    /// the one that forced <c>capacity-modules-exceeded</c> to split: a module holds many terminals, and a count of
    /// one read as the other is a sentence that says the wrong unit beside a number.
    /// </param>
    /// <param name="OutputModules">Output data LINES the controller carries. Vendor datasheet: 16.</param>
    /// <param name="AddressesPerDirection">
    /// Maximum addresses per direction. Vendor datasheet: 128, corroborated by the authoring bound — the address
    /// chooser offers 1–8 input and 1–16 output modules, and 8×16 and 16×8 both land on 128.
    /// </param>
    /// <param name="WirelessDevices">
    /// Wireless products. Vendor help states at most 64, explicitly for response-time reasons. That "explicitly"
    /// is load-bearing: it is a RECOMMENDATION, which is why the row that reads it is a Warning — an Error's
    /// consequence must hold whatever the author intended, and a slow-but-working system does not qualify.
    /// </param>
    /// <param name="Resources">
    /// How many resources the controller's table holds. THE ONE FIGURE HERE WITH NO VENDOR SOURCE — see
    /// <see cref="AuthoredResourceCeiling"/>, which carries the TODO and the reasoning. A caller with a real
    /// controller in mind should pass its own value rather than inherit the authored one.
    /// </param>
    /// <param name="LinksPerWirelessUnit">
    /// Follow-links one wireless unit may carry. Vendor help states 32. Like <see cref="WirelessDevices"/> this
    /// is a RECOMMENDATION rather than a hard refusal — and the field evidence is contradictory, with reports of
    /// degradation well below the published figure — which is why the row reading it is a Warning.
    /// </param>
    /// <param name="LinksPerCombiUnit">
    /// Follow-links a COMBI unit may carry. Vendor help states 64. Declared as its own number rather than as a
    /// multiple of <see cref="LinksPerWirelessUnit"/>: that the two happen to differ by a factor of two is an
    /// observation about today's figures, not a rule the vendor states, and deriving one from the other would
    /// turn a published fact into an inference.
    /// </param>
    /// <param name="ScenariosPerReceiver">
    /// Scenarios one wireless RECEIVER may take part in. Vendor help states 32. A recommendation like the two
    /// above, and the row reading it is a Warning for the same reason.
    /// </param>
    public sealed record ControllerCapabilityLimits(
        int InputModules,
        int OutputModules,
        int AddressesPerDirection,
        int WirelessDevices,
        int Resources,
        int LinksPerWirelessUnit,
        int LinksPerCombiUnit,
        int ScenariosPerReceiver)
    {
        /// <summary>
        /// The vendor-documented defaults. Supplied EXPLICITLY by a caller that has a target controller in mind;
        /// there is deliberately no implicit fallback, because silently validating against a default is
        /// indistinguishable from validating against a guess.
        /// </summary>
        public static ControllerCapabilityLimits VendorDocumented { get; } =
            new(8, 16, 128, 64, AuthoredResourceCeiling, 32, 64, 32);

        /// <summary>
        /// The resource ceiling <c>capacity-resources-high</c> measures a project against.
        /// <para>
        /// TODO: unconfirmed. This is the ONE figure in this type with NO vendor source: the datasheet documents the
        /// module and address bounds and the vendor help states the wireless recommendation, but no published
        /// document gives a controller's resource-table size. It is authored (D20), it is marked here rather than
        /// only in a backlog entry (D21(d)), and it is deliberately generous — the row it feeds is a Warning about
        /// APPROACHING a limit, so an over-large figure makes the row quiet rather than wrong. A caller with a real
        /// controller in mind should pass its own value.
        /// </para>
        /// </summary>
        public const int AuthoredResourceCeiling = 2000;

        // EXPECTED TO GROW, and cheaply: a further controller limit arrives as one member plus one rule, the way
        // the link and scenario ceilings did. What must NOT migrate here is a PROJECT-ONLY count — a cap on how
        // many of something a project may contain is a declared threshold on the rule that enforces it, because
        // it needs no controller and would otherwise make a project-only rule require context it does not need.
    }
}
