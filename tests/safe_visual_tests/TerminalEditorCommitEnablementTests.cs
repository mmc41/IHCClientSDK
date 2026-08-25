using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-32: the terminal address editor's <b>OK</b> and <b>Anvend</b>
/// stay disabled until something changes.
///
/// <para>Measured 2026-08-11 on a <c>Lampeudtag</c>'s <c>Udgang</c>, both states of the same dialog: on open the
/// reference application reports <c>OK enabled=false</c> and <c>Anvend enabled=false</c> (only <c>Annuller</c> is
/// live); after selecting a <c>Ledningsfarve</c> both read <c>enabled=true</c>. OpenVisual had all three enabled
/// from the moment the editor opened.</para>
///
/// <para>Story 03/US-012 says the same thing and gives the reason: "OK and Apply stay <b>disabled until something
/// changes</b> — so an editor opened to read an address cannot accidentally rewrite it." This dialog is opened to
/// LOOK things up at least as often as to change them, and a stray Return on an unchanged editor should not write
/// an address back.</para>
///
/// <para>Cancel stays live throughout, so a dialog opened by mistake is never a trap.</para>
/// </summary>
public class TerminalEditorCommitEnablementTests : AvaloniaTestBase
{
    private static PinPropertiesWindow Opened()
    {
        var window = new PinPropertiesWindow();
        CurrentTestWindow = window;
        // Populated like the real editor: the address lists have no contents until they are filled, and a rule
        // asserted against an empty dialog is not the rule the installer meets.
        window.Populate(new ihc_openvisual.Services.PinPropertiesInput(
            "Udgang 'Udgang'", IsOutput: true, DataLine: 1, Terminal: 0,
            CableColour: "", Note: "", InitialValueOn: false, InUseTerminals: []));
        return window;
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void OnOpen_CommitButtonsAreWithheld_AndCancelIsNot()
    {
        PinPropertiesWindow w = Opened();

        Assert.Multiple(() =>
        {
            Assert.That(w.FindControl<Button>("OkButton")!.IsEnabled, Is.False,
                "an editor opened to read an address must not commit one");
            Assert.That(w.FindControl<Button>("ApplyButton")!.IsEnabled, Is.False, "…nor Anvend");
            Assert.That(w.FindControl<Button>("CancelButton")!.IsEnabled, Is.True,
                "Annuller stays live, so a dialog opened by mistake is never a trap");
        });
    }

    /// <summary>Each editable field must arm the commit, not just the first one someone wired up — asserted per
    /// field, since a dirty flag hung off one control looks identical to a working one until another is used.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    [TestCase("CableColourBox")]
    [TestCase("NoteBox")]
    public void ChangingAField_ArmsTheCommit(string field)
    {
        PinPropertiesWindow w = Opened();

        w.FindControl<TextBox>(field)!.Text = "changed";

        Assert.Multiple(() =>
        {
            Assert.That(w.FindControl<Button>("OkButton")!.IsEnabled, Is.True, $"{field} is an editable field");
            Assert.That(w.FindControl<Button>("ApplyButton")!.IsEnabled, Is.True, $"{field} arms Anvend too");
        });
    }

    /// <summary>The address arms the commit like any other field — but only once it is COMPLETE, which is why
    /// both lists are set here. The half-set case is <c>TerminalAddressListParityTests</c>'s.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ChangingTheAddress_ArmsTheCommit()
    {
        PinPropertiesWindow w = Opened();

        w.FindControl<ComboBox>("DataLineList")!.SelectedIndex = 1;
        w.FindControl<ComboBox>("TerminalList")!.SelectedIndex = 2;

        Assert.That(w.FindControl<Button>("OkButton")!.IsEnabled, Is.True,
            "the address is the whole point of this editor, so changing it must arm the commit");
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ChangingThePowerFailFlag_ArmsTheCommit()
    {
        PinPropertiesWindow w = Opened();

        w.FindControl<CheckBox>("SaveValueCheck")!.IsChecked = true;

        Assert.That(w.FindControl<Button>("OkButton")!.IsEnabled, Is.True,
            "the power-fail flag is committed by this dialog, so it must arm the commit like any other field");
    }
}
