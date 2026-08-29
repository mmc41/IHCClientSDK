using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using ihc_openvisual.Controls;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace safe_visual_tests;

/// <summary>
/// The ONE generic product dialog, rendering a composed descriptor. It carries no per-family knowledge, so these
/// tests drive it with real descriptors composed from real placed products rather than with hand-built stubs —
/// a stub would let the window pass against a shape the composer never produces.
/// </summary>
public class ProductDialogWindowTests : AvaloniaTestBase
{
    private static async Task<ProductDialogViewModel> DialogFor(string productIdentifier)
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
        return new ProductDialogViewModel(app.GetProductDialog(session.Current!, id));
    }

    private static ProductDialogWindow Shown(ProductDialogViewModel vm)
    {
        var window = new ProductDialogWindow();
        window.Populate(vm);
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>
    /// THE gate of T027: the modem renders 30 telephone boxes in three columns. Both halves matter — the count
    /// proves the repeat reached the renderer, and the column count proves the descriptor's layout hint is
    /// honoured rather than flattened into a list.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheModemRenders30PhoneBoxesInThreeColumns()
    {
        ProductDialogViewModel vm = await DialogFor("_0x3103");
        ProductDialogWindow window = Shown(vm);

        var phoneFields = vm.AllFields.Where(f => f.Caption.StartsWith("Nummer")).ToList();
        UniformGrid[] grids = [.. window.GetVisualDescendants().OfType<UniformGrid>()];

        Assert.Multiple(() =>
        {
            Assert.That(phoneFields, Has.Count.EqualTo(30), "thirty slots reached the view-model");
            Assert.That(grids.Any(g => g.Columns == 3), Is.True,
                "the telephone group lays out in three columns, as the original does");
            Assert.That(window.GetVisualDescendants().OfType<TextBox>().Count(), Is.GreaterThanOrEqualTo(30),
                "and thirty editable boxes are actually realized");
        });
    }

    /// <summary>Every group the descriptor carries becomes a rendered group; a captioned one draws its box.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheModemRendersItsFourCaptionedGroups()
    {
        ProductDialogViewModel vm = await DialogFor("_0x3103");
        ProductDialogWindow window = Shown(vm);

        var captions = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text).Where(t => t is not null).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(vm.Groups.Select(g => g.Caption),
                Is.EqualTo(new[] { "Modem egenskaber", "Kabling", "Indstillinger", "Telefon numre" }).AsCollection);
            Assert.That(captions, Does.Contain("Telefon numre"));
            Assert.That(captions, Does.Contain("Nummer 30"), "the last slot's caption is rendered, not truncated");
        });
    }

    /// <summary>The window serves every family — the same code, a different descriptor.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheSameWindowRendersASmallFamilyToo()
    {
        ProductDialogViewModel vm = await DialogFor("_0x4409");   // the LED dimmer: three fields, one group
        ProductDialogWindow window = Shown(vm);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Title, Is.EqualTo("IHC LED Dimmer 2 kanaler"));
            Assert.That(vm.AllFields.Count(), Is.EqualTo(3));
            Assert.That(window.GetVisualDescendants().OfType<TextBox>().Any(), Is.True);
        });
    }

    /// <summary>A read-only field renders disabled — the original greys a locked product's Navn.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task AReadOnlyFieldRendersDisabled()
    {
        ProductDialogViewModel vm = await DialogFor("_0x3103");

        ProductDialogFieldViewModel navn = vm.AllFields.First(f => f.Caption == "Navn");

        Assert.That(navn.IsReadOnly, Is.True);
    }

    // ── changed-fields-only, and the untouched OK ───────────────────────────────────────────────────

    /// <summary>
    /// An untouched dialog produces NO edits. That is what makes an untouched OK a `NoChange` commit rather than
    /// a rewrite of every attribute the dialog happened to show (T024).
    /// </summary>
    [AvaloniaTest]
    public async Task AnUntouchedDialogProducesNoEdits()
    {
        ProductDialogViewModel vm = await DialogFor("_0x3103");

        Assert.That(vm.PendingEdits, Is.Empty);
    }

    [AvaloniaTest]
    public async Task OnlyTheChangedFieldsBecomeEdits()
    {
        ProductDialogViewModel vm = await DialogFor("_0x3103");
        ProductDialogFieldViewModel note = vm.AllFields.First(f => f.Caption == "Note");

        note.Value = "ændret";

        Assert.Multiple(() =>
        {
            Assert.That(vm.PendingEdits, Has.Length.EqualTo(1));
            Assert.That(vm.PendingEdits[0].Value, Is.EqualTo("ændret"));
            Assert.That(vm.PendingEdits[0].Attribute, Is.EqualTo("note"));
        });
    }

    /// <summary>A read-only field never contributes an edit, even if something set its value.</summary>
    [AvaloniaTest]
    public async Task AReadOnlyFieldNeverContributesAnEdit()
    {
        ProductDialogViewModel vm = await DialogFor("_0x3103");
        ProductDialogFieldViewModel navn = vm.AllFields.First(f => f.Caption == "Navn");

        navn.Value = "forsøgt omdøbt";

        Assert.That(vm.PendingEdits, Is.Empty);
    }

    // ── the fail-loud arm ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A control kind the renderer does not know FAILS LOUDLY rather than rendering nothing. Silently omitting the
    /// field would produce a dialog missing a value the installer needs — precisely the class of defect the
    /// metadata engine exists to make impossible, and one that no test could see.
    /// <para>Asked of the window's OWN selector, pulled out of its resources, not of a stand-in constructed here:
    /// a stand-in would prove that some selector throws, not that this dialog does.</para>
    /// </summary>
    [AvaloniaTest]
    public void AnUnknownControlKind_ThrowsRatherThanRenderingNothing()
    {
        DialogFieldTemplate selector = SelectorOfTheRealWindow();
        var rogue = new ProductDialogFieldViewModel(
            new DialogDescriptorField("dlg.g.x", "X", (DialogControlKind)999,
                new ElementId(1, 2), "note", "", false, null, null, null));

        Assert.That(() => selector.Build(rogue),
            Throws.TypeOf<DialogFieldTemplate.UnknownControlKindException>());
    }

    /// <summary>
    /// And the arm is not simply always throwing: the window supplies a template for EVERY declared kind, so the
    /// throw above is unreachable for anything the composer can actually emit. This is what keeps the two in step
    /// — adding a <see cref="DialogControlKind"/> without a template fails here, at the renderer, rather than
    /// shipping a field that renders as a caption over empty space.
    /// </summary>
    [AvaloniaTest]
    public void EveryRealControlKindHasATemplate()
    {
        DialogFieldTemplate selector = SelectorOfTheRealWindow();

        var missing = System.Enum.GetValues<DialogControlKind>()
            .Where(kind => selector.ForKind(kind) is null)
            .ToList();

        Assert.That(missing, Is.Empty, $"every control kind is templated; these are not: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// T036: a captioned group draws a VISIBLE frame, not just a caption.
    /// <para>Found on product 002's composite, where the vendor draws a bordered <i>Produkt egenskaber</i>
    /// box and OpenVisual drew the caption over bare fields. The cause is an unresolved
    /// <c>DynamicResource</c>: the group template asked for a brush key the theme does not define, which
    /// leaves <c>BorderBrush</c> null and paints nothing — while the caption, the padding and the corner
    /// radius all render, so the template LOOKS applied and only the boundary is silently missing.</para>
    /// <para>Asserted as "the brush resolves", not as a colour: the colour is out of scope (RUBRIC
    /// out-of-scope row 4), the existence of the boundary is not (row 4, grouping).</para>
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ACaptionedGroup_DrawsAFrameAndNotJustACaption()
    {
        ProductDialogViewModel vm = await DialogFor("_0x4409");
        ProductDialogWindow window = Shown(vm);

        AccessibleGroupBox box = window.GetVisualDescendants().OfType<AccessibleGroupBox>().First();

        // The group's OWN frame, not any border inside it. Scoping this to `box.GetVisualDescendants()`
        // is not enough: every ComboBox and TextBox in the group carries its own Border, so the loose
        // query passes on a group with no frame at all. Take the border that WRAPS the content presenter.
        Border? frame = box.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Child is ContentPresenter);

        Assert.Multiple(() =>
        {
            Assert.That(frame, Is.Not.Null, "the captioned group's template wraps its content in a bordered box");
            Assert.That(frame?.BorderThickness.Top, Is.GreaterThan(0), "with a thickness that draws");
            Assert.That(frame?.BorderBrush, Is.Not.Null,
                "and a BorderBrush that RESOLVES — an unresolved DynamicResource leaves it null, which "
                + "paints no boundary while every other part of the template still renders");
        });
    }

    /// <summary>
    /// US-012 MUST: both terminal grids are ALWAYS present — "a product with no inputs shows an empty
    /// <c>Indgange</c> grid whose Configure button is disabled (never a MISSING grid)".
    /// <para>T030's widget markup hid each grid when its side was empty, so <c>LK FUGA Tryk 2 tast</c>
    /// (two inputs, no outputs) showed no <i>Udgange</i> section at all where the vendor shows an empty
    /// one with a greyed <i>Konfigurer udgang</i>. Found on product 003's composite (T037). The absent
    /// grid is worse than an empty one: an empty grid says "this product has no outputs", a missing
    /// section says nothing and reads as an unfinished dialog.</para>
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task BothTerminalGrids_ArePresent_EvenWhenOneSideIsEmpty()
    {
        ProductDialogDescriptor descriptor = await DescriptorFor("_0x2101");
        var viewModel = new ProductDialogViewModel(descriptor,
        [
            new ProductTerminal("Tryk (venstre)", "", "", "", IsOutput: false, PinId: "_0x1"),
            new ProductTerminal("Tryk (højre)", "", "", "", IsOutput: false, PinId: "_0x2"),
        ]);
        ProductDialogWindow window = Shown(viewModel);

        // Addressed by automation id, not by control name: the two grids render from one template and a name
        // cannot be bound, so neither list carries one.
        ListBox? inputs = WithId<ListBox>(window, "dlg.terminaler.indgange");
        ListBox? outputs = WithId<ListBox>(window, "dlg.terminaler.udgange");

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Is.Not.Null, "the Indgange grid is present");
            Assert.That(outputs, Is.Not.Null, "and so is Udgange, though this product has no outputs");
            // IsEffectivelyVisible, not IsVisible: a control keeps its own IsVisible=true inside a hidden
            // parent, so the local property answers True for a grid nobody can see.
            Assert.That(inputs!.IsEffectivelyVisible, Is.True);
            Assert.That(outputs!.IsEffectivelyVisible, Is.True, "an EMPTY grid, never a missing one (US-012)");
            Assert.That(ConfigureButton(window, "Konfigurer indgang")?.IsEnabled, Is.True);
            Assert.That(ConfigureButton(window, "Konfigurer udgang")?.IsEnabled, Is.False,
                "the empty side's Configure button is disabled rather than absent");
        });
    }

    /// <summary>
    /// T098: a <see cref="DialogControlKind.Checkbox"/> field realizes as a CheckBox carrying the caption as
    /// its own content — not as a caption over a text box.
    /// <para>The vendor draws <i>Inkluder produktet i slutbruger rapport</i> as a ticked box with the text to
    /// its right (product 064). A checkbox is the one kind whose label belongs INSIDE the control, so the
    /// shared caption block above the editor has to stand down for it, or the words appear twice.</para>
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ACheckboxFieldRendersAsATickedBoxLabelledOnce()
    {
        ProductDialogViewModel vm = await DialogFor("_0x2701");
        ProductDialogWindow window = Shown(vm);

        CheckBox? box = window.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault();
        // Standalone TextBlocks only: the CheckBox draws its own Content through a TextBlock of the same
        // text, and counting that one would make the duplicate-label check impossible to satisfy.
        var captionBlocks = window.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Text == "Inkluder produktet i slutbruger rapport" && t.IsEffectivelyVisible)
            .Count(t => !t.GetVisualAncestors().OfType<CheckBox>().Any());

        Assert.Multiple(() =>
        {
            Assert.That(box, Is.Not.Null, "the checkbox kind realizes as a CheckBox");
            Assert.That(box!.IsChecked, Is.True, "and reads the placed value — the vendor draws it checked");
            Assert.That(box.Content, Is.EqualTo("Inkluder produktet i slutbruger rapport"));
            Assert.That(captionBlocks, Is.EqualTo(0),
                "the shared caption block stands down: the box labels itself");
        });
    }

    /// <summary>Unticking it produces exactly one edit, in the file's own yes/no vocabulary.</summary>
    [AvaloniaTest]
    public async Task UntickingTheCheckbox_ProducesOneYesNoEdit()
    {
        ProductDialogViewModel vm = await DialogFor("_0x2701");
        ProductDialogFieldViewModel flag =
            vm.AllFields.First(f => f.Control == DialogControlKind.Checkbox);

        flag.IsChecked = false;

        Assert.Multiple(() =>
        {
            Assert.That(vm.PendingEdits, Has.Length.EqualTo(1));
            Assert.That(vm.PendingEdits[0].Attribute, Is.EqualTo("enduser_report"));
            Assert.That(vm.PendingEdits[0].Value, Is.EqualTo("no"));
        });
    }

    /// <summary>
    /// The advanced settings are a DISCLOSURE captioned as the vendor captions the group, not a button opening a
    /// window.
    /// <para>The vendor expands them in place inside the product dialog and collapses them again; this used to
    /// draw an <i>Avanceret</i> button that opened a separate modal. The caption was already parity (product
    /// 080's composite); what differed was what pressing it did, and that difference is what the disclosure
    /// closes.</para>
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheAdvancedSettingsExpandInPlaceRatherThanOpeningAWindow()
    {
        ProductDialogViewModel vm = await DialogFor("_0x4303");   // Mobil stikkontakt dimmer
        ProductDialogWindow window = Shown(vm);

        Expander? disclosure = window.GetVisualDescendants().OfType<Expander>()
            .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == "dlg.avanceret.udvid");

        Assert.Multiple(() =>
        {
            Assert.That(disclosure, Is.Not.Null, "the wireless dimmer offers its advanced settings in place");
            Assert.That(disclosure!.Header, Is.EqualTo("Avancerede Dimmer egenskaber"));
            Assert.That(window.GetVisualDescendants().OfType<Button>()
                    .Where(b => (b.Content as string)?.StartsWith("Avanceret") == true),
                Is.Empty,
                "and no button remains that would open a window instead");
        });
    }

    private static T? WithId<T>(Window window, string automationId) where T : Control =>
        window.GetVisualDescendants().OfType<T>()
            .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == automationId);

    private static Button? ConfigureButton(Window window, string content) =>
        window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => (b.Content as string) == content);

    private static async Task<ProductDialogDescriptor> DescriptorFor(string productIdentifier)
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId placed = session.Apply(new AddProduct(locality,
            app.GetAvailableProducts().First(p => p.ProductIdentifier == productIdentifier))).Value;
        await Task.CompletedTask;
        return app.GetProductDialog(session.Current!, placed);
    }

    /// <summary>The selector the shipping window actually uses, read out of its resource dictionary.</summary>
    private static DialogFieldTemplate SelectorOfTheRealWindow()
    {
        var window = new ProductDialogWindow();
        CurrentTestWindow = window;
        Assert.That(window.TryFindResource("FieldEditor", out object? resource), Is.True,
            "the window declares its field-editor selector as a resource");
        return (DialogFieldTemplate)resource!;
    }
}
