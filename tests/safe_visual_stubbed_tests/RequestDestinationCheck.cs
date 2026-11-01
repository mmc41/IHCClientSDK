using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace safe_visual_stubbed_tests;

using Ihc;
using Ihc.Stubbing;
using Ihc.Tests.Shared;

/// <summary>
/// Every request this run sends goes to a stub, and that is checked from telemetry rather than assumed: the SDK's
/// HTTP spans must name a stub host, and the runtime's own HTTP activities, which see a request whether or not it
/// passed through the SDK's handler, may name nothing but the loopback address.
/// </summary>
/// <remarks>
/// A check over no requests passes, so the run starts with a canary: one call on a stub of its own, which must
/// produce exactly one SDK span to a stub host and, being in-process, no runtime request at all. OpenVisual
/// itself sends nothing yet, so without the canary the check could not be told apart from a capture wired to
/// nothing.
/// </remarks>
[SetUpFixture]
public class RequestDestinationCheck
{
    private TelemetryCapture? _sdk;
    private TelemetryCapture? _runtime;

    [OneTimeSetUp]
    public async Task StartCapturing_AndSendTheCanary()
    {
        _sdk = TelemetryCapture.Listen(Telemetry.ActivitySourceName);
        _runtime = TelemetryCapture.Listen(RequestDestinations.RuntimeHttpScope);

        var stub = new ControllerStub();
        using (ControllerStubRegistration registration = ControllerStubs.Register(stub))
        {
            await Canary(registration.Endpoint);
        }

        Assert.Multiple(() =>
        {
            Activity canary = RequestDestinations.SdkRequests(_sdk.Spans).Single();
            Assert.That(new IhcSettings { Endpoint = (string)canary.GetTagItem("url.full")! }.IsStubConfigured(), Is.True,
                "the canary's span names a stub host");
            Assert.That(stub.Journal.All, Has.Count.EqualTo(1), "the canary is one exchange");
            Assert.That(RequestDestinations.RuntimeRequests(_runtime.Spans), Is.Empty,
                "an in-process stub involves no socket handler, so the runtime sees no request");
        });
    }

    [OneTimeTearDown]
    public void EveryRequestWentToAStub()
    {
        try
        {
            if (_sdk is { } sdk && _runtime is { } runtime)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(RequestDestinations.SdkRequests(sdk.Spans), Is.Not.Empty, "at least the canary was seen");
                    Assert.That(RequestDestinations.NonStubDestinations(ExceptLoopbackIdentityChecks(RequestDestinations.SdkRequests(sdk.Spans))),
                        Is.Empty, "SDK requests whose host is not a stub host");
                    Assert.That(RequestDestinations.NonLoopbackDestinations(RequestDestinations.RuntimeRequests(runtime.Spans)), Is.Empty,
                        "runtime requests to anything but the loopback address");
                });
            }
        }
        finally
        {
            _sdk?.Dispose();
            _runtime?.Dispose();
        }
    }

    /// <summary>
    /// <paramref name="sdkRequests"/> without the switch's identity checks: the one SDK request a switch makes before
    /// any stub endpoint exists, so it is traced on the loopback stub host's own address. Every other request has to
    /// name a stub host.
    /// </summary>
    private static IEnumerable<Activity> ExceptLoopbackIdentityChecks(IEnumerable<Activity> sdkRequests) =>
        sdkRequests.Where(span => !(Uri.TryCreate(span.GetTagItem("url.full") as string, UriKind.Absolute, out Uri? url)
                                    && url.Host == RequestDestinations.Loopback
                                    && url.AbsolutePath.EndsWith("/identify", StringComparison.Ordinal)));

    /// <summary>
    /// The one place this suite builds a real service, admitted in ControllerReachGuard: its endpoint is a stub
    /// endpoint registered for it, which the SDK routes only to that stub.
    /// </summary>
    internal static async Task Canary(string stubEndpoint)
    {
        using var authentication = new AuthenticationService(new IhcSettings { Endpoint = stubEndpoint });
        await authentication.Ping();
    }
}
