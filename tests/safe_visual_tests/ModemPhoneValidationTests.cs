using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
/// US-013: the modem's telephone numbers are validated against the SDK's one rule
/// (<see cref="DialogValueRule.PhoneNumber"/>) and a refusal is stated when they break it.
/// <para>The rule itself — 3–20 characters, no spaces, leading country code, and its boundaries — is pinned in
/// the SDK by <c>DialogValueRuleTests</c>. These tests pin only what the DIALOG does with it: that it consults
/// the shared rule at all, that a refusal is stated rather than silent, that it names the offending slot, and
/// that the dialog stays open so the number can be fixed. Duplicating the boundary cases here would create a
/// second definition of "valid" that could drift from the first.</para>
/// <para>Since T029 the modem has no dialog of its own: this drives the ONE generic
/// <see cref="ProductDialogWindow"/> on the modem's composed descriptor. The behaviour is therefore no longer
/// the modem's — every family gets it, for every field the composer gives a rule.</para>
/// </summary>
public class ModemPhoneValidationTests : AvaloniaTestBase
{
    private const string SmsModem = "_0x3103";

    private sealed record Opened(
        ProductDialogWindow Window,
        ProductDialogViewModel ViewModel,
        IReadOnlyList<ProductDialogFieldViewModel> PhoneSlots,
        TextBlock Error);

    /// <summary>Opens the real generic dialog on the modem's real composed descriptor — no stub: a stub could
    /// carry a rule the composer never attaches, and "the dialog validates" would be true of nothing shipping.</summary>
    private static Opened Open()
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        ProductDefinition modem = app.GetAvailableProducts().First(p => p.ProductIdentifier == SmsModem);
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId placed = session.Apply(new AddProduct(locality, modem)).Value;

        var viewModel = new ProductDialogViewModel(app.GetProductDialog(session.Current!, placed));
        var window = new ProductDialogWindow();
        window.Populate(viewModel);
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var slots = viewModel.AllFields.Where(f => f.Rule is not null).ToList();
        Assert.That(slots, Has.Count.EqualTo(30), "precondition: the composer gave all 30 telephone slots a rule");

        return new Opened(window, viewModel, slots,
            window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "DialogError"));
    }

    private static void ClickOk(ProductDialogWindow window) =>
        window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "OkButton")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void NothingIsSaidBeforeAnythingIsWrong()
    {
        Opened opened = Open();
        Assert.That(opened.Error.IsVisible, Is.False);
    }

    /// <summary>All 30 slots blank is the state the dialog opens in; it must be committable.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AllSlotsEmpty_Commits()
    {
        Opened opened = Open();

        ClickOk(opened.Window);

        Assert.Multiple(() =>
        {
            Assert.That(opened.ViewModel.HasRefusal, Is.False, "an unfilled slot is not an invalid one");
            Assert.That(opened.Error.IsVisible, Is.False);
        });
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AnInvalidNumber_IsRefusedOutLoud_AndTheDialogStaysOpen()
    {
        Opened opened = Open();
        opened.PhoneSlots[0].Value = "12";   // two characters: below the rule's minimum

        ClickOk(opened.Window);

        Assert.Multiple(() =>
        {
            Assert.That(opened.Error.IsVisible, Is.True, "the refusal is stated");
            Assert.That(opened.Error.Text, Does.Contain(DialogValueRule.PhoneNumber.Refusal),
                "and it is the SDK's sentence, not a second copy written out in the GUI");
            Assert.That(opened.Window.IsVisible, Is.True, "the dialog stays open so the number can be fixed");
        });
    }

    /// <summary>
    /// The refusal names WHICH slot. With 30 fields on screen, "the number is invalid" leaves the installer
    /// hunting; the vendor's own message names the slot too.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheRefusalNamesTheOffendingSlot()
    {
        Opened opened = Open();
        opened.PhoneSlots[16].Value = "+45 70 10";   // slot 17, spaces

        ClickOk(opened.Window);

        Assert.That(opened.Error.Text, Does.StartWith("Nummer 17"));
    }

    /// <summary>Editing the offending field retracts the message — it described a state that no longer holds.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void EditingTheFieldRetractsTheRefusal()
    {
        Opened opened = Open();
        opened.PhoneSlots[0].Value = "12";
        ClickOk(opened.Window);
        Assert.That(opened.Error.IsVisible, Is.True, "precondition: the refusal is up");

        opened.PhoneSlots[0].Value = "+4570100001";

        Assert.That(opened.Error.IsVisible, Is.False);
    }

    /// <summary>A valid number commits — the guard refuses the invalid, not the unfamiliar.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AValidNumber_Commits()
    {
        Opened opened = Open();
        opened.PhoneSlots[0].Value = "+4570100001";
        opened.PhoneSlots[29].Value = "+45";   // the shortest the rule accepts, in the last slot

        ClickOk(opened.Window);

        Assert.Multiple(() =>
        {
            Assert.That(opened.ViewModel.HasRefusal, Is.False);
            Assert.That(opened.ViewModel.PendingEdits, Has.Length.EqualTo(2), "and both numbers are committed");
        });
    }
}
