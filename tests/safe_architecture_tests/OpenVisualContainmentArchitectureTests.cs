using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <b>G1 — no unsupervised <c>async void</c>.</b> An <c>async void</c> method has nothing awaiting it, so a
    /// fault after its first <c>await</c> is raised on the synchronization context with no caller to catch it; a
    /// window-lifecycle handler is worse, running off the message loop where no global net can see it at all. Every
    /// such handler in the GUI assembly must therefore reach a floor: the view-model's own error boundary when it
    /// has a view-model in reach (floor 1), or the view layer's guard when it does not (floor 3).
    ///
    /// <para><b>Two lists, and they are different things.</b> An EXEMPTION is permanent and carries a reason. The
    /// BASELINE is debt: each entry names the task that removes it, the list may only shrink, and a later task
    /// asserts it reached empty. Merging them would make every entry look equally final, which is how a list stops
    /// being read.</para>
    ///
    /// <para>This fixture sets the shape the sibling containment gates follow.</para>
    /// </summary>
    [TestFixture]
    public class OpenVisualContainmentArchitectureTests
    {
        private static readonly Assembly Gui = typeof(global::ihc_openvisual.App).Assembly;

        private const string HandlerGuardTypeName = "ihc_openvisual.Views.HandlerGuard";
        private const string GuardMemberName = "RunAsync";
        private static readonly string ViewModelTypeName =
            typeof(global::ihc_openvisual.ViewModels.MainWindowViewModel).FullName!;
        private const string BoundaryMemberName = "RunAsync";

        private static readonly AsyncVoidScan.Anchors GuiFloors =
            new(HandlerGuardTypeName, GuardMemberName, ViewModelTypeName, BoundaryMemberName);

        /// <summary>
        /// Sites that reach no floor today. Every entry names the task that puts it on one; when they are all gone
        /// the list is asserted empty and the ratchet becomes a plain gate.
        /// </summary>
        private static readonly IReadOnlyList<ContainmentDebt> Baseline = [];

        /// <summary>Sites deliberately outside the rule, permanently. Empty is the healthy state: a handler that
        /// genuinely cannot reach a floor is rare, and an entry here is a claim that nothing will ever pay it.</summary>
        private static readonly IReadOnlyList<ContainmentExemption> Exemptions = [];

        /// <summary>
        /// THE BASELINE IS EMPTY, asserted rather than merely observed.
        /// </summary>
        /// <remarks>
        /// <para>Every seeded entry was deleted by the task that owed it, so the ratchet has become the plain
        /// gate it was always meant to turn into: from here a new site fails the rule above outright instead of
        /// being admitted as named debt.</para>
        /// <para><b>Why assert emptiness instead of deleting the mechanism.</b> An empty list costs one field
        /// and keeps the vocabulary: a future debt can be recorded as debt, with an owner, rather than being
        /// pushed into the exemption list — which is the one way this rule quietly stops meaning anything, since
        /// an exemption is a claim that nobody will EVER pay and a baseline entry is a claim that someone will.
        /// This test is what makes re-opening the baseline a deliberate act with a failing test attached, rather
        /// than a line somebody adds while fixing something else.</para>
        /// <para>The exemption list is deliberately untouched by this: the two lists say different things.</para>
        /// </remarks>
        [Test]
        public void TheBaselineIsEmpty() =>
            Assert.That(Baseline, Is.Empty,
                "every seeded entry has been paid; re-opening this list is a decision, and this assertion is "
                + "where it has to be argued rather than assumed");

        /// <summary>The rule. A site is contained, or it is named — there is no third answer.</summary>
        [Test]
        public void EveryAsyncVoidHandler_ReachesAFloor_OrIsNamed()
        {
            IReadOnlyList<(ContainmentSite Site, MethodInfo Method)> sites = AsyncVoidScan.Sites(Gui);
            Assert.That(sites, Is.Not.Empty,
                "the scan found no async void at all in the GUI assembly — this rule would pass vacuously; fix the scan, not the assert");

            var named = Baseline.Select(d => d.Site).Concat(Exemptions.Select(x => x.Site)).ToHashSet();
            var unsupervised = sites
                .Where(s => !named.Contains(s.Site))
                .Where(s => AsyncVoidScan.FloorOf(s.Method, GuiFloors) == AsyncVoidScan.Floor.None)
                .Select(s => s.Site.ToString())
                .ToList();

            Assert.That(unsupervised, Is.Empty,
                "an async void whose fault reaches no floor dies with no user report, no log record and no span. "
                + "Route it through HandlerGuard, or through a member the view-model's RunAsync boundary owns — "
                + "or, if it is deliberate, name it in the exemption list WITH a reason");
        }

        /// <summary>The start-up hook is floor 1 by the book, and the gate must say so: an async void lambda whose
        /// whole body awaits one call to a boundary-routed member. Pinned as its own case because the rule above
        /// would also pass if the scan simply stopped seeing lambdas.</summary>
        [Test]
        public void TheStartUpHook_IsRecognisedAsReachingTheViewModelBoundary()
        {
            var startup = AsyncVoidScan.Sites(Gui)
                .Where(s => s.Site.Type == typeof(global::ihc_openvisual.App).FullName)
                .ToList();

            Assert.That(startup, Is.Not.Empty, "the App start-up hook is no longer an async void — re-anchor this test");
            Assert.That(startup.Select(s => AsyncVoidScan.FloorOf(s.Method, GuiFloors)),
                Is.All.EqualTo(AsyncVoidScan.Floor.ViewModelBoundary),
                "a handler routed through the view-model's own boundary is contained; seeding it as debt would put "
                + "an entry in the baseline that no task can ever pay");
        }

        /// <summary>A baseline entry that no longer names a real site is the list rotting: the debt was paid and
        /// nobody removed the row, so the next reader trusts a list that has stopped describing the code.</summary>
        [Test]
        public void EveryBaselineEntry_StillNamesAnUnsupervisedSite()
        {
            var sites = AsyncVoidScan.Sites(Gui).ToDictionary(s => s.Site, s => s.Method);

            var stale = Baseline
                .Where(debt => !sites.TryGetValue(debt.Site, out MethodInfo? method)
                               || AsyncVoidScan.FloorOf(method, GuiFloors) != AsyncVoidScan.Floor.None)
                .Select(debt => $"{debt.Site} (owed by {debt.PaidBy})")
                .ToList();

            Assert.That(stale, Is.Empty,
                "these baseline entries name sites that are gone or already contained — delete the rows; "
                + "the baseline may only shrink, and an entry that describes nothing is how it stops being read");
        }

        /// <summary>The same honesty rule for the permanent list.</summary>
        [Test]
        public void EveryExemption_StillNamesARealSite()
        {
            var sites = AsyncVoidScan.Sites(Gui).Select(s => s.Site).ToHashSet();

            Assert.That(Exemptions.Where(x => !sites.Contains(x.Site)).Select(x => x.Site.ToString()), Is.Empty,
                "an exemption for a site that no longer exists is noise that teaches the reader to skip the list");
        }

        // ── Positive and negative controls ──────────────────────────────────────────────────────────────────────
        //
        // Seeded in THIS assembly, run through the exact predicate the rule above uses, with the floor anchors
        // pointed at seeded stand-ins. Anchoring the seeds on the real guard is impossible — it is internal to the
        // GUI assembly — and parameterising the anchors is what lets both floors be armed rather than only the one
        // this assembly could name.

        private static readonly AsyncVoidScan.Anchors SeededFloors = new(
            typeof(SeededGuard).FullName!, nameof(SeededGuard.RunAsync),
            typeof(SeededBoundaryOwner).FullName!, nameof(SeededBoundaryOwner.RunAsync));

        internal static class SeededGuard
        {
            internal static Task RunAsync(Func<Task> work) => work();
        }

        internal static class SeededBoundaryOwner
        {
            internal static Task RunAsync(Func<Task> work) => work();

            /// <summary>The floor-1 shape: a member whose own body is a boundary call.</summary>
            internal static Task RoutedWork() => RunAsync(() => Task.CompletedTask);

            /// <summary>The near-miss: async work that reaches no boundary.</summary>
            internal static Task UnroutedWork() => Task.CompletedTask;
        }

        private static class SeededHandlers
        {
#pragma warning disable CS1998   // deliberately not awaiting: the shape under test is the signature, not the body
            internal static async void Unguarded(object? sender, EventArgs e) => await SeededBoundaryOwner.UnroutedWork();

            internal static async void OnFloorThree(object? sender, EventArgs e) =>
                await SeededGuard.RunAsync(() => Task.CompletedTask);

            internal static async void OnFloorOne(object? sender, EventArgs e) => await SeededBoundaryOwner.RoutedWork();

            /// <summary>Awaits TWO things, so the floor-1 arm must refuse it however routed the first one is —
            /// "it probably reaches a boundary eventually" is the reading that arm exists to reject.</summary>
            internal static async void OnTwoAwaits(object? sender, EventArgs e)
            {
                await SeededBoundaryOwner.RoutedWork();
                await SeededBoundaryOwner.UnroutedWork();
            }
#pragma warning restore CS1998
        }

        private static MethodInfo Seeded(string name) =>
            typeof(SeededHandlers).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

        /// <summary>The detector is armed: pointed at seeded handlers it reports each floor, and reports NONE for
        /// the two shapes that reach none. A rule whose scan cannot fail is a rule that proves nothing.</summary>
        [Test]
        public void TheFloorScan_IsArmed()
        {
            Assert.Multiple(() =>
            {
                Assert.That(AsyncVoidScan.FloorOf(Seeded(nameof(SeededHandlers.Unguarded)), SeededFloors),
                    Is.EqualTo(AsyncVoidScan.Floor.None), "an unguarded handler must be caught");
                Assert.That(AsyncVoidScan.FloorOf(Seeded(nameof(SeededHandlers.OnFloorThree)), SeededFloors),
                    Is.EqualTo(AsyncVoidScan.Floor.HandlerGuard), "a guarded handler must be recognised");
                Assert.That(AsyncVoidScan.FloorOf(Seeded(nameof(SeededHandlers.OnFloorOne)), SeededFloors),
                    Is.EqualTo(AsyncVoidScan.Floor.ViewModelBoundary), "a boundary-routed handler must be recognised");
                Assert.That(AsyncVoidScan.FloorOf(Seeded(nameof(SeededHandlers.OnTwoAwaits)), SeededFloors),
                    Is.EqualTo(AsyncVoidScan.Floor.None), "the floor-1 arm admits ONE awaited call, not a body that contains one");
                Assert.That(AsyncVoidScan.FloorOf(Seeded(nameof(SeededHandlers.OnFloorThree)), GuiFloors),
                    Is.EqualTo(AsyncVoidScan.Floor.None), "and the anchors are load-bearing: the real floors are not the seeded ones");
            });
        }

        /// <summary>The floors themselves still exist under the names the scan looks for. Without this, renaming
        /// either would leave every site reading as unsupervised — or, worse, the rule quietly matching nothing.</summary>
        [Test]
        public void TheFloorAnchors_StillResolve()
        {
            Type? guard = Gui.GetType(HandlerGuardTypeName);
            Assert.Multiple(() =>
            {
                Assert.That(guard, Is.Not.Null, $"'{HandlerGuardTypeName}' is gone — re-anchor the guard floor");
                Assert.That(guard!.GetMethod(GuardMemberName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
                    Is.Not.Null, "the guard no longer has the member the scan looks for");
                Assert.That(
                    Gui.GetType(ViewModelTypeName)!.GetMethod(BoundaryMemberName,
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
                    Is.Not.Null, "the view-model's error boundary no longer has the member the scan looks for");
            });
        }
    }
}
