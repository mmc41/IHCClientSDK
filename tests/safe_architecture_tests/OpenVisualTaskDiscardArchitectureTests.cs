using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <b>G2 — no unsupervised task discard.</b> <c>_ = SomethingAsync()</c> produces a task nobody observes, so a
    /// fault inside it surfaces on the finalizer thread through <c>TaskScheduler.UnobservedTaskException</c> —
    /// arbitrarily later, attributed to nothing, and possibly after the state it concerned has moved on. It is the
    /// other half of the same hole <c>async void</c> opens, and the discard syntax is there to silence the warning
    /// that would have said so.
    ///
    /// <para>Two lists, with the same meanings the sibling containment gate gives them: an EXEMPTION is permanent
    /// and reasoned; the BASELINE is debt whose entries each name the task that removes them, may only shrink, and
    /// is asserted empty later.</para>
    /// </summary>
    [TestFixture]
    public class OpenVisualTaskDiscardArchitectureTests
    {
        private static readonly Assembly Gui = typeof(global::ihc_openvisual.App).Assembly;

        /// <summary>
        /// The supervised hand-off. Named rather than <c>typeof</c>-anchored because the supervisor is
        /// <c>internal</c> to the GUI assembly and this suite is not on its <c>InternalsVisibleTo</c> list.
        /// <para>
        /// The allowance is LOAD-BEARING: every caller discards the task the supervisor hands back, which is
        /// exactly the shape the rule refuses everywhere else. <see cref="TheSupervisorAnchor_StillResolves"/> is
        /// what keeps a rename from silently emptying the rule.
        /// </para>
        /// </summary>
        private static readonly TaskDiscardScan.Supervisor Supervised =
            new("ihc_openvisual.Services.TaskSupervisor", "Fire");

        /// <summary>
        /// Discards that reach no supervision today. Every entry names the task that converts it.
        /// <para>
        /// EMPTY: every seeded site now hands its task to the supervisor. The list stays as a ratchet — a new
        /// discard must be named here with an owner, or fixed.
        /// </para>
        /// </summary>
        private static readonly IReadOnlyList<ContainmentDebt> Baseline = [];

        /// <summary>
        /// Permanently outside the rule. Every entry is a discard in code that runs where a supervisor cannot:
        /// before the sink exists, or in a process that has no user to report to. A baseline entry for either
        /// would be debt no task will ever pay — which is exactly what would keep the baseline from reaching
        /// empty, and why the two lists stay separate.
        /// </summary>
        private static readonly IReadOnlyList<ContainmentExemption> Exemptions =
        [
            new(new("ihc_openvisual.DesignTime.DesignMainWindowViewModel", "DesignWorkflow"),
                "design-time only: the XAML previewer builds this, in a process with no logging pipeline, no sink "
                + "and nobody to report to. Supervising it would route a fault to a port that cannot exist there."),
            new(new("ihc_openvisual.Program", "Main"),
                "the telemetry self-check's ContinueWith. The task that could carry a real fault -- the probe -- IS "
                + "observed: the continuation exists to read its exception and log it. What is discarded is the "
                + "CONTINUATION, whose whole body is that one LogWarning, and it runs before the logging pipeline "
                + "has anything else to report to. Start-up also predates the sink a supervisor would route into."),
        ];

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

        /// <summary>
        /// The name-anchored supervisor STILL RESOLVES. Without this, renaming <c>TaskSupervisor.Fire</c> would
        /// turn every supervised hand-off back into an unadmitted discard — or, worse, leave the anchor matching
        /// nothing while the rule below still reads green because the sites moved with the rename.
        /// </summary>
        [Test]
        public void TheSupervisorAnchor_StillResolves()
        {
            Type? supervisor = Gui.GetType(Supervised.TypeFullName, throwOnError: false);
            Assert.That(supervisor, Is.Not.Null,
                $"the discard gate anchors on '{Supervised.TypeFullName}', which no longer exists — re-anchor it");
            Assert.That(
                supervisor!.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(m => m.Name),
                Has.Member(Supervised.Member),
                $"'{Supervised.TypeFullName}' declares no '{Supervised.Member}' — the allowance now admits nothing");
        }

        /// <summary>The rule. A discard is supervised, or it is named — there is no third answer.</summary>
        [Test]
        public void EveryTaskDiscard_IsSupervised_OrIsNamed()
        {
            IReadOnlyList<ContainmentSite> sites = TaskDiscardScan.Sites(Gui, Supervised);
            var named = Baseline.Select(d => d.Site).Concat(Exemptions.Select(x => x.Site)).ToHashSet();

            Assert.That(sites.Where(s => !named.Contains(s)).Select(s => s.ToString()), Is.Empty,
                "a discarded task is observed by nobody: its fault reaches the finalizer thread through "
                + "TaskScheduler.UnobservedTaskException, arbitrarily later, or not at all. Hand it to the "
                + "supervisor — or, if the discard is deliberate, name it in the exemption list WITH a reason");
        }

        /// <summary>A baseline entry that no longer names a real discard is the list rotting: the debt was paid
        /// and nobody deleted the row, so the next reader trusts a list that has stopped describing the code.</summary>
        [Test]
        public void EveryBaselineEntry_StillNamesARealDiscard()
        {
            var sites = TaskDiscardScan.Sites(Gui, Supervised).ToHashSet();

            Assert.That(Baseline.Where(d => !sites.Contains(d.Site)).Select(d => $"{d.Site} (owed by {d.PaidBy})"),
                Is.Empty,
                "these baseline entries name discards that are gone or already supervised — delete the rows; "
                + "the baseline may only shrink, and an entry that describes nothing is how it stops being read");
        }

        /// <summary>The same honesty rule for the permanent list.</summary>
        [Test]
        public void EveryExemption_StillNamesARealDiscard()
        {
            var sites = TaskDiscardScan.Sites(Gui, Supervised).ToHashSet();

            Assert.That(Exemptions.Where(x => !sites.Contains(x.Site)).Select(x => x.Site.ToString()), Is.Empty,
                "an exemption for a discard that no longer exists is noise that teaches the reader to skip the list");
        }

        // ── Positive and negative controls ──────────────────────────────────────────────────────────────────────

        private static readonly TaskDiscardScan.Supervisor SeededSupervisor =
            new(typeof(SeededSupervision).FullName!, nameof(SeededSupervision.Fire));

        internal static class SeededSupervision
        {
            internal static Task Fire(Task work, string origin) => work;
        }

        private static class SeededDiscards
        {
            internal static Task Work() => Task.CompletedTask;

            internal static int Counted() => 1;

            /// <summary>Positive control: the shape the rule exists to catch.</summary>
            internal static void Discards() => _ = Work();

            /// <summary>Negative control: awaited, so it is observed.</summary>
            internal static async Task Awaits() => await Work();

            /// <summary>Negative control: kept, so a caller can still observe it.</summary>
            internal static Task Returns() => Work();

            /// <summary>Negative control: a discarded NON-task has no fault to lose.</summary>
            internal static void DiscardsANonTask() => _ = Counted();

            /// <summary>Negative control: handed to the supervisor, and the supervisor's own return discarded —
            /// the belt-and-braces shape the allowance exists for.</summary>
            internal static void Supervised() => _ = SeededSupervision.Fire(Work(), "seed");
        }

        private static MethodBase Seeded(string name) =>
            typeof(SeededDiscards).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

        /// <summary>The detector is armed: it reports the discard, stays silent on the three shapes that observe
        /// the task or produce none, and honours the supervisor allowance. A rule whose scan cannot fail proves
        /// nothing.</summary>
        [Test]
        public void TheDiscardScan_IsArmed()
        {
            Assert.Multiple(() =>
            {
                Assert.That(TaskDiscardScan.DiscardsATask(Seeded(nameof(SeededDiscards.Discards)), SeededSupervisor),
                    Is.True, "an unsupervised discard must be caught");
                Assert.That(TaskDiscardScan.DiscardsATask(Seeded(nameof(SeededDiscards.Returns)), SeededSupervisor),
                    Is.False, "a returned task is still observable by its caller");
                Assert.That(TaskDiscardScan.DiscardsATask(Seeded(nameof(SeededDiscards.DiscardsANonTask)), SeededSupervisor),
                    Is.False, "discarding a value that is not a task loses no fault");
                Assert.That(TaskDiscardScan.DiscardsATask(Seeded(nameof(SeededDiscards.Supervised)), SeededSupervisor),
                    Is.False, "a supervised hand-off is not an unobserved task");
                Assert.That(TaskDiscardScan.DiscardsATask(Seeded(nameof(SeededDiscards.Supervised)), Supervised),
                    Is.True, "and the supervisor anchor is load-bearing: the real one is not the seeded one");
            });
        }

        /// <summary>The awaited control lives in its own test because an async method's body is in a state machine,
        /// so the scan has to reach it there rather than in the method a reader sees.</summary>
        [Test]
        public void AnAwaitedTask_IsNotADiscard()
        {
            var awaiting = TaskDiscardScan.Sites(typeof(OpenVisualTaskDiscardArchitectureTests).Assembly, SeededSupervisor)
                .Where(site => site.Type == typeof(OpenVisualTaskDiscardArchitectureTests).FullName)
                .Select(site => site.Member)
                .ToList();

            Assert.That(awaiting, Does.Not.Contain(nameof(SeededDiscards.Awaits)),
                "awaiting a task observes it — reporting that as a discard would make the rule unusable");
        }
    }
}
