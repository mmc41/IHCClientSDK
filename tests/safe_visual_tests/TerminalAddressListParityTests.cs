using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Services;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-33 (tmp/align-campaign-2026-08-10.md): the terminal address editor picks its address from two
/// LISTS, as the reference application does — not from numeric spinners.
///
/// <para>Measured 2026-08-11 on a <c>Lampeudtag</c>'s <c>Udgang</c>, driving the vendor's dialog through:</para>
/// <list type="bullet">
/// <item>the data-line list carries an explicit <c>ikke konfigureret</c> entry ahead of 1–16;</item>
/// <item>the terminal list is <b>empty and disabled</b> until a line is chosen, then holds 1–8 (an output's
/// terminals-per-line);</item>
/// <item>and the commit follows the address's COMPLETENESS, not merely its dirtiness — after choosing a line
/// alone, OK and Anvend went back to <c>enabled=false</c>, and only choosing the terminal re-armed them.</item>
/// </list>
///
/// <para>Story 03/US-012 asks for the same shape and adds the in-use marking: the terminal list marks ports
/// already taken as <c>1 (i brug)</c> … rather than listing them in a separate line.</para>
///
/// <para>The un-address route is why the <c>ikke konfigureret</c> entry matters (story line 272): an addressed
/// terminal must be returnable to unaddressed, not only movable to another port.</para>
/// </summary>
public class TerminalAddressListParityTests : AvaloniaTestBase
{
    private const string NotConfigured = "ikke konfigureret";

    private static PinPropertiesInput Input(int dataLine, int terminal, params string[] inUse) =>
        new("Udgang 'Udgang'", IsOutput: true, DataLine: dataLine, Terminal: terminal,
            CableColour: "", Note: "", InitialValueOn: false, InUseTerminals: inUse, Name: "Udgang");

    private static PinPropertiesWindow Opened(PinPropertiesInput input)
    {
        var window = new PinPropertiesWindow();
        CurrentTestWindow = window;
        window.Populate(input);
        return window;
    }

    private static ComboBox Lines(PinPropertiesWindow w) => w.FindControl<ComboBox>("DataLineList")!;
    private static ComboBox Terminals(PinPropertiesWindow w) => w.FindControl<ComboBox>("TerminalList")!;
    private static List<string> ItemsOf(ComboBox box) => box.Items.Cast<object?>().Select(i => i?.ToString() ?? "").ToList();

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void UnaddressedTerminal_ShowsNotConfigured_AndNoTerminalListYet()
    {
        PinPropertiesWindow w = Opened(Input(dataLine: 1, terminal: 0));

        Assert.Multiple(() =>
        {
            Assert.That(ItemsOf(Lines(w))[0], Is.EqualTo(NotConfigured), "the vendor leads the line list with it");
            Assert.That(Lines(w).SelectedIndex, Is.Zero, "an unaddressed terminal reads as not configured");
            Assert.That(Terminals(w).IsEnabled, Is.False, "the vendor's terminal list is dead until a line is chosen");
        });
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ChoosingALine_FillsAndEnablesTheTerminalList()
    {
        PinPropertiesWindow w = Opened(Input(dataLine: 1, terminal: 0));

        Lines(w).SelectedIndex = 1;   // data line 1

        Assert.Multiple(() =>
        {
            Assert.That(Terminals(w).IsEnabled, Is.True);
            Assert.That(ItemsOf(Terminals(w)), Has.Count.EqualTo(8), "an output line carries 8 terminals");
            Assert.That(ItemsOf(Terminals(w))[0], Is.EqualTo("1"));
        });
    }

    /// <summary>The marking is per LINE: a port taken on line 2 says nothing about line 1.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void PortsAlreadyTaken_AreMarkedInTheList_ForTheChosenLineOnly()
    {
        PinPropertiesWindow w = Opened(Input(dataLine: 1, terminal: 0, inUse: ["1.3", "2.5"]));

        Lines(w).SelectedIndex = 1;

        List<string> items = ItemsOf(Terminals(w));
        Assert.Multiple(() =>
        {
            Assert.That(items[2], Is.EqualTo("3 (i brug)"), "terminal 3 is taken on line 1");
            Assert.That(items[4], Is.EqualTo("5"), "terminal 5 is taken on line 2, not this one");
        });
    }

    /// <summary>The completeness rule measured on the vendor: a half-set address DISARMS the commit.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AHalfSetAddress_WithholdsTheCommit_AndCompletingItArms()
    {
        PinPropertiesWindow w = Opened(Input(dataLine: 1, terminal: 0));
        Button ok = w.FindControl<Button>("OkButton")!;

        Lines(w).SelectedIndex = 1;
        Assert.That(ok.IsEnabled, Is.False, "a line without a terminal is not yet an address");

        Terminals(w).SelectedIndex = 0;   // terminal 1
        Assert.That(ok.IsEnabled, Is.True, "completing the address arms the commit");
    }

    /// <summary>Returning to unaddressed is a COMPLETE address, so it must arm the commit — otherwise a
    /// terminal could never be un-addressed, which is the whole point of the not-configured entry.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ReturningToNotConfigured_ArmsTheCommit_AndYieldsNoAddress()
    {
        PinPropertiesWindow w = Opened(Input(dataLine: 2, terminal: 3));

        Lines(w).SelectedIndex = 0;

        Assert.Multiple(() =>
        {
            Assert.That(w.FindControl<Button>("OkButton")!.IsEnabled, Is.True);
            Assert.That(w.ResultForTest().Terminal, Is.Zero, "unaddressed is terminal 0, the existing convention");
        });
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AnAddressedTerminal_OpensOnItsOwnLineAndPort()
    {
        PinPropertiesWindow w = Opened(Input(dataLine: 2, terminal: 3));

        Assert.Multiple(() =>
        {
            Assert.That(Lines(w).SelectedIndex, Is.EqualTo(2), "line 2 sits at index 2, after not-configured");
            Assert.That(Terminals(w).SelectedIndex, Is.EqualTo(2), "terminal 3 is the third entry");
            Assert.That(w.ResultForTest().DataLine, Is.EqualTo(2));
            Assert.That(w.ResultForTest().Terminal, Is.EqualTo(3));
        });
    }
}
