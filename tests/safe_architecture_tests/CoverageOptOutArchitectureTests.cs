using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <b>G5 — the containment surface may not opt out of measurement.</b> <c>.runsettings</c> excludes
    /// <c>[ExcludeFromCodeCoverage]</c>, so applying it to a guard, a boundary, a sink or a catch-bearing workflow
    /// member deletes exactly the evidence the containment tests depend on — and deletes it INVISIBLY: the
    /// reported figure improves and the untested path simply stops being listed.
    ///
    /// <para>The pressure this forecloses is structural rather than malicious. Coverage reports rather than gates
    /// here, but a printed number invites being raised, and this attribute is the cheapest way to raise one.
    /// An exemption may say "deliberate, because X"; it may never say "to raise the number".</para>
    ///
    /// <para>The ban lands against an empty population, which is the cheapest it will ever be.</para>
    /// </summary>
    [TestFixture]
    public class CoverageOptOutArchitectureTests
    {
        /// <summary>The assemblies the containment surface spans: the GUI shell that owns the guards and the
        /// boundaries, the SDK whose services carry the fault paths beneath them, and the two halves of the shared
        /// host bootstrap where the process-wide nets are wired.</summary>
        private static readonly IReadOnlyList<CoverageOptOutScan.Scope> Surface =
        [
            new(typeof(global::ihc_openvisual.App).Assembly, typeof(global::ihc_openvisual.App).Namespace!),
            new(typeof(global::Ihc.IhcSettings).Assembly, "Ihc"),
            new(typeof(global::Ihc.Bootstrap.TelemetryBootstrap).Assembly, "Ihc"),
            new(typeof(global::Ihc.Bootstrap.AppTelemetryBootstrap).Assembly, "Ihc"),
        ];

        /// <summary>Deliberate, permanent opt-outs. Empty, and that is the point: the ban is being written while
        /// nothing in the repository uses the attribute at all.</summary>
        private static readonly IReadOnlyList<ContainmentExemption> Exemptions = [];

        /// <summary>The rule.</summary>
        [Test]
        public void NothingOnTheContainmentSurface_OptsOutOfCoverage()
        {
            var exempt = Exemptions.Select(x => x.Site).ToHashSet();
            var offences = Surface
                .SelectMany(CoverageOptOutScan.OptedOut)
                .Where(site => !exempt.Contains(site))
                .Select(site => site.ToString())
                .Distinct()
                .ToList();

            Assert.That(offences, Is.Empty,
                "these members handle a fault and are excluded from coverage measurement, so the path that "
                + "reports the fault is no longer listed as untested — the figure improves and the evidence "
                + "disappears together. Remove the attribute, or name the member in the exemption list WITH a "
                + "reason that is not 'to raise the number'");
        }

        /// <summary>The surface is not empty, or the rule above would pass by scanning nothing.</summary>
        [Test]
        public void TheContainmentSurface_IsNotEmpty()
        {
            var handled = Surface
                .SelectMany(CoverageOptOutScan.Members)
                .Where(CoverageOptOutScan.HandlesAFault)
                .ToList();

            Assert.That(handled, Is.Not.Empty,
                "no member in any scanned assembly was seen to catch anything — the scan is broken, not the code");
        }

        /// <summary>
        /// The floors are on the surface BY THE STRUCTURAL DEFINITION, not by being listed. This is what makes a
        /// named surface list unnecessary: a member that catches is containment, so the guard and the view-model's
        /// error boundary are covered without either being enumerated — and if one ever stopped catching, this
        /// test would say so rather than the rule silently ceasing to protect it.
        /// </summary>
        [Test]
        public void TheNamedFloors_AreOnTheContainmentSurface()
        {
            Assembly gui = typeof(global::ihc_openvisual.App).Assembly;
            MethodBase? guard = gui.GetType("ihc_openvisual.Views.HandlerGuard")
                ?.GetMethod("RunAsync", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            MethodBase? boundary = typeof(global::ihc_openvisual.ViewModels.MainWindowViewModel)
                .GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            Assert.Multiple(() =>
            {
                Assert.That(guard, Is.Not.Null, "the view layer's guard is gone — re-anchor this test");
                Assert.That(boundary, Is.Not.Null, "the view-model's error boundary is gone — re-anchor this test");
                Assert.That(CoverageOptOutScan.HandlesAFault(guard!), Is.True,
                    "the guard must be on the surface the ban protects");
                Assert.That(CoverageOptOutScan.HandlesAFault(boundary!), Is.True,
                    "the view-model's error boundary must be on the surface the ban protects");
            });
        }

        /// <summary>An exemption naming a member that is no longer on the surface is noise that teaches the reader
        /// to skip the list.</summary>
        [Test]
        public void EveryExemption_StillNamesAnOptOutOnTheSurface()
        {
            var offending = Surface.SelectMany(CoverageOptOutScan.OptedOut).ToHashSet();

            Assert.That(Exemptions.Where(x => !offending.Contains(x.Site)).Select(x => x.Site.ToString()), Is.Empty,
                "this exemption no longer describes anything — delete the row");
        }

        // ── Positive and negative controls ──────────────────────────────────────────────────────────────────────

        private static class SeededCoverage
        {
            /// <summary>Positive control: handles a fault AND opts out — the shape the ban exists to catch.</summary>
            [ExcludeFromCodeCoverage]
            internal static bool CatchesAndOptsOut()
            {
                try
                {
                    return bool.Parse("no");
                }
                catch (FormatException)
                {
                    return false;
                }
            }

            /// <summary>Negative control: handles a fault and is measured, which is the healthy shape.</summary>
            internal static bool CatchesAndIsMeasured()
            {
                try
                {
                    return bool.Parse("no");
                }
                catch (FormatException)
                {
                    return false;
                }
            }

            /// <summary>Negative control: opts out but handles nothing. The ban is scoped to the containment
            /// surface, so this is out of its reach — a repo-wide ban is a different, larger rule.</summary>
            [ExcludeFromCodeCoverage]
            internal static int OptsOutWithoutCatching() => 1;

            /// <summary>Positive control for the async arm: the catch a reader sees here lives in the state
            /// machine, so a scan that only read this method's own body would miss it.</summary>
            [ExcludeFromCodeCoverage]
            internal static async Task<bool> AwaitsAndCatchesAndOptsOut()
            {
                try
                {
                    await Task.Yield();
                    return bool.Parse("no");
                }
                catch (FormatException)
                {
                    return false;
                }
            }
        }

        /// <summary>Opting out via the enclosing TYPE rather than the member — the cheapest spelling, and the one
        /// a member-only scan would miss.</summary>
        [ExcludeFromCodeCoverage]
        private static class SeededTypeWideOptOut
        {
            internal static bool CatchesUnderATypeWideOptOut()
            {
                try
                {
                    return bool.Parse("no");
                }
                catch (FormatException)
                {
                    return false;
                }
            }
        }

        private static MethodBase Seeded(Type owner, string name) =>
            owner.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

        /// <summary>The detector is armed, in both directions and on both spellings. A ban whose scan cannot fire
        /// is a comment.</summary>
        [Test]
        public void TheOptOutScan_IsArmed()
        {
            MethodBase catchesAndOptsOut = Seeded(typeof(SeededCoverage), nameof(SeededCoverage.CatchesAndOptsOut));
            MethodBase measured = Seeded(typeof(SeededCoverage), nameof(SeededCoverage.CatchesAndIsMeasured));
            MethodBase optsOutOnly = Seeded(typeof(SeededCoverage), nameof(SeededCoverage.OptsOutWithoutCatching));
            MethodBase asyncOptOut = Seeded(typeof(SeededCoverage), nameof(SeededCoverage.AwaitsAndCatchesAndOptsOut));
            MethodBase typeWide = Seeded(typeof(SeededTypeWideOptOut),
                nameof(SeededTypeWideOptOut.CatchesUnderATypeWideOptOut));

            Assert.Multiple(() =>
            {
                Assert.That(CoverageOptOutScan.HandlesAFault(catchesAndOptsOut)
                            && CoverageOptOutScan.IsExcludedFromCoverage(catchesAndOptsOut), Is.True,
                    "a catch-bearing member carrying the attribute must be caught");
                Assert.That(CoverageOptOutScan.IsExcludedFromCoverage(measured), Is.False,
                    "a measured member must not be reported");
                Assert.That(CoverageOptOutScan.HandlesAFault(optsOutOnly), Is.False,
                    "the ban is scoped to the containment surface: a member that handles nothing is out of reach");
                Assert.That(CoverageOptOutScan.HandlesAFault(asyncOptOut), Is.True,
                    "an async body's catch lives in its state machine, and the scan must follow it there");
                Assert.That(CoverageOptOutScan.IsExcludedFromCoverage(typeWide), Is.True,
                    "an opt-out on the enclosing type covers its members, and the scan must see it there too");
            });
        }
    }
}
