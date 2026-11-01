using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Ihc.Tests;
using NUnit.Framework;

// Declared OUTSIDE any namespace, for the reason the latch fixture is. NUnit applies a [SetUpFixture] to the namespace
// it is declared in, and to the whole assembly only when it is declared in none; and it runs a setup fixture only when
// a test below it runs, so a namespaced gate in a suite whose tests live elsewhere would never run at all. The link pin
// in the architecture suite refuses this file if it declares a namespace.

/// <summary>
/// THE SAFETY RULE, as a gate. A live controller is one of the three untouchable things: its state changes
/// are destructive and cannot be undone by a test run. Only <c>safe_integration_tests</c> may reach one, and
/// only through state-safe operations; every other suite runs on fakes, controller stubs, files or headless UI.
///
/// <para>That was convention plus fixture design, and the document said so — nothing mechanically prevented
/// an unsafe test from being added. This setup fixture is compiled into the controller-free suites and scans the
/// assembly it is running in before any of its tests runs, so a site that can reach a controller stops the run
/// instead of relying on review.</para>
///
/// <para><b>Why a setup fixture, not a test.</b> A test only reports: it runs beside the offending test, as often
/// after it as before, so the call it exists to stop has already been sent, and a filtered run skips it. A failing
/// one-time set-up fails every selected test without running it, filtered runs included. For a transport the suite
/// builds itself, such as a bare <c>HttpClient</c>, this is the only net: such a call never enters
/// <c>Client.Post</c>, where routing and the latch sit.</para>
///
/// <para><b>Which suites host it, and the one that does not.</b> Every suite that runs without a controller
/// hosts it — those in the default verification, and the end-to-end suite, whose desktop leg is outside
/// every default run and would otherwise be the one place an unsafe site could be added unpoliced. The
/// architecture suite is the deliberate exception: it hosts the SEEDED violator that proves the scan can
/// fail, and a violator planted where the guard runs would fail the very rule it exists to arm. It links the
/// scan without the guard, and <c>ControllerReachArchitectureTests</c> drives the same predicate over its own
/// seeds instead.</para>
///
/// <para><b>The exemption list is a dictionary of site to reason, not a bare list.</b> Three shapes
/// legitimately need admitting. A site that builds a real service and calls nothing on it is safe because no
/// message is ever sent. A site whose service DOES call is safe when its endpoint is a controller-stub
/// endpoint: the SDK routes such an endpoint to a registered stub and refuses it when none is registered, so
/// no call can reach a controller. And a latch probe calls a real endpoint on purpose while a stub is
/// registered, to see the latch refuse it, on a host under the reserved <c>.invalid</c> name that never
/// resolves. A reason is what stops the next entry being added because the gate was in the way, so each entry
/// states which of the three it is.</para>
/// </summary>
[SetUpFixture]
[SuppressMessage("Design", "CA1050:Declare types in namespaces",
    Justification = "Only a SetUpFixture in no namespace applies to every test in the assembly.")]
