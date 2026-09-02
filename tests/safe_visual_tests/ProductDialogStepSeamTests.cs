using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// Stepping into a sub-dialog no longer closes the product dialog.
///
/// <para>The old protocol closed this window, carried the chosen composite out as a result, and let the caller
/// raise a fresh one afterwards. Two things were wrong with that. The installer saw the dialog vanish and
/// reappear, which is not what the vendor does — the sub-dialog appears ON TOP of a parent that stays put. And
/// closing DESTROYED the window, so everything it held that had not reached the document was gone: a typed
/// value, the selected row, the scroll position.</para>
///
/// <para><b>One property is deliberately NOT tested here: that an invalid value blocks the step.</b> It holds —
/// the step runs the same <c>TryCommit</c> the OK button runs, so the guard is shared code already covered by
/// the OK tests — but it cannot be exercised through this seam on real data. Measured across every catalog
/// product: none offers both a terminal grid to step from and a rule-constrained field to break, so the scenario
/// has no product to run on. Building a stub descriptor for it would let the assertion pass against a shape the
/// composer never emits, which is the thing this suite exists to avoid.</para>
/// </summary>
public class ProductDialogStepSeamTests : AvaloniaTestBase
{
    private static (ProductDialogViewModel Dialog, IReadOnlyList<ProductTerminal> Terminals) DialogFor(
        string productIdentifier = "_0x2101")
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        ProductDefinition definition = app.GetAvailableProducts()
            .First(p => p.ProductIdentifier == productIdentifier);
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId id = session.Apply(new AddProduct(locality, definition)).Value;

        ProjectElement product = session.Current!.FindById(id)!;
        List<ProductTerminal> terminals = [.. product.Children
            .Where(c => c.Kind == ElementKind.DatalinePin)
            .Select(c => new ProductTerminal(
                session.Current!.View(c).Name ?? "", "", "", "", c.IsOutputPin, c.Id!.Value.ToToken()))];

        return (new ProductDialogViewModel(app.GetProductDialog(session.Current!, id), terminals), terminals);
    }

    /// <summary>Presses <i>Konfigurer</i> on the first terminal grid, exactly as the installer would.</summary>
    private static void PressConfigure(Window window)
    {
        Button configure = window.GetVisualDescendants().OfType<Button>()
            .First(b => Avalonia.Automation.AutomationProperties.GetAutomationId(b)
                ?.StartsWith("dlg.terminaler.konfigurer", StringComparison.Ordinal) == true);
        configure.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaTest]
    public void TheProductWindowIsTheSameInstanceAcrossAVisit()
    {
        (ProductDialogViewModel dialog, _) = DialogFor();
        var window = new ProductDialogWindow();
        List<ProductDialogWidgetAction> stepped = [];
        window.Populate(dialog, options: null, onStep: action =>
        {
            stepped.Add(action);
            return Task.FromResult<ProductDialogRefresh?>(null);
        });
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        PressConfigure(window);

        Assert.Multiple(() =>
        {
            Assert.That(stepped, Has.Count.EqualTo(1), "the step handler ran");
            Assert.That(stepped[0].Kind, Is.EqualTo(DialogWidgetKind.TerminalGrids));
            Assert.That(window.IsVisible, Is.True,
                "and the product dialog is STILL OPEN — the installer never saw it vanish");
        });

        window.Close();
    }

    /// <summary>
    /// A sub-dialog opened during the step is owned by the PRODUCT window, not by the shell behind it. Owned by
    /// the shell it would not be modal to the dialog that raised it, so the installer could reach back into the
    /// very values it was opened to change.
    /// </summary>
    [AvaloniaTest]
    public void TheSubDialogIsOwnedByTheProductWindowNotTheShell()
    {
        Window shell = new();
        CurrentTestWindow = shell;
        shell.Show();
        AvaloniaDialogService service = new() { Owner = shell };

        (ProductDialogViewModel dialog, _) = DialogFor();
        var product = new ProductDialogWindow();
        Window? subDialogOwner = null;
        product.Populate(dialog, options: null, onStep: _ =>
        {
            // Whatever the service would parent a modal on at this moment — the same resolution every dialog
            // goes through.
            subDialogOwner = AvaloniaDialogService.Innermost(service.Owner!);
            return Task.FromResult<ProductDialogRefresh?>(null);
        });
        product.Show(shell);
        Dispatcher.UIThread.RunJobs();

        PressConfigure(product);

        Assert.That(subDialogOwner, Is.SameAs(product),
            "the sub-dialog stacks on the product dialog, which is still open beneath it");

        product.Close();
        shell.Close();
    }

    /// <summary>
    /// With NO step handler the gesture does NOTHING — and, in particular, does not close the dialog.
    ///
    /// <para>This is the inversion of what stood here. The window used to close on a composite gesture and hand
    /// the action out as its result for the caller to act on and re-open behind; that protocol is gone (T058),
    /// because closing destroyed everything not yet committed — a typed value, a selected row, the scroll
    /// position — and the visit is one transaction now. A window with no handler cannot open a sub-dialog by
    /// itself, so the honest outcome is that nothing happens.</para>
    /// </summary>
    [AvaloniaTest]
    public void WithNoStepHandlerTheGestureDoesNothingAndTheDialogStaysOpen()
    {
        (ProductDialogViewModel dialog, _) = DialogFor();
        var window = new ProductDialogWindow();
        window.Populate(dialog);
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        PressConfigure(window);

        Assert.Multiple(() =>
        {
            Assert.That(window.IsVisible, Is.True, "the dialog stays open");
            Assert.That(window.AcceptedResult, Is.Null, "and accepts nothing on the way");
        });
    }
}
