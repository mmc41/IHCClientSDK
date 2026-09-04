using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ihc.Vis.Session;
using ihc_openvisual.Services;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The publisher behind the test surface: what it writes, when it writes it, and — the half that carries the
/// safety argument — that switching it off leaves the application computing exactly what it computed before.
/// </summary>
/// <remarks>
/// It names no Avalonia type, which is what lets it be tested here at all: the output is an
/// <see cref="Action{T}"/> the composition root supplies, so a test hands it a list instead of a window.
/// </remarks>
[TestFixture]
public class AutomationSnapshotPublisherTests
{
    private static string SampleProject() =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Project1-SimpelWired.vis");

    /// <summary>The composed pieces one publisher reads, kept together so a test disposes both.</summary>
    private sealed class Rig : IDisposable
    {
        public ShellHarness Harness { get; } = ShellHarness.Create();

        public InternalErrorLog Faults { get; } = new();

        public List<string> Published { get; } = [];

        public AutomationSnapshotPublisher Publish(bool enabled = true) =>
            new(enabled, Published.Add, Harness.Session, Faults);

        public void Dispose() => Harness.Dispose();
    }

    [Test]
    public async Task Enabled_PublishesAtConstruction_AndAgainWhenAnEditLands()
    {
        using Rig rig = new();
        await rig.Harness.Session.StartAsync();
        using AutomationSnapshotPublisher publisher = rig.Publish();
        int atConstruction = rig.Published.Count;

        await rig.Harness.Session.ApplyAsync(new AddLocality("Ny lokalitet"));

        Assert.Multiple(() =>
        {
            Assert.That(atConstruction, Is.EqualTo(1),
                "a driver reading before anything has moved must see the state, not an absent property — which "
                + "is indistinguishable from the surface being off");
            Assert.That(rig.Published.Count, Is.GreaterThan(atConstruction), "the edit published nothing");
            Assert.That(publisher.Current.Version, Is.GreaterThan(0),
                "'ver' is what answers 'did my edit land?', a question with no other answer today");
            Assert.That(publisher.Current.Dirty, Is.True);
        });
    }

    [Test]
    public async Task Disabled_PublishesNothingEver_ButStillComputesTheSameValue()
    {
        using Rig rig = new();
        await rig.Harness.Session.StartAsync();
        using AutomationSnapshotPublisher publisher = rig.Publish(enabled: false);

        await rig.Harness.Session.ApplyAsync(new AddLocality("Ny lokalitet"));

        Assert.Multiple(() =>
        {
            Assert.That(rig.Published, Is.Empty,
                "off is INERT: no subscription, no write, and so nothing for an assistive technology to announce");
            Assert.That(publisher.Current.Version, Is.GreaterThan(0),
                "the difference the flag makes is whether the value is PUBLISHED, not whether it exists");
        });
    }

    [Test]
    public async Task Disposed_StopsPublishing()
    {
        using Rig rig = new();
        await rig.Harness.Session.StartAsync();
        AutomationSnapshotPublisher publisher = rig.Publish();
        publisher.Dispose();
        int afterDispose = rig.Published.Count;

        await rig.Harness.Session.ApplyAsync(new AddLocality("Ny lokalitet"));

        Assert.That(rig.Published, Has.Count.EqualTo(afterDispose),
            "a disposed publisher still attached to the workflow's events is the leak every fixture pays for");
    }

