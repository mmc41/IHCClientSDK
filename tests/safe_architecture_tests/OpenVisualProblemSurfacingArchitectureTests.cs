using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// A fault can go quiet in three ways, and until this fixture only the first two were detected: an
    /// <c>async void</c> handler reaching no containment floor, and a discarded task nobody supervises. The third
    /// is a problem shown to the installer while the workflow's span ends <c>ok</c>, because saying so is a
    /// separate statement that a site can simply not make.
    ///
    /// <para>The helper that removes the mistake by construction exists — it folds the span outcome, the log
    /// record and the dialog into one call — but nothing stopped a NEW site going around it. Both defects that
    /// helper exists to prevent had already reached shipped code, and both were found by reading rather than by
    /// a test, which is the same review that would have to catch the next one.</para>
    ///
    /// <para>This fixture follows the shape its two sibling containment gates set: a scan, a rule, a baseline of
    /// named debt with an owner per entry, a permanent exemption list kept separate from it, and seeded controls
    /// proving the detector can fail.</para>
    /// </summary>
    [TestFixture]
    public class OpenVisualProblemSurfacingArchitectureTests
    {
        private static readonly Assembly Gui = typeof(global::ihc_openvisual.App).Assembly;

        private const string PortTypeName = "ihc_openvisual.Services.IDialogService";
        private const string ReportTypeName = "ihc_openvisual.Services.FailureReport";
        private const string RaisedDisplayTypeName = "ihc_openvisual.Services.RaisedProblemDisplay";

        /// <summary>
        /// The two members that present a fault, and the two helpers allowed to reach them. The report reaches
        /// the port itself for a refusal; the raised-problem display reaches it on the report's behalf, choosing
        /// between the chain and the aggregate form. Nothing else has a reason to.
        /// </summary>
        private static readonly ProblemSurfacingScan.Anchors GuiAnchors = new(
            PortTypeName,
            ["ShowProblemAsync", "ShowInternalErrorAsync"],
            [ReportTypeName, RaisedDisplayTypeName]);

        /// <summary>
        /// Sites that surface a problem without the report today. Every entry names the task that routes it;
        /// when they are all gone the list is asserted empty and the ratchet becomes a plain gate.
        /// </summary>
        private static readonly IReadOnlyList<ContainmentDebt> Baseline = [];

        /// <summary>
        /// THE BASELINE IS EMPTY, asserted rather than merely observed.
        /// </summary>
        /// <remarks>
        /// <para>Every seeded entry was routed through the report by the task that owed it, so the ratchet has
        /// become the plain gate it was meant to turn into: a new site now fails the rule above outright instead
        /// of being admitted as named debt.</para>
        /// <para>An empty list costs one field and keeps the vocabulary. Without it the next debt would be
        /// recorded as an EXEMPTION -- a claim that nobody will ever pay it -- because that is the only list
        /// left, and the two say different things. This assertion is where re-opening the baseline has to be
        /// argued rather than assumed.</para>
        /// </remarks>
        [Test]
        public void TheBaselineIsEmpty() =>
            Assert.That(Baseline, Is.Empty,
                "every recorded site now reports through FailureReport; re-opening this list is a decision, and "
                + "this assertion is where it has to be argued rather than assumed");

        /// <summary>Sites deliberately outside the rule, permanently. Empty is the healthy state: a site that
        /// genuinely must present a fault without telling its span is not a shape this application has.</summary>
        private static readonly IReadOnlyList<ContainmentExemption> Exemptions =
        [
            new(new("ihc_openvisual.ViewModels.MainWindowViewModel", ".ctor"),
                "the internal-error presenter is handed to the Problemer panel here so a person can OPEN a fault "
                + "row that was already recorded. Activating a row is not a workflow failing -- there is no "
                + "operation in flight and no span to tell -- and routing it through the report would file a NEW "
                + "fault every time somebody read an old one. The hand-off is what the rule matches, and it is "
                + "matched deliberately: the member reference is the only place this decision is visible."),
        ];

        /// <summary>The rule. A site goes through the report, or it is named — there is no third answer.</summary>
        [Test]
        public void EverySiteThatSurfacesAProblem_GoesThroughTheReport_OrIsNamed()
        {
            IReadOnlyList<ContainmentSite> sites = ProblemSurfacingScan.Sites(Gui, GuiAnchors);

            var named = Baseline.Select(d => d.Site).Concat(Exemptions.Select(x => x.Site)).ToHashSet();
            var unrouted = sites.Where(site => !named.Contains(site)).Select(site => site.ToString()).ToList();

            Assert.That(unrouted, Is.Empty,
                "a problem shown from here leaves the workflow's span ending ok, because telling it is a separate "
                + "statement this site does not make. Route it through FailureReport — RefusedAsync for a "
                + "condition the workflow detected itself, FailedAsync for an exception — or, if it is "
                + "deliberate, name it in the exemption list WITH a reason");
        }

        /// <summary>
        /// The two admitted helpers must actually reach the guarded members. Admitting a type that reaches
        /// nothing would silently widen the rule's blind spot rather than describe an exception to it — and the
        /// day the report stops reaching the port itself, this rule is policing a member nobody presents through.
        /// </summary>
        [Test]
        public void TheAdmittedHelpers_ReallyDoReachTheGuardedMembers()
        {
            ProblemSurfacingScan.Anchors admittingNothing = GuiAnchors with { AdmittedTypeNames = [] };
            var reachers = ProblemSurfacingScan.Sites(Gui, admittingNothing)
                .Select(site => site.Type)
                .ToHashSet();

            Assert.Multiple(() =>
            {
                Assert.That(reachers, Does.Contain(ReportTypeName),
                    "the report no longer reaches the port; either it was renamed or the rule is now admitting a "
                    + "type that does nothing, which widens the blind spot instead of narrowing it");
                Assert.That(reachers, Does.Contain(RaisedDisplayTypeName),
                    "the raised-problem display no longer reaches the port — same reasoning");
            });
        }

        /// <summary>A baseline entry that no longer names a real site is the list rotting: the debt was paid and
        /// nobody removed the row, so the next reader trusts a list that has stopped describing the code.</summary>
        [Test]
        public void EveryBaselineEntry_StillNamesASurfacingSite()
        {
            var sites = ProblemSurfacingScan.Sites(Gui, GuiAnchors).ToHashSet();

            var stale = Baseline
                .Where(debt => !sites.Contains(debt.Site))
                .Select(debt => $"{debt.Site} (owed by {debt.PaidBy})")
                .ToList();

            Assert.That(stale, Is.Empty,
                "these baseline entries name sites that are gone or already routed — delete the rows; the "
                + "baseline may only shrink, and an entry that describes nothing is how it stops being read");
        }

        /// <summary>The same honesty rule for the permanent list.</summary>
        [Test]
        public void EveryExemption_StillNamesARealSite()
        {
            ContainmentListHonesty.EveryExemptionStillNamesASite(
                Exemptions, ProblemSurfacingScan.Sites(Gui, GuiAnchors).ToHashSet());
        }

        // ── Positive and negative controls ──────────────────────────────────────────────────────────────────────
        //
        // Seeded in THIS assembly and run through the exact predicate the rule above uses, with the anchors
        // pointed at a seeded port and a seeded helper. Anchoring the seeds on the real port is impossible — the
        // GUI assembly does not open its internals to this suite — and parameterising the anchors is what lets
        // the hand-off case be armed at all, since the production hand-off is one line in one view-model.

        private static readonly ProblemSurfacingScan.Anchors SeededAnchors = new(
            typeof(global::ihc_openvisual.Seeded.ISeededDialogPort).FullName!,
            ["ShowProblemAsync", "ShowInternalErrorAsync"],
            [typeof(global::ihc_openvisual.Seeded.SeededFailureReport).FullName!]);

        private static IReadOnlyList<ContainmentSite> SeededSites() =>
            ProblemSurfacingScan.Sites(typeof(OpenVisualProblemSurfacingArchitectureTests).Assembly, SeededAnchors);

        private static ContainmentSite Site<T>(string member) =>
            new(typeof(T).FullName!, member);

        /// <summary>
        /// The detector can fail. Without this the rule above is indistinguishable from one that finds nothing:
        /// its production list is meant to reach empty, and an empty result from a broken scan reads identically.
        /// </summary>
        [Test]
        public void TheSeededBypass_IsFlagged() =>
            Assert.That(SeededSites(),
                Does.Contain(Site<global::ihc_openvisual.Seeded.SeededBypassingWorkflow>("ReportAsync")),
                "a site showing a problem straight off the port is the whole subject of this rule");

        /// <summary>
        /// The trap the rule exists to survive: this site never CALLS the member, it hands it over as a method
        /// group. A scan matching invocations sees nothing here, and the member is then invoked from a component
        /// no scan would associate with a workflow.
        /// </summary>
        [Test]
        public void TheSeededMethodGroupHandOff_IsFlagged() =>
            Assert.That(SeededSites(),
                Does.Contain(Site<global::ihc_openvisual.Seeded.SeededMethodGroupHandOff>("Hand")),
                "the rule must match the member REFERENCE, not the call, or a hand-off passes unnoticed");

        /// <summary>The admitted helper does its job and must not be flagged for it.</summary>
        [Test]
        public void TheSeededHelper_IsNotFlagged() =>
            Assert.That(SeededSites().Select(site => site.Type),
                Does.Not.Contain(typeof(global::ihc_openvisual.Seeded.SeededFailureReport).FullName),
                "admitting the helper is the point: it is what every other site is supposed to go through");

        /// <summary>A workflow that routes through the helper never names a guarded member, so it is invisible to
        /// the scan rather than merely forgiven by it.</summary>
        [Test]
        public void TheSeededConformingWorkflow_IsNotFlagged() =>
            Assert.That(SeededSites().Select(site => site.Type),
                Does.Not.Contain(typeof(global::ihc_openvisual.Seeded.SeededConformingWorkflow).FullName),
                "routing through the report is the conforming shape and must stay silent");

        /// <summary>
        /// The reason the rule is scoped by member: an ordinary dialog on the same port is the interactive UI,
        /// not a fault. Flagging it is what made a port-scoped rule buy a roster longer than its subject.
        /// </summary>
        [Test]
        public void TheSeededOrdinaryDialog_IsNotFlagged() =>
            Assert.That(SeededSites().Select(site => site.Type),
                Does.Not.Contain(typeof(global::ihc_openvisual.Seeded.SeededOrdinaryDialogUser).FullName),
                "a confirmation prompt is not a fault report; scoping by port instead of by member is what this "
                + "control refuses");
    }
}
