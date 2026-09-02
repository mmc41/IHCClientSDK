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
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// The wireless dimmer's advanced settings are ORDINARY FIELDS of the product dialog, behind a disclosure that
/// expands them in place — the vendor's own shape. They were a separate modal window here: the same six values,
/// reached differently, which is a shape divergence rather than a capability one.
/// </summary>
public class AdvancedDimmerGroupTests : AvaloniaTestBase
{
    private static (ProjectDocumentSession Session, ElementId Product, ProductDialogDescriptor Descriptor) Dimmer()
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        ProductDefinition definition = app.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("LK IHC Wireless", StringComparison.Ordinal) && p.CategoryPath.Contains("Dimmer"));
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId id = session.Apply(new AddProduct(locality, definition)).Value;
        return (session, id, app.GetProductDialog(session.Current!, id));
    }

    [Test]
    public void TheSixSettingsAreDescriptorFieldsWithDlgIds()
    {
        (_, _, ProductDialogDescriptor descriptor) = Dimmer();
        DialogDescriptorGroup advanced = descriptor.Groups.Single(g => g.Caption == "Avancerede Dimmer egenskaber");

        Assert.Multiple(() =>
        {
            Assert.That(advanced.Fields.AsImmutableArray().Select(f => f.AutomationId),
                Has.All.StartWith("dlg.avanceret."),
                "addressable like every other field, not hidden behind a window");
            Assert.That(advanced.Fields, Has.Length.EqualTo(6));
            Assert.That(advanced.Collapsible, Is.True, "expanded in place, with a collapse affordance");
            // The button slot is not merely unused: T057 deleted the widget kind, the window it opened and the
            // command behind it, so there is no member left to assert the absence of.
        });
    }

    /// <summary>
    /// The ramp is stored in MILLISECONDS and captioned in seconds, so the field divides on the way out and
    /// multiplies on the way back. A scale applied at one end only is a value that drifts every time the dialog
    /// is opened and closed, which is why this asserts the ROUND TRIP rather than either half.
    /// </summary>
    [Test]
    public void TheManualRampRoundTripsBetweenSecondsAndMilliseconds()
    {
        (ProjectDocumentSession session, ElementId product, ProductDialogDescriptor descriptor) = Dimmer();
        var app = new ProjectAppService(new IhcSettings());
        DialogDescriptorField ramp = descriptor.AllFields
            .Single(f => f.AutomationId == "dlg.avanceret.manuel");

        EditOutcome outcome = session.Apply(app.Commands.ApplyProductDialog(
            session.Current!, product, [new ProductDialogEdit(ramp.Target, ramp.Attribute, "7")]));

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
            Assert.That(session.Current!.FindById(ramp.Target)!.GetAttribute("value"), Is.EqualTo("7000"),
                "7 s committed → 7000 ms stored");
            Assert.That(app.GetProductDialog(session.Current!, product).AllFields
                    .Single(f => f.AutomationId == "dlg.avanceret.manuel").Value,
                Is.EqualTo("7"),
                "and 7000 ms stored → 7 s shown, or the value changes every time the dialog is reopened");
            Assert.That(ramp.Maximum, Is.LessThan(100),
                "the BOUNDS are in seconds too — a seconds box offering millisecond bounds would refuse 7");
        });
    }

    /// <summary>The load type is a CLOSED list — its own declaration's tokens, not free text.</summary>
    [Test]
    public void TheLoadTypeOffersExactlyItsDeclaredTokens()
    {
        (_, _, ProductDialogDescriptor descriptor) = Dimmer();
        DialogDescriptorField load = descriptor.AllFields
            .Single(f => f.AutomationId == "dlg.avanceret.belastning");

        Assert.Multiple(() =>
        {
            Assert.That(load.Control, Is.EqualTo(DialogControlKind.ComboFixed));
            Assert.That(load.Suggestions.AsImmutableArray(),
                Is.EqualTo(new[] { "auto", "rc", "rl" }).AsCollection,
                "the DTD's own tokens — a second list here could disagree with the format");
        });
    }

    [Test]
    public void AnEditToASettingCommitsThroughTheProductDialogsOwnCommand()
    {
        (ProjectDocumentSession session, ElementId product, ProductDialogDescriptor descriptor) = Dimmer();
        var app = new ProjectAppService(new IhcSettings());
        DialogDescriptorField maximum = descriptor.AllFields
            .Single(f => f.AutomationId == "dlg.avanceret.maksimum");

        EditOutcome outcome = session.Apply(app.Commands.ApplyProductDialogVisit(
            session.Current!, product,
            [new ProductDialogEdit(maximum.Target, maximum.Attribute, "90")], []));

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
            Assert.That(session.Current!.FindById(maximum.Target)!.GetAttribute("value"), Is.EqualTo("90"),
                "written by the one visit command, like every other field of this dialog");
        });
    }

    /// <summary>The disclosure starts closed and opens — the vendor's Avanceret/Normal affordance.</summary>
    [AvaloniaTest]
    public async Task TheGroupIsDrawnCollapsedAndExpands()
    {
        (_, _, ProductDialogDescriptor descriptor) = Dimmer();
        var window = new ProductDialogWindow();
        window.Populate(new ProductDialogViewModel(descriptor));
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        await Task.CompletedTask;

        Expander disclosure = window.GetVisualDescendants().OfType<Expander>()
            .Single(e => Avalonia.Automation.AutomationProperties.GetAutomationId(e) == "dlg.avanceret.udvid");

        Assert.Multiple(() =>
        {
            Assert.That(disclosure.IsExpanded, Is.False, "closed on open — most visits never touch these");
            Assert.That(disclosure.Header, Is.EqualTo("Avancerede Dimmer egenskaber"));
        });

        disclosure.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();
        Assert.That(disclosure.IsExpanded, Is.True, "and it opens in place, without a window");

        window.Close();
    }
}
