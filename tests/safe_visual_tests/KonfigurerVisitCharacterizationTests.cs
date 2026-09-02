using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace safe_visual_tests;

/// <summary>
/// The end state a <i>Konfigurer</i> visit must keep producing while the visit's internals are reworked into a
/// single commit: the installer opens a product's dialog, steps into a terminal's addressing editor and comes back
/// out through OK, and the document afterwards carries BOTH what the product dialog changed and what the terminal
/// editor changed.
/// <para>
/// Deliberately a pin on the RESULT and nothing else. How many commands the visit produces, how deep the undo
/// stack ends up, and whether the terminal editor's own OK is what reaches the document are all about to change,
/// so a test that asserted any of them would forbid the rework rather than protect it.
/// </para>
/// </summary>
public class KonfigurerVisitCharacterizationTests
{
    // A structural snapshot of the whole document: every element's tag, identity and name, in tree order, plus the
    // id counter. Enough to see a partially-inserted product that a product-count assertion would miss.
    private static IReadOnlyList<string> Snapshot(Project project) =>
        [.. project.Root.DescendantsAndSelf().Select(e =>
            $"{e.Tag}|{e.Id?.ToToken() ?? "-"}|{project.View(e).Name}"),
          $"lastUniqueId|{project.LastUniqueId}"];

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

        ProjectElement placed = harness.Session.Current!.FindById(product)!;
        ProjectElement terminal = placed.DescendantsAndSelf()
            .First(e => e.Kind == ElementKind.DatalinePin);
        return (harness, vm, product, terminal.Id!.Value);
    }

    private static TreeNodeViewModel ProductNode(MainWindowViewModel vm, ElementId product) =>
        TreeNodes.FindById(vm.InstallationNodes, product)!;

    // The insert route the installer takes: a leaf of the Products menu, which raises the dialog as part of placing.
    private static ProductMenuItemViewModel FirstWiredLeaf(MainWindowViewModel vm)
    {
        static ProductMenuItemViewModel? Leaf(IEnumerable<ProductMenuItemViewModel> nodes)
        {
            foreach (ProductMenuItemViewModel node in nodes)
            {
                if (node.IsLeaf)
                    return node;
                if (node.Children.Count > 0 && Leaf(node.Children) is { } found)
                    return found;
            }
            return null;
        }
        return Leaf(vm.ProductsMenu.First(c => c.Header == CatalogMenu.WiredProductsCategory).Children)!;
    }

    /// <summary>
    /// The visit: OK on the product dialog having typed a note AND having activated a terminal row, then an address
    /// and a cable colour in the terminal editor, then out. Both halves are in the document when the visit ends.
    /// </summary>
    [Test]
    public async Task KonfigurerVisit_EndingInOk_LeavesProductDocumentationAndTerminalAddressing()
    {
        var (harness, vm, product, pin) = await ProductWithTerminalsAsync();
        using var _ = harness;

        int asked = 0;
        harness.Dialogs.StepIntoTerminalOnce(pin);
        harness.Dialogs.ProductDialogResponder = descriptor =>
        {
            // The installer presses Konfigurer first — the dialog stays open across that — and leaves by OK
            // afterwards, carrying whatever the fields hold. Two acts, so two channels: the STEP is scripted on
            // the stepper below, and this answers once the stepping is done.
            asked++;
            DialogDescriptorField note = descriptor.Groups.SelectMany(g => g.Fields)
                .First(f => f.Caption == "Note");
            return new ProductDialogEdits([new ProductDialogEdit(note.Target, note.Attribute, "besøgt")]);
        };
        bool isOutput = harness.Session.Current!.FindById(pin)!.IsOutputPin;
        harness.Dialogs.PinPropertiesResult =
            new PinPropertiesResult(DataLine: 2, Terminal: 3, CableColour: "Grøn", Note: "klemmenote",
                InitialValueOn: false);

        await vm.PropertiesCommand.ExecuteAsync(ProductNode(vm, product));

        Project after = harness.Session.Current!;
        var pinView = new PinView(after, after.FindById(pin)!);
        Assert.Multiple(() =>
        {
            Assert.That(after.View(after.FindById(product)!).Note, Is.EqualTo("besøgt"),
                "the product documentation the visit started with survives the visit");
            Assert.That(pinView.Address?.DataLine, Is.EqualTo(2), "the terminal is addressed");
            Assert.That(pinView.Address?.Terminal, Is.EqualTo(3));
            Assert.That(pinView.CableColour, Is.EqualTo("Grøn"), "and the terminal's documentation is stored");
            Assert.That(pinView.IsOutput, Is.EqualTo(isOutput), "the visit addressed the terminal it was given");
        });
    }

    /// <summary>
    /// Cancelling the dialog that PLACING a product raises rolls the whole product back — not merely its count:
    /// every element it brought with it is gone and the id counter is where it was.
    /// </summary>
    [Test]
    public async Task InsertVisit_Cancelled_RollsTheWholeProductBack()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectNode(vm.InstallationNodes[0].Children[0]);
        IReadOnlyList<string> before = Snapshot(harness.Session.Current!);

        harness.Dialogs.CancelProductDialog = true;
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)FirstWiredLeaf(vm).Command!).ExecuteAsync(null);

        Assert.That(Snapshot(harness.Session.Current!), Is.EqualTo(before).AsCollection,
            "Annuller leaves the document exactly as it was — no orphaned terminals, no burnt ids");
    }
}