    /// <summary>
    /// The oracle §8 names: <c>val == gen.ver</c> holds exactly when a result is bound AND not stale.
    /// </summary>
    /// <remarks>
    /// The <c>Result is not null</c> half is not decoration. <c>ValidationMonitor.IsStale</c> is false when
    /// NOTHING is bound — right for the panel, since nothing bound is not a stale result — so an oracle stated
    /// as "current iff not stale" would be wrong during the first run of every launch and immediately after
    /// every document replacement, which is to say constantly.
    /// </remarks>
    [Test]
    public async Task ValidationCurrency_HoldsExactlyWhenAResultIsBoundAndNotStale()
    {
        using Rig rig = new();
        using AutomationSnapshotPublisher publisher = rig.Publish();

        await rig.Harness.Session.StartAsync();
        AssertOracle(rig, publisher, "a fresh document, before the first run completes");

        await rig.Harness.SettleValidationAsync();
        AssertOracle(rig, publisher, "the first run bound");

        await rig.Harness.Session.ApplyAsync(new AddLocality("Ny lokalitet"));
        AssertOracle(rig, publisher, "an edit past the bound result");

        await rig.Harness.SettleValidationAsync();
        AssertOracle(rig, publisher, "the edit validated");

        await rig.Harness.Session.OpenAsync(SampleProject());
        AssertOracle(rig, publisher, "a different document, nothing bound for it yet");

        await rig.Harness.SettleValidationAsync();
        AssertOracle(rig, publisher, "the new document validated");
    }

    [Test]
    public async Task AFault_MovesTheCountAndNamesTheCode()
    {
        using Rig rig = new();
        await rig.Harness.Session.StartAsync();
        using AutomationSnapshotPublisher publisher = rig.Publish();

        rig.Faults.Append(ProblemsTestData.Fault("internal.rule-failed", "Regel fejlede.", "boom"));

        Assert.Multiple(() =>
        {
            Assert.That(publisher.Current.Faults, Is.EqualTo(1));
            Assert.That(publisher.Current.LastFault, Is.EqualTo("internal.rule-failed"),
                "a moved count says only THAT something faulted; the code is what says which");
            Assert.That(rig.Published[^1], Does.Contain("fault=1:internal.rule-failed"),
                "the fault reached the published string, not merely the value behind it");
        });
    }

    /// <summary>
    /// That the flag gates PUBLICATION and not BEHAVIOUR: the same route, driven twice, reaches the same
    /// domain state.
    /// </summary>
    /// <remarks>
    /// <b>Supporting evidence, not proof.</b> Driving one route twice cannot rule out a branch on a route this
    /// comparison did not take, and if the flag really only gates one property write the comparison is close to
    /// vacuous by construction. It is kept because it stops being vacuous the day somebody adds a branch. What
    /// actually enforces the boundary is the architecture gate over who may READ the flag.
    /// </remarks>
    [Test]
    public async Task NoDomainStateDiffers_WithTheFlagOrWithoutIt()
    {
        AutomationSnapshot withFlag = await DriveTheSameRoute(enabled: true);
        AutomationSnapshot withoutFlag = await DriveTheSameRoute(enabled: false);

        Assert.That(withFlag, Is.EqualTo(withoutFlag),
            "the two runs reached different application state, so the flag is gating more than publication");
    }

    private static async Task<AutomationSnapshot> DriveTheSameRoute(bool enabled)
    {
        using Rig rig = new();
        using AutomationSnapshotPublisher publisher = rig.Publish(enabled);

        await rig.Harness.Session.StartAsync();
        await rig.Harness.Session.ApplyAsync(new AddLocality("Ny lokalitet"));
        await rig.Harness.SettleValidationAsync();
        await rig.Harness.Session.UndoAsync();
        await rig.Harness.SettleValidationAsync();

        return publisher.Current;
    }

    private static void AssertOracle(Rig rig, AutomationSnapshotPublisher publisher, string at)
    {
        ValidationMonitor monitor = rig.Harness.Session.Validation;
        bool bound = monitor.Result is not null && !monitor.IsStale;

        Assert.That(publisher.Current.IsValidationCurrent, Is.EqualTo(bound),
            $"at '{at}': the published currency disagrees with the monitor's own staleness rule "
            + $"(Result is {(monitor.Result is null ? "null" : "bound")}, IsStale={monitor.IsStale}, "
            + $"snapshot='{publisher.Current.Format()}')");
    }
}