public sealed class ControllerReachGuard
{
    /// <summary>
    /// Sites allowed to construct a real service anyway. Empty for most suites: an entry here is a claim that
    /// building one is safe, which is only true while nothing is called on it, or while its endpoint is a
    /// controller-stub endpoint.
    /// </summary>
    internal static readonly Dictionary<string, string> Admitted = new()
    {
        ["Ihc.Tests.ReflectionUtilRealTest.get_Service"] =
            "constructs a real AuthenticationService and calls NO operation on it: the shared reflection "
            + "contract asks what the runtime says about the TYPE, so constructing it is the whole arrange "
            + "and no message reaches a controller.",

        ["Ihc.Vis.Tests.ControllerBridgeTests.InjectingControllerWithoutMatchingAuth_IsRejected_NeverAuthenticatesAForeignSession"] =
            BridgeWiringReason,
        ["Ihc.Vis.Tests.ControllerBridgeTests.MatchedPairBridge_AuthenticatesTheExactAuthTheControllerWasBuiltFrom"] =
            BridgeWiringReason,

        ["Ihc.Tests.ServiceConstructionTests.AuthenticationService_ConstructedFromSettings_OwnsItsCookieSession"] =
            ProductionConstructionReason,
        ["Ihc.Tests.ServiceConstructionTests.OpenAPIService_ConstructedFromASession_AdoptsThatSessionsCookieHandler"] =
            ProductionConstructionReason,
        ["Ihc.Tests.ServiceConstructionTests.OpenAPIService_ConstructedFromSettings_OwnsItsCookieSession"] =
            ProductionConstructionReason,
        ["Ihc.Tests.ServiceConstructionTests.ServiceBase_RefusesAnUnusableEndpoint"] =
            ProductionConstructionReason,

        ["Ihc.Tests.ControllerStubSeamTests.ServicesOn"] = StubEndpointReason,
        ["Ihc.Tests.StubbedSdk.Start"] = StubEndpointReason,
        ["Ihc.Tests.LabSmokeTests.ServicesOn"] = StubEndpointReason,
        ["safe_visual_stubbed_tests.RequestDestinationCheck.Canary"] = StubEndpointReason,
        ["Ihc.Tests.ControllerStubSwitchTests.LoginFrom"] = StubEndpointReason,

        ["Ihc.Tests.ControllerStubLatchTests.ProbeOn"] = LatchProbeReason,

        // Each suite that runs latched proves it from its own namespace, with one probe.
        ["Ihc.Vis.Tests.TestSetup.ProveTheRunIsLatched"] = LatchProbeReason,
        ["Ihc.Tests.LabRunLatchProbe.ProveTheRunIsLatched"] = LatchProbeReason,
        ["Ihc.Tests.UnitRunLatchProbe.ProveTheRunIsLatched"] = LatchProbeReason,
        ["safe_visual_tests.RunLatchProbe.ProveTheRunIsLatched"] = LatchProbeReason,
        ["safe_visual_stubbed_tests.RunLatchProbe.ProveTheRunIsLatched"] = LatchProbeReason,
        ["safe_visual_e2e_tests.DesktopSessionRequirement.ProveTheRunIsLatched"] = LatchProbeReason,

        ["Ihc.Tests.ControllerStubSeamTests.AnUnregisteredMockEndpoint_IsRefusedWhenTheServiceIsBuilt_WithBothWaysOut"] =
            "builds a real service on a mock:// endpoint no stub is registered for, to assert that construction "
            + "REFUSES it: the constructor throws before any client exists, so no service is built and nothing "
            + "can be called.",
    };

    /// <summary>
    /// Shared by the sites whose services are CALLED, and safe for a different reason than the construct-only
    /// ones: where the call goes. A stub endpoint is <c>mock://</c> or a host under the reserved
    /// <c>.controllerstub.invalid</c> name, which never resolves, and the SDK's transport sends a stub endpoint
    /// only to the stub registered for it, in the process or on a loopback host, and refuses it when none is.
    /// </summary>
    private const string StubEndpointReason =
        "operations ARE called; the endpoint is a controller-stub endpoint, mock:// or a host under the reserved "
        + ".controllerstub.invalid name, which never resolves. The SDK routes it only to the stub registered for "
        + "it, in this process or on a loopback host, and refuses it when none is registered, so no call reaches "
        + "a controller.";

    /// <summary>
    /// For a site that builds a service on a REAL endpoint and calls it while a stub is registered, to assert
    /// that the latch refuses the call. Safe even if the latch regressed: the endpoint is a host under the
    /// reserved <c>.invalid</c> name, which never resolves, and the settings carry no credential.
    /// </summary>
    private const string LatchProbeReason =
        "operations ARE called on a non-stub endpoint while a controller stub is registered, so the latch must "
        + "refuse them before any socket client exists; should it fail to, the endpoint's host is under the "
        + "reserved .invalid name, which never resolves, and the settings carry no credential.";

