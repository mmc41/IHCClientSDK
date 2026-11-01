using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ihc_openvisual.Configuration;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Stubbing;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace safe_visual_stubbed_tests;

/// <summary>
/// OpenVisual composed the way its <c>Main</c> composes it when started with the controller-stub switch: the switch
/// applied to the settings OpenVisual has with no settings file, against a loopback stub host, and the configuration
/// rebuilt from what the switch leaves.
/// </summary>
[TestFixture]
public class OpenVisualLoopbackSwitchTests
{
    private const string Suffix = " – STUB-controller (ingen rigtig controller)";

    [Test]
    public async Task ComposedFromTheSwitch_OpenVisualIsStubTitled_AndTheOnlySocketRequestIsThePreflight()
    {
        await using ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(new ControllerStub(), CancellationToken.None);
        using TelemetryCapture runtime = TelemetryCapture.Listen(RequestDestinations.RuntimeHttpScope);
        using var noFile = new ScratchDir("ihc_ov_switch_");
        var blank = new AppConfiguration(noFile.Path);

        var (effective, _) = ControllerStubs.ApplySwitch([ControllerStubs.SwitchArgument + "=" + host.BaseAddress], blank.IhcSettings);
        var composed = new AppConfiguration(blank, effective);
        using var harness = ShellHarness.Create();
        MainWindowViewModel viewModel = harness.CreateViewModel(config: composed, controllerStubInUse: effective.IsStubConfigured());
        await viewModel.InitializeAsync();
        await viewModel.ShowSettingsCommand.ExecuteAsync(null);

        var socketRequests = RequestDestinations.RuntimeRequests(runtime.Spans);
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Title, Is.EqualTo("unavngivet - IHC OpenVisual" + Suffix));
            Assert.That(harness.Dialogs.LastSettingsText, Does.Contain("Slutpunkt: " + effective.Endpoint));
            Assert.That(host.IdentifyRequests, Is.GreaterThan(0), "the switch checked the host's identity");
            Assert.That(socketRequests, Has.Count.EqualTo(1),
                "composing OpenVisual calls no controller, so the one request a socket carried is the switch's preflight");
            Assert.That(socketRequests.Select(request => request.GetTagItem("server.address")), Is.All.EqualTo(RequestDestinations.Loopback));
        });
    }
}
