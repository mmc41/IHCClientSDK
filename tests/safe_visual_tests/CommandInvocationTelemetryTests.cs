using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The command funnel: one count per invocation of a registered row.
///
/// Menu bar, toolbar, context flyout and gesture all materialize from ONE local function inside
/// <c>Register</c>, so counting there covers every route without the four surfaces having to agree on
/// anything. The claim is deliberately narrow - registered rows only, no surface dimension (the function
/// cannot see which surface invoked it) and no error dimension (it hands back the row's task without
/// observing it) - and these tests pin the narrowness as much as the counting.
/// </summary>
[TestFixture]
public class CommandInvocationTelemetryTests
{
    /// <summary>
    /// The gate's assertion: BOTH the ordinary command and the gesture command are counted, because both
    /// materialize from the same local function. A gesture that bypassed the count would make keyboard use
    /// look like no use at all.
    /// </summary>
    [Test]
    public async Task InvokingARowThroughBothTheCommandAndTheGesture_CountsBothKeyedByTheRowId()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" },
            instruments: new[] { "ihc.command.invocation" });
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.Registry.Commands["app.settings"].ExecuteAsync(null);
        await vm.Registry.GestureCommands["app.settings"].ExecuteAsync(null);

        CapturedPoint[] counts = capture.Points
            .Where(c => (string?)c.Tag("ihc.command.id") == "app.settings").ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(counts, Has.Length.EqualTo(2),
                "both routes go through the one shared local function, so both are counted");
            Assert.That(capture.Spans.Count(s => (string?)s.GetTagItem("ihc.command.id") == "app.settings"),
                Is.EqualTo(2), "one span per invocation, carrying the row id");
        });
    }

    /// <summary>
    /// The narrow claim, pinned: every counted id is a REGISTERED row. If anything outside the registry
    /// were ever counted here, the instrument would silently become something other than what it says.
    /// </summary>
    [Test]
    public async Task EveryCountedIdIsARegisteredRow()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" },
            instruments: new[] { "ihc.command.invocation" });
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.Registry.Commands["app.settings"].ExecuteAsync(null);
        await vm.Registry.Commands["file.new"].ExecuteAsync(null);

        string[] counted = capture.Points.Select(c => (string?)c.Tag("ihc.command.id") ?? string.Empty).ToArray();

        Assert.That(counted, Is.Not.Empty);
        Assert.That(counted.All(id => vm.Registry.Commands.ContainsKey(id)), Is.True,
            "an id outside the registered row set would mean the counter measures something else");
    }

    /// <summary>The documented absences, asserted so a later change cannot add them by accident.</summary>
    [Test]
    public async Task TheCountCarriesNoSurfaceAndNoErrorDimension()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "CommandRegistry.Invoke" },
            instruments: new[] { "ihc.command.invocation" });
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.Registry.Commands["app.settings"].ExecuteAsync(null);

        CapturedPoint count = capture.Points[0];
        Assert.That(count.Tags.Keys, Is.EquivalentTo(new[] { "ihc.edit.status", "ihc.command.id" }),
            "the registry cannot observe the surface or a failure, so it must not pretend to");
    }
}
