using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// THE SAFETY RULE, as a gate. A live controller is one of the three untouchable things: its state changes
    /// are destructive and cannot be undone by a test run. Only <c>safe_integration_tests</c> may reach one, and
    /// only through state-safe operations; every other suite runs on fakes, files or headless UI.
    ///
    /// <para>That was convention plus fixture design, and the document said so — nothing mechanically prevented
    /// an unsafe test from being added. This fixture is compiled into the controller-free suites and scans the
    /// assembly it is running in, so the rule now fails a build instead of relying on review.</para>
    ///
    /// <para><b>Which suites host it, and the one that does not.</b> Every suite that runs without a controller
    /// hosts it — the four in the default verification, and the end-to-end suite, whose desktop leg is outside
    /// every default run and would otherwise be the one place an unsafe site could be added unpoliced. The
    /// architecture suite is the deliberate exception: it hosts the SEEDED violator that proves the scan can
    /// fail, and a violator planted where the guard runs would fail the very rule it exists to arm. It links the
    /// scan without the guard, and <c>ControllerReachArchitectureTests</c> drives the same predicate over its own
    /// seeds instead.</para>
    ///
    /// <para><b>The exemption list is a dictionary of site to reason, not a bare list.</b> A site that builds a
    /// real service without calling anything is safe and is the one shape that legitimately needs admitting; a
    /// reason is what stops the next entry being added because the gate was in the way.</para>
    /// </summary>
    [TestFixture]
    public class ControllerReachGuard
    {
        /// <summary>Every controller-facing service carries this, so it is what "reaches a controller" means.</summary>
        private const string MarkerInterface = "Ihc.IIHCApiService";

        /// <summary>The generated SOAP layer: a constructor taking one of its types is handed its transport.</summary>
        private const string TransportNamespace = "Ihc.Soap.";

        /// <summary>
        /// Sites allowed to construct a real service anyway. Empty for most suites: an entry here is a claim that
        /// building one is safe, which is only true while nothing is called on it.
        /// </summary>
        private static readonly Dictionary<string, string> Admitted = new()
        {
            ["Ihc.Tests.ReflectionUtilRealTest.get_Service"] =
                "constructs a real AuthenticationService and calls NO operation on it: the shared reflection "
                + "contract asks what the runtime says about the TYPE, so constructing it is the whole arrange "
                + "and no message reaches a controller.",

            ["Ihc.Vis.Tests.ControllerBridgeTests.InjectingControllerWithoutMatchingAuth_IsRejected_NeverAuthenticatesAForeignSession"] =
                BridgeWiringReason,
            ["Ihc.Vis.Tests.ControllerBridgeTests.MatchedPairBridge_AuthenticatesTheExactAuthTheControllerWasBuiltFrom"] =
                BridgeWiringReason,
        };

        /// <summary>
        /// Shared by the two bridge-wiring sites. They assert WHICH auth a controller rides, which can only be
        /// asked of real instances -- a fake would answer whatever it was told. Construction does no network I/O,
        /// no operation is called, and the endpoint is under the reserved <c>.invalid</c> TLD, which is
        /// guaranteed never to resolve: three independent reasons no message can leave.
        /// </summary>
        private const string BridgeWiringReason =
            "builds a real AuthenticationService and ControllerService to assert that the bridge rides the auth "
            + "the controller was built from. Construction performs no I/O, no operation is called, and the "
            + "endpoint is under the reserved .invalid TLD.";

        private static ControllerReachScan.Anchors AnchorsForThisSuite() => new(MarkerInterface, TransportNamespace, Admitted);

        /// <summary>The rule. A suite that may not reach a controller must not be able to build one.</summary>
        [Test]
        public void ThisSuiteConstructsNoControllerService()
        {
            Assembly suite = typeof(ControllerReachGuard).Assembly;

            var offending = ControllerReachScan.Sites(suite, AnchorsForThisSuite())
                .Where(site => !Admitted.ContainsKey(site))
                .ToList();

            Assert.That(offending, Is.Empty,
                "this suite may not reach a live controller: its state changes are destructive and a test run "
                + "cannot undo them. Put the fake on the low-level IIHCApiService implementation, which is the "
                + "seam the safety rule prescribes — or, if the site only CONSTRUCTS a service and calls nothing "
                + "on it, admit it in ControllerReachGuard WITH that reason");
        }

        /// <summary>
        /// An admitted site that no longer constructs anything is a permission nobody needs, and a permission
        /// nobody needs is how the next unsafe site gets waved through. Checked only where the site exists: this
        /// fixture runs in several assemblies and an entry describes one of them.
        /// </summary>
        [Test]
        public void EveryAdmittedSiteInThisSuiteStillBuildsOne()
        {
            Assembly suite = typeof(ControllerReachGuard).Assembly;
            var built = ControllerReachScan.Sites(suite, AnchorsForThisSuite()).ToHashSet();

            var declaredHere = Admitted.Keys
                .Where(site => suite.GetType(site[..site.LastIndexOf('.')]) is not null)
                .ToList();

            Assert.That(declaredHere.Where(site => !built.Contains(site)), Is.Empty,
                "an admitted site in this assembly no longer constructs a controller service — delete the entry; "
                + "a permission that describes nothing is how the list stops being read");
        }
    }
}