    /// <summary>
    /// Shared by the sites that exercise the PRODUCTION constructor itself. What they assert is what
    /// construction WIRED - the settings instance, the cookie handler instance, or the refusal of an
    /// unusable endpoint - which a fake cannot answer, since a fake would report whatever it was told. No
    /// operation is called on any service built there, and the endpoint they configure is under the
    /// reserved <c>.invalid</c> TLD, which is guaranteed never to resolve - unlike <c>.local</c>, which is
    /// mDNS and answers on a LAN.
    /// </summary>
    private const string ProductionConstructionReason =
        "builds real services through their public constructor to assert that construction shares the "
        + "session's settings and cookie handler - the invariant ARCHITECTURE.md states and no fake can "
        + "answer. No operation is called on them, and the configured endpoint is under the reserved "
        + ".invalid TLD.";

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

    /// <summary>
    /// Every site in this suite that can reach a controller, scanned once: the gate below and the admission check
    /// ask the same question of the same assembly. The marker interface and the transport doors come from the scan
    /// itself, so this suite and the architecture suite that proves its exclusions cannot come to scan by different
    /// ones.
    /// </summary>
    internal static readonly Lazy<IReadOnlyList<string>> Sites =
        new(() => ControllerReachScan.Sites(typeof(ControllerReachGuard).Assembly, ControllerReachScan.AnchorsFor(Admitted)));

    /// <summary>The rule. A suite that may not reach a controller must not be able to build one.</summary>
    [OneTimeSetUp]
    public void StopTheRunIfThisSuiteCanReachAController()
    {
        var offending = Sites.Value.Where(site => !Admitted.ContainsKey(site)).ToList();

        Assert.That(offending, Is.Empty,
            "this suite may not reach a live controller: its state changes are destructive and a test run "
            + "cannot undo them, so no test in it runs while it can. Replace the controller below the application "
            + "services: run the real SDK over a registered controller stub, building the services on the "
            + "registration's Endpoint, or put a fake on the low-level IIHCApiService implementation. Build a "
            + "substituted transport through Client.CreateHttpClient over a handler this suite declares, never a bare "
            + "HttpClient, a socket handler, a socket or web client, a WebRequest or a generated Ihc.Soap client, "
            + "which reach the real network. Then admit a site that builds real services in ControllerReachGuard "
            + "with the reason true of it: a stub endpoint (StubEndpointReason), a latch probe on a .invalid host "
            + "(LatchProbeReason), or a service on which nothing is called");
    }
}

/// <summary>
/// An admitted site that no longer constructs anything is a permission nobody needs, and a permission nobody needs
/// is how the next unsafe site gets waved through. A test rather than part of the gate, because a stale entry
/// endangers nothing and should not stop a run. Checked only where the site exists: the guard runs in several
/// assemblies and an entry describes one of them.
/// </summary>
[TestFixture]
[SuppressMessage("Design", "CA1050:Declare types in namespaces",
    Justification = "Shares the reach guard's file, which declares no namespace so the guard covers the whole assembly.")]
public sealed class ControllerReachAdmissions
{
    [Test]
    public void EveryAdmittedSiteInThisSuiteStillBuildsOne()
    {
        Assembly suite = typeof(ControllerReachGuard).Assembly;
        var built = ControllerReachGuard.Sites.Value.ToHashSet();

        var declaredHere = ControllerReachGuard.Admitted.Keys
            .Where(site => suite.GetType(site[..site.LastIndexOf('.')]) is not null)
            .ToList();

        Assert.That(declaredHere.Where(site => !built.Contains(site)), Is.Empty,
            "an admitted site in this assembly no longer constructs a controller service — delete the entry; "
            + "a permission that describes nothing is how the list stops being read");
    }
}
