using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// The real application started with the controller-stub switch: it runs against its launch's loopback stub and
/// reaches nothing else, the launch gate refuses a launch whose switch went elsewhere, and a switch the application
/// cannot use stops it before it opens a window.
/// </summary>
/// <remarks>
/// Desktop only: the headless driver composes the application in this process with no settings and takes no
/// switch, so none of this exists there. The gate's own logic and the single launch site are checked headless, in
/// <see cref="LaunchGateTests"/>.
/// </remarks>
[TestFixture]
public sealed class ControllerStubE2ETests
{
    private const string FixtureFile = "Project1-SimpelWired.vis";

    /// <summary>The application's process name, which a killed launch must leave no instance of.</summary>
    private const string ProcessName = "ihc_openvisual";

    [TearDown]
    public void CloseApp() => E2E.CloseApp();

    /// <summary>
    /// OpenVisual does not transfer projects yet, so the stub changes nothing a person can do with a controller:
    /// Send and Retrieve stay refused, and the stub hears nothing beyond the start-up identity check.
    /// </summary>
    [Test]
    [Category(E2E.DesktopOnly)]
    public void TheStubSmoke_PassesTheGate_AndSendAndRetrieveStillRefuse()
    {
        E2E.Launch(E2E.Fixture(FixtureFile));

        Assert.Multiple(() =>
        {
            Assert.That(E2E.RunOk("control", "enabled", "--id", "ToolbarSend").Flag("enabled"), Is.False, "Send");
            Assert.That(E2E.RunOk("control", "enabled", "--id", "ToolbarRetrieve").Flag("enabled"), Is.False, "Retrieve");
            Assert.That(E2E.LaunchStubJournal(), Is.Empty, "the stub heard no controller call");
        });
    }

    /// <summary>
    /// The gate reads its own launch's host, so a switch pointing at another stub fails it even though the
    /// application did start against a stub: the gate proves where THIS launch went, not that some stub was used.
    /// </summary>
    [Test]
    [Category(E2E.DesktopOnly)]
    public async Task ASwitchToAnotherHost_FailsTheGate_AndTheApplicationIsKilled()
    {
        await using ControllerStubLoopbackHost elsewhere = await ControllerStubLoopbackHost.StartAsync(new ControllerStub(), CancellationToken.None);

        Assert.Throws<AssertionException>(() => E2E.Launch(E2E.Fixture(FixtureFile), elsewhere.BaseAddress.ToString()));

        Assert.Multiple(() =>
        {
            Assert.That(elsewhere.IdentifyRequests, Is.GreaterThanOrEqualTo(1), "the application did start, against the other host");
            Assert.That(Process.GetProcessesByName(ProcessName), Is.Empty, "the gate killed the application before any action");
        });
    }

    [Test]
    [Category(E2E.DesktopOnly)]
    public void ABadSwitchValue_StopsTheStart_WithTheFatalStartupCode()
    {
        E2E.Envelope launch = E2E.LaunchExpectingRefusal("not-a-loopback-address");

        Assert.That(launch.Number("exitCode"), Is.EqualTo(ihc_openvisual.Program.FatalStartupExitCode));
    }

    [Test]
    [Category(E2E.DesktopOnly)]
    public async Task AValidSwitchValue_WithNoHostListening_StopsTheStart_WithTheFatalStartupCode()
    {
        string vacated;
        await using (ControllerStubLoopbackHost gone = await ControllerStubLoopbackHost.StartAsync(new ControllerStub(), CancellationToken.None))
        {
            vacated = gone.BaseAddress.ToString();
        }

        E2E.Envelope launch = E2E.LaunchExpectingRefusal(vacated);

        Assert.That(launch.Number("exitCode"), Is.EqualTo(ihc_openvisual.Program.FatalStartupExitCode));
    }
}
