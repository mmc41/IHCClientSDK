using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Session;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// D10: a callback handed to a dialog window runs on that window's stack, AFTER the flow that supplied it has
/// left its error boundary behind. Anvend is such a callback, and a fault in it used to reach only the window's
/// <c>HandlerGuard</c> — floor 3, which logs and returns — so the installer who pressed the button saw nothing.
///
/// <para><b>Wrapped at the SUPPLIER.</b> <c>PropertiesDialogCoordinator</c> already holds the view-model's
/// boundary; the windows deliberately hold no view-model reference. Wrapping at the consumer would have meant
/// giving a view one, which the layering rules forbid — so the wrap goes where the callback is created, and the
/// two architecture rules that make that the right point stay green.</para>
/// </summary>
[TestFixture]
public class PinPropertiesAnvendFloorTests
{
    /// <summary>
    /// A shell with one data-line product placed, its first pin's Properties opened, and the dialog pressing
    /// Anvend before it answers.
    /// </summary>
    private static async Task<ShellHarness> RunAnvendAsync(Action<ShellHarness> arrange)
    {
        ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var product = harness.ProjectService.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie", StringComparison.Ordinal) && p.Resources.Count > 1);
        await harness.Session.AddProductAsync(
            vm.InstallationNodes[0].Children[0].ElementId!.Value, product.ProductIdentifier);

        arrange(harness);
        // Cancel, so the flow ends WITHOUT applying — then Anvend is pressed afterwards, off the flow's stack.
        harness.Dialogs.PinPropertiesResult = null;

        await vm.PropertiesCommand.ExecuteAsync(
            vm.InstallationNodes[0].Children[0].Children[0].Children[0]);
        return harness;
    }

    /// <summary>
    /// Presses Anvend the way the real window does: AFTER the opening call returned, so the callback runs on a
    /// stack that is no longer inside the flow's own error boundary. Invoking it inside the awaited call — which
    /// is the easy thing for a fake to do — would put it back inside that boundary and prove nothing.
    /// </summary>
    private static Task PressAnvendAsync(ShellHarness harness) =>
        harness.Dialogs.LastPinApply!(new PinPropertiesResult(1, 3, "Sort", "", false));

    /// <summary>
    /// The gate's assertion: a fault inside Anvend reaches FLOOR 1 — the installer gets the Danish dialog, rather
    /// than a log entry nobody is looking at.
    /// </summary>
    [Test]
    public async Task AFaultInsideAnvendReachesTheDanishDialog()
    {
        using ShellHarness harness = await RunAnvendAsync(
            h => h.Session.StateChanged += (_, _) => throw new TimeoutException("anvend-boom-42"));

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditPinPropertiesCalls, Is.EqualTo(1),
                "non-vacuity: the dialog really opened");
            Assert.That(async () => await PressAnvendAsync(harness), Throws.Nothing,
                "CONTAINED: without the wrap this throws straight out into the window's own stack");
            Assert.That(harness.Dialogs.LastProblem, Is.Not.Null,
                "floor 3 would have logged and returned; floor 1 tells the person who pressed the button");
        });
    }

    /// <summary>An Anvend that SUCCEEDS raises no dialog — the wrapper is a floor, not a tap.</summary>
    [Test]
    public async Task AnAnvendThatSucceedsRaisesNoDialog()
    {
        using ShellHarness harness = await RunAnvendAsync(_ => { });

        await PressAnvendAsync(harness);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditPinPropertiesCalls, Is.EqualTo(1));
            Assert.That(harness.Dialogs.LastProblem, Is.Null);
            Assert.That(harness.Session.IsDirty, Is.True, "and the apply really applied");
        });
    }

    /// <summary>
    /// Anvend reports to the scope its OWN boundary started, not to the one that opened the dialog.
    ///
    /// <para>The boundary hands each body the scope it created; a body that ignores that parameter and closes
    /// over its caller's instead compiles, runs, and is invisible from the outside — the two are the same type
    /// and the same name. The consequence is not a lost record but a misfiled one: a refused Anvend marks the
    /// opening operation as failed, its own span still reads <c>ok</c>, and because the last outcome before
    /// disposal wins, a subsequent OK cannot take the wrong mark back off.</para>
    ///
    /// <para>Asserted by scope IDENTITY rather than through the span, deliberately. Only a FAILED edit outcome
    /// reaches a scope at all from here — a refusal narrates itself on the status line by design — and the shell
    /// has no seam that makes <c>UpdatePin</c> fault, so the misfiling this pins would show on no span any test
    /// can produce. Identity is the same fact one level in, and it is available.</para>
    /// </summary>
    [Test]
    public async Task AnvendReportsOnTheScopeItsOwnBoundaryStarted()
    {
        using ShellHarness harness = ShellHarness.Create();
        await harness.Session.NewAsync();
        var product = harness.ProjectService.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie", StringComparison.Ordinal) && p.Resources.Count > 1);
        await harness.Session.AddProductAsync(
            harness.Session.Current!.Groups[0].Id!.Value, product.ProductIdentifier);
        ElementId pinId = harness.Session.Current!.Root.DescendantsAndSelf()
            .First(e => e.Kind == ElementKind.DatalinePin).Id!.Value;

        Ihc.OperationTelemetry telemetry =
            new(ihc_openvisual.Configuration.AppTelemetryRegistry.Surface, nameof(PinPropertiesAnvendFloorTests));
        List<Ihc.OperationScope> reportedTo = [];
        Ihc.OperationScope? anvendScope = null;
        string? guardedOperation = null;

        async Task Guarded(string operation, Func<Ihc.OperationScope, Task> body)
        {
            using Ihc.OperationScope inner = telemetry.Start(operation);
            (guardedOperation, anvendScope) = (operation, inner);
            await body(inner);
        }

        var coordinator = new PropertiesDialogCoordinator(
            harness.Session, harness.Dialogs,
            (scope, _, _, _) => { reportedTo.Add(scope); return Task.CompletedTask; },
            _ => { },
            Guarded);

        // Anvend WHILE the opening call is still awaited — the real modal's shape, and the only one in which the
        // outer scope is still alive to be marked by mistake. Annuller afterwards, so Anvend is the sole commit.
        harness.Dialogs.PinPropertiesApply = new PinPropertiesResult(1, 3, "Sort", string.Empty, false);
        harness.Dialogs.PinPropertiesResult = null;

        using Ihc.OperationScope opening = telemetry.Start("Properties");
        await coordinator.OpenAsync(opening, pinId);

        Assert.Multiple(() =>
        {
            Assert.That(guardedOperation, Is.EqualTo(PropertiesDialogCoordinator.AnvendOperation),
                "non-vacuity: the boundary under test is the one Anvend raises");
            Assert.That(reportedTo, Has.Count.EqualTo(1), "and it committed exactly once");
            Assert.That(reportedTo[0], Is.SameAs(anvendScope),
                "the commit reports to Anvend's own scope, so a refused address marks the operation that "
                + "actually refused");
            Assert.That(reportedTo[0], Is.Not.SameAs(opening),
                "and NOT to the opening operation, whose outcome would then describe someone else's failure");
        });
    }
}
