using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;

namespace Ihc.Vis.Tests;

/// <summary>
/// THE VISIT IS THE TRANSACTION. Opening the product dialog, stepping into a terminal, addressing it and coming
/// back out through OK is one act — so it is one commit and one undo entry, and Annuller discards all of it.
///
/// <para>Before this, the sub-dialog wrote straight through: the addressing was already in the document by the
/// time the installer reached Annuller, so cancelling the dialog cancelled only the half they had not finished.
/// That is the failure this pins — a cancel that keeps some of the change is worse than no cancel at all,
/// because nothing on screen says which half survived.</para>
/// </summary>
public class ProductDialogVisitTransactionTests
{
    private static async Task<(ShellHarness Harness, MainWindowViewModel Vm, ElementId Product, ElementId Pin)>
        ProductWithTerminalsAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ProductDefinition definition = harness.ProjectService.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie", StringComparison.Ordinal) && p.Resources.Count > 0);
        ElementId product = (await harness.Session.AddProductAsync(
            vm.InstallationNodes[0].Children[0].ElementId!.Value, definition.ProductIdentifier))!.Value;
        ProjectElement pin = harness.Session.Current!.FindById(product)!.DescendantsAndSelf()
            .First(e => e.Kind == ElementKind.DatalinePin);
        return (harness, vm, product, pin.Id!.Value);
    }

    /// <summary>
    /// The visit as the installer performs it: press Konfigurer on the terminal, then leave the dialog by OK
    /// carrying whatever was typed.
    /// <para>The step and the final answer are separate returns, because they are separate acts — the dialog
    /// stays open across the first.</para>
    /// </summary>
    private static void ScriptVisit(ShellHarness harness, ElementId pin, string note)
    {
        harness.Dialogs.StepIntoTerminalOnce(pin);
        harness.Dialogs.ProductDialogResponder = descriptor => new ProductDialogEdits(
                [.. descriptor.Groups.SelectMany(g => g.Fields)
                    .Where(f => f.Caption == "Note")
                    .Select(f => new ProductDialogEdit(f.Target, f.Attribute, note))]);
    }

    /// <summary>
    /// How many entries the history holds, counted by exhausting it and putting it back. The session exposes
    /// only CanUndo, and the redo stack restores exactly what the undos took, so this leaves it as it found it.
    /// </summary>
    private static int UndoDepth(ShellHarness harness)
    {
        int depth = 0;
        while (harness.Session.CanUndo)
        {
            harness.Session.UndoAsync().GetAwaiter().GetResult();
            depth++;
        }
        for (int i = 0; i < depth; i++)
        {
            harness.Session.RedoAsync().GetAwaiter().GetResult();
        }
        return depth;
    }

    [Test]
    public async Task NothingReachesTheDocumentAcrossTheSubDialogHop()
    {
        var (harness, vm, product, pin) = await ProductWithTerminalsAsync();
        using var _ = harness;

        Project before = harness.Session.Current!;
        int depthBefore = UndoDepth(harness);
        string? colourBefore = before.FindById(pin)!.GetAttribute("cable_colour");

        // The pin editor's OK fires DURING the visit; the assertions below read the document at that moment.
        Project? duringHop = null;
        int depthDuringHop = -1;
        harness.Dialogs.PinPropertiesResponder = _ =>
        {
            duringHop = harness.Session.Current;
            depthDuringHop = UndoDepth(harness);
            return new PinPropertiesResult(2, 3, "Grøn", "klemme", InitialValueOn: false);
        };
        ScriptVisit(harness, pin, "besøgt");

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, product));

        Assert.Multiple(() =>
        {
            Assert.That(duringHop, Is.Not.Null, "precondition: the sub-dialog really opened");
            Assert.That(ReferenceEquals(duringHop, before), Is.True,
                "the document is the same instance it was — the hop wrote nothing");
            Assert.That(depthDuringHop, Is.EqualTo(depthBefore), "and added no undo entry");
            Assert.That(duringHop!.FindById(pin)!.GetAttribute("cable_colour"), Is.EqualTo(colourBefore));
        });
    }

    [Test]
    public async Task TheProductOkCommitsTheWholeVisitAsOneUndoEntry()
    {
        var (harness, vm, product, pin) = await ProductWithTerminalsAsync();
        using var _ = harness;
        int depthBefore = UndoDepth(harness);

        harness.Dialogs.PinPropertiesResponder = _ =>
            new PinPropertiesResult(2, 3, "Grøn", "klemme", InitialValueOn: false);
        ScriptVisit(harness, pin, "besøgt");

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, product));

        Project after = harness.Session.Current!;
        Assert.Multiple(() =>
        {
            Assert.That(after.View(after.FindById(product)!).Note, Is.EqualTo("besøgt"), "the product half");
            Assert.That(after.FindById(pin)!.GetAttribute("cable_colour"), Is.EqualTo("Grøn"),
                "and the terminal half");
            Assert.That(UndoDepth(harness) - depthBefore, Is.EqualTo(1),
                "ONE entry — the installer performed one act");
        });
    }

    [Test]
    public async Task AnnullerDiscardsAnAlreadyOkdSubDialogValue()
    {
        var (harness, vm, product, pin) = await ProductWithTerminalsAsync();
        using var _ = harness;
        string? colourBefore = harness.Session.Current!.FindById(pin)!.GetAttribute("cable_colour");
        int depthBefore = UndoDepth(harness);

        // The pin editor is OK'd — the installer really did address the terminal — and only THEN is the product
        // dialog cancelled.
        harness.Dialogs.PinPropertiesResponder = _ =>
            new PinPropertiesResult(2, 3, "Grøn", "klemme", InitialValueOn: false);
        harness.Dialogs.StepIntoTerminalOnce(pin);
        harness.Dialogs.ProductDialogResponder = _ => null;   // and then Annuller

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, product));

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditPinPropertiesCalls, Is.EqualTo(1),
                "precondition: the sub-dialog was opened and OK'd");
            Assert.That(harness.Session.Current!.FindById(pin)!.GetAttribute("cable_colour"),
                Is.EqualTo(colourBefore),
                "Annuller discards the addressing too — the installer cancelled the act, not half of it");
            Assert.That(UndoDepth(harness), Is.EqualTo(depthBefore), "and left no entry behind");
        });
    }

    /// <summary>
    /// What the dialog SHOWS after the hop is the visit's pending state — so the installer sees the address they
    /// just entered, on a document that still does not have it.
    /// <para>The alternative sources are both wrong here, which is why the re-projection exists: the document has
    /// not been told yet, and the dialog's own rendered rows are a rendering — deriving values back out of them
    /// would make the display its own source of truth.</para>
    /// </summary>
    [Test]
    public async Task TheGridShowsTheTypedValueWhileTheDocumentStillLacksIt()
    {
        var (harness, vm, product, pin) = await ProductWithTerminalsAsync();
        using var _ = harness;

        harness.Dialogs.PinPropertiesResponder = _ =>
            new PinPropertiesResult(2, 3, "Grøn", "klemmenote", InitialValueOn: false);

        // The ANSWER is given after the step and before the visit commits — the one moment at which "shown" and
        // "not yet written" are both observable, which is the whole claim.
        ProductTerminal? shownMidVisit = null;
        string? inDocumentMidVisit = null;
        int asked = 0;
        harness.Dialogs.StepIntoTerminalOnce(pin);
        harness.Dialogs.ProductDialogResponder = _ =>
        {
            asked++;
            shownMidVisit = harness.Dialogs.LastRefresh?.Terminals.FirstOrDefault(t => t.PinId == pin.ToToken());
            inDocumentMidVisit = harness.Session.Current!.FindById(pin)!.GetAttribute("cable_colour");
            return new ProductDialogEdits([]);
        };

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, product));

        Assert.Multiple(() =>
        {
            Assert.That(shownMidVisit, Is.Not.Null, "precondition: the grid was re-projected after the step");
            Assert.That(shownMidVisit!.CableColour, Is.EqualTo("Grøn"),
                "the value the installer typed is what the grid was told to show");
            Assert.That(shownMidVisit.Address, Is.Not.Empty,
                "and the address they chose, formatted as the grid shows it");
            Assert.That(inDocumentMidVisit, Is.Not.EqualTo("Grøn"),
                "while the DOCUMENT still lacks it — the visit has not committed yet");
        });
    }

    /// <summary>
    /// The direct route is UNCHANGED: the tree's own gesture on a pin has no visit to be pending inside, so it
    /// commits straight to the document. Same window, two commit semantics — stated because it would otherwise
    /// read as a regression.
    /// </summary>
    [Test]
    public async Task ThePinDialogOpenedFromTheTreeStillCommitsStraightToTheDocument()
    {
        var (harness, vm, _, pin) = await ProductWithTerminalsAsync();
        using var __ = harness;
        int depthBefore = UndoDepth(harness);

        harness.Dialogs.PinPropertiesResult =
            new PinPropertiesResult(2, 3, "Blå", "direkte", InitialValueOn: false);
        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, pin));

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current!.FindById(pin)!.GetAttribute("cable_colour"), Is.EqualTo("Blå"));
            Assert.That(UndoDepth(harness) - depthBefore, Is.EqualTo(1));
        });
    }
}
