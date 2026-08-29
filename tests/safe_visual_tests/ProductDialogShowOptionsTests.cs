using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
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
/// WHERE the product dialog opens, as distinct from what it contains. A route arrives with a field to land on, a
/// terminal row already picked, or a sub-item already stepped into — and the three describe one arrival, which
/// is why they travel together.
///
/// <para>Driven with REAL composed descriptors rather than stubs: an id that no descriptor produces would let
/// the focus assertions pass against a shape the composer never emits.</para>
/// </summary>
public class ProductDialogShowOptionsTests : AvaloniaTestBase
{
    private static async Task<(ProductDialogViewModel Dialog, IReadOnlyList<ProductTerminal> Terminals)> DialogFor(
        string productIdentifier)
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        ProductDefinition definition = app.GetAvailableProducts()
            .First(p => p.ProductIdentifier == productIdentifier);
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId id = session.Apply(new AddProduct(locality, definition)).Value;
        await Task.CompletedTask;

        ProjectElement product = session.Current!.FindById(id)!;
        List<ProductTerminal> terminals = [.. product.Children
            .Where(c => c.Kind == ElementKind.DatalinePin)
            .Select(c => new ProductTerminal(
                session.Current!.View(c).Name ?? "", "", "", "", c.IsOutputPin, c.Id!.Value.ToToken()))];

        return (new ProductDialogViewModel(app.GetProductDialog(session.Current!, id), terminals), terminals);
    }

    private static ProductDialogWindow Shown(ProductDialogViewModel vm, ProductDialogShowOptions options)
    {
        var window = new ProductDialogWindow();
        window.Populate(vm, options);
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static string[] FocusedIds(Visual root) =>
        [.. root.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsFocused)
            .Select(c => AutomationProperties.GetAutomationId(c) ?? string.Empty)];

    [AvaloniaTest]
    public async Task AFieldIdFocusesExactlyThatOneControl()
    {
        (ProductDialogViewModel dialog, _) = await DialogFor("_0x2101");
        // An EDITABLE field: a read-only editor renders disabled, and a disabled control cannot take focus, so
        // asserting on one would fail for a reason that has nothing to do with the route.
        ProductDialogFieldViewModel field = dialog.AllFields.First(f => !f.IsReadOnly);
        string id = field.AutomationId;
        Assert.That(id, Does.StartWith("dlg."), "precondition: the composer really produces dlg.* ids");

        ProductDialogWindow window = Shown(dialog, new ProductDialogShowOptions(FocusAutomationId: id));

        Assert.That(FocusedIds(window), Is.EqualTo(new[] { id }).AsCollection,
            "exactly one control is focused, and it is the one the route named");

        window.Close();
    }

    /// <summary>
    /// An id the descriptor never composed focuses NOTHING. A route that promised such a field was wrong, and
    /// landing on some other control would hide that rather than show it.
    /// </summary>
    [AvaloniaTest]
    public async Task AnIdTheDialogDoesNotContainFocusesNothing()
    {
        (ProductDialogViewModel dialog, _) = await DialogFor("_0x2101");

        ProductDialogWindow window = Shown(dialog,
            new ProductDialogShowOptions(FocusAutomationId: "dlg.der.findes.ikke"));

        Assert.That(FocusedIds(window), Has.None.EqualTo("dlg.der.findes.ikke"));

        window.Close();
    }

    /// <summary>
    /// A READ-ONLY field's id also focuses nothing — its editor renders disabled, and a disabled control cannot
    /// take focus. That is not a gap: the planner degrades a read-only attribute to a dialog-level route and
    /// never asks for one, so this pins the two halves agreeing rather than a limitation.
    /// </summary>
    [AvaloniaTest]
    public async Task AReadOnlyFieldsIdFocusesNothing()
    {
        (ProductDialogViewModel dialog, _) = await DialogFor("_0x2101");
        ProductDialogFieldViewModel? readOnly = dialog.AllFields.FirstOrDefault(f => f.IsReadOnly);
        Assert.That(readOnly, Is.Not.Null, "precondition: this product really does offer a read-only field");

        ProductDialogWindow window = Shown(dialog,
            new ProductDialogShowOptions(FocusAutomationId: readOnly!.AutomationId));

        Assert.That(FocusedIds(window), Has.None.EqualTo(readOnly.AutomationId));

        window.Close();
    }

    [AvaloniaTest]
    public async Task ATerminalPinPreselectsThatRow()
    {
        (ProductDialogViewModel dialog, IReadOnlyList<ProductTerminal> terminals) = await DialogFor("_0x2101");
        Assert.That(terminals, Has.Count.GreaterThan(1),
            "precondition: more than one terminal, or 'the right row' is whichever row there is");
        ProductTerminal wanted = terminals[^1];

        ProductDialogWindow window = Shown(dialog,
            new ProductDialogShowOptions(SelectTerminalPin: wanted.PinId));

        IReadOnlyList<ProductDialogTerminalGridViewModel> grids =
            [.. dialog.Groups.SelectMany(g => g.TerminalGrids)];
        Assert.Multiple(() =>
        {
            Assert.That(grids, Is.Not.Empty, "precondition: this product has terminal grids");
            Assert.That(grids.Select(g => g.SelectedRow?.PinId), Does.Contain(wanted.PinId),
                "the row the route named is the selected one, not the first");
        });

        window.Close();
    }

    /// <summary>
    /// An initial action fires ONCE as the window opens, through the same door the installer's own gesture uses —
    /// so a route cannot reach an outcome the installer could not.
    /// </summary>
    [AvaloniaTest]
    public async Task AnInitialActionFiresExactlyOnceOnOpen()
    {
        (ProductDialogViewModel dialog, IReadOnlyList<ProductTerminal> terminals) = await DialogFor("_0x2101");
        ElementId pin = ElementId.ParseOrNull(terminals[0].PinId)!.Value;

        var window = new ProductDialogWindow();
        // Counted at the SEAM, not by watching the window close. It used to close on a step and the closure was
        // the observable; the window stays open now (T058), so the step itself is what there is to count — which
        // is also the thing the claim was ever about.
        int stepped = 0;
        window.Populate(
            dialog,
            new ProductDialogShowOptions(
                SelectTerminalPin: terminals[0].PinId,
                InitialAction: new ProductDialogWidgetAction(DialogWidgetKind.TerminalGrids, pin)),
            onStep: _ =>
            {
                stepped++;
                return Task.FromResult<ProductDialogRefresh?>(null);
            });
        CurrentTestWindow = window;

        window.Show();
        Dispatcher.UIThread.RunJobs();
        // A second pump must not fire it again: the handler unsubscribes itself, so "on open" means once.
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(stepped, Is.EqualTo(1),
                "the dialog stepped into the sub-item once — not zero times, and not once per dispatcher turn");
            Assert.That(window.IsVisible, Is.True, "and stayed open while it did");
        });
    }

    [AvaloniaTest]
    public async Task WithNoOptionsNothingIsFocusedAndNothingIsSteppedInto()
    {
        (ProductDialogViewModel dialog, _) = await DialogFor("_0x2101");
        int closed = 0;

        ProductDialogWindow window = Shown(dialog, ProductDialogShowOptions.None);
        window.Closed += (_, _) => closed++;
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(FocusedIds(window).Where(id => id.StartsWith("dlg.")), Is.Empty,
                "the ordinary open lands on no field in particular");
            Assert.That(closed, Is.Zero, "and steps into nothing");
        });

        window.Close();
    }
}
