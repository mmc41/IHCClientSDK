using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Session;

namespace safe_visual_tests;

/// <summary>
/// That the snapshot actually reaches an automation peer — and that without the flag there is nothing on the
/// peer to reach.
/// </summary>
/// <remarks>
/// <para><b>Both states, because a gate tested in one state is not tested.</b> The enabled half proves the
/// transport works; the disabled half proves the un-flagged configuration — the one every user runs — is not
/// merely unexercised. Reaching both is only possible because activation is an injected VALUE: a static read
/// from <c>Main</c> would be permanently false here, since nothing that hosts this window in a test runs
/// <c>Main</c> at all.</para>
///
/// <para><b>On <c>ItemStatus</c>.</b> Avalonia's own XML documentation for the attached property says it
/// "currently has no effect". That remark is stale, and this fixture is half of the evidence: the peer returns
/// what was set. The other half is the desktop end-to-end suite, whose row addressing already round-trips
/// <c>ItemStatus</c> through the real Windows UIA bridge. Do not "fix" the code to stop using it.</para>
/// </remarks>
public class AutomationSnapshotPeerTests : AvaloniaTestBase
{
    [AvaloniaTest]
    public async Task WithTheFlag_TheMainWindowPeerCarriesTheSnapshot_AndTracksAnEdit()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();

        InternalErrorLog faults = new();
        using AutomationSnapshotPublisher publisher = new(
            enabled: true, snapshot => AutomationProperties.SetItemStatus(window, snapshot),
            harness.Session, faults);

        SnapshotRead atStart = AutomationSnapshot.Read(PeerStatus(window));
        await harness.Session.ApplyAsync(new AddLocality("Ny lokalitet"));
        SnapshotRead afterEdit = AutomationSnapshot.Read(PeerStatus(window));

        Assert.Multiple(() =>
        {
            Assert.That(atStart.Rejection, Is.Null, atStart.Rejection);
            Assert.That(atStart.Value, Is.Not.Null,
                "the peer carried nothing, so the transport this whole surface rides on does not work");
            Assert.That(afterEdit.Value!.Value.Version, Is.GreaterThan(atStart.Value!.Value.Version),
                "the peer still reports the state from before the edit — a driver waiting on it would settle "
                + "on a stale reading, which is the defect the snapshot exists to remove");
        });
    }

    [AvaloniaTest]
    public async Task WithoutTheFlag_ThePeerCarriesNothingAtAll()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();

        InternalErrorLog faults = new();
        using AutomationSnapshotPublisher publisher = new(
            enabled: false, snapshot => AutomationProperties.SetItemStatus(window, snapshot),
            harness.Session, faults);

        await harness.Session.ApplyAsync(new AddLocality("Ny lokalitet"));

        Assert.That(AutomationSnapshot.Read(PeerStatus(window)).Absent, Is.True,
            "a session started as a user starts one published a snapshot; the flag is not gating publication");
    }

    private static string? PeerStatus(MainWindow window) =>
        ControlAutomationPeer.CreatePeerForElement(window).GetItemStatus();
}
