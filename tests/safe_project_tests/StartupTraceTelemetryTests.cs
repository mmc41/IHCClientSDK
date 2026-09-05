using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// Start-up as ONE tree.
///
/// <para>The shell's constructor does real work — it builds the catalog menus off the SDK's component
/// catalog, builds both trees for the first time, and sweeps every command row's gate. Measured against the
/// four newest launches in the backend, each of those left a SINGLE-SPAN trace of its own: a
/// <c>ProjectAppService.GetAvailableFunctionBlocks</c> with no parent, a <c>MainWindowViewModel.TreeUpdate</c>
/// with no parent, a <c>CommandRegistry.OnContextChanged</c> with no parent. Fragments, not operations — a
/// reader asking "why was the window slow to appear" found four traces and no way to tell they were one
/// launch.</para>
///
/// <para>What this fixture pins is the SHAPE, not the timing: the constructor's work hangs off a named
/// operation. The other half — that the operation is in turn a child of the composition root's
/// <c>App.Startup</c>, so composition and the first project load share one trace — cannot be reached from a
/// controller-free suite, because it is the Avalonia composition root that opens it. It is verified against
/// the live backend instead, and §11 of <c>telemetry_points.md</c> carries the query.</para>
/// </summary>
[TestFixture]
public class StartupTraceTelemetryTests
{
    private const string ComposeSpan = "MainWindowViewModel.Compose";

    /// <summary>
    /// Both pieces of constructor work, asserted through their PARENT rather than through their own
    /// existence: they existed before, as roots. Being someone's child is the whole change.
    /// </summary>
    [Test]
    public void TheShellsConstructionIsOneOperation_AndItsWorkHangsOffIt()
    {
        using TelemetryCapture capture = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { ComposeSpan, "MainWindowViewModel.TreeUpdate", "CommandRegistry.OnContextChanged" });
        using TraceProbe probe = TraceProbe.Start();
        using ShellHarness harness = ShellHarness.Create();

        _ = harness.CreateViewModel();

        Activity compose = probe.Span(capture, ComposeSpan);
        Activity tree = probe.SpansNamed(capture, "MainWindowViewModel.TreeUpdate").First();
        Activity context = probe.SpansNamed(capture, "CommandRegistry.OnContextChanged").First();

        Assert.Multiple(() =>
        {
            Assert.That(tree.Parent, Is.SameAs(compose),
                "the first tree build is a phase OF constructing the shell, not a trace of its own");
            // The parent by IDENTITY, never merely "not null": a TraceProbe is ambient over the whole test, so
            // every span it owns has SOME parent and the weaker assertion would pass without the fix.
            Assert.That(context.Parent, Is.SameAs(compose),
                "the constructor's gate sweep runs inside the same operation");
            Assert.That(compose.Duration, Is.GreaterThanOrEqualTo(tree.Duration),
                "an operation that does not cover the work it parents is a stub, not a root");
        });
    }

    /// <summary>
    /// The SDK half: the catalog menus are built off <c>GetAvailableFunctionBlocks</c>, which is the span
    /// that was a root 4 launches out of 4. It belongs to the SDK's scope, so it takes a second capture —
    /// and the assertion is the parent's NAME, not merely that it has one: under a
    /// <see cref="TraceProbe"/> every span has a parent, so "not null" would pass without the fix.
    /// <para>Both scopes are listened to for a second reason: a source nothing listens to creates no span at
    /// all, so without the app capture the parent under test would never exist to be found.</para>
    /// </summary>
    [Test]
    public void TheCatalogMenuBuildHangsOffTheShellsConstruction()
    {
        using TelemetryCapture app = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName, spanNames: new[] { ComposeSpan });
        using TelemetryCapture sdk = TelemetryCapture.Listen(Ihc.Telemetry.ActivitySourceName,
            spanNames: new[] { "ProjectAppService.GetAvailableFunctionBlocks" });
        using TraceProbe probe = TraceProbe.Start();
        using ShellHarness harness = ShellHarness.Create();

        _ = harness.CreateViewModel();

        Activity[] menuBuilds = probe.SpansNamed(sdk, "ProjectAppService.GetAvailableFunctionBlocks").ToArray();

        Assert.That(menuBuilds, Is.Not.Empty, "constructing the shell builds the catalog menus");
        Assert.That(menuBuilds.Select(s => s.Parent?.OperationName), Is.All.EqualTo(ComposeSpan),
            "a catalog read with no parent is a fragment nothing can attribute to a launch");
    }
}
