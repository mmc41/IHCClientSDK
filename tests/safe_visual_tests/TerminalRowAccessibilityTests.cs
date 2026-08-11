using System.Linq;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using ihc_openvisual.Services;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-35 — the product dialog's terminal rows announced their C# record.
///
/// <para>Measured live 2026-08-11 on a <c>Lampeudtag</c>: the row's accessible name was, verbatim,</para>
/// <code>
///   ProductTerminal { Name = Udgang, Address = , CableColour = , Note = , IsOutput = True, PinId = _0x525b }
/// </code>
/// <para>A screen reader reads that out in full — brace syntax, the <c>IsOutput</c> flag and the internal
/// <c>PinId</c> included — because the row's four columns are loose <see cref="TextBlock"/>s under a header
/// grid, so with no name of its own the item falls back to <c>ToString()</c>.</para>
///
/// <para>The dialog's scene table already solved this: <c>SceneContainerRow.Summary</c> reads its columns as one
/// sentence WITH their captions spelled in, because Avalonia's Windows bridge exposes no table pattern and a
/// client otherwise meets unassociated text runs it cannot map to headers. The terminal rows get the same
/// treatment — this is the app's own established answer, applied to the surface that missed it.</para>
/// </summary>
public class TerminalRowAccessibilityTests : AvaloniaTestBase
{
    private static readonly ProductTerminal Terminal =
        new("Udgang", "Datalinie 1.02", "Brun", "Ved døren", IsOutput: true, PinId: "_0x525b");

    /// <summary>The summary names each column, so the row is readable without the header grid.</summary>
    [Test]
    public void ATerminalRow_ReadsAsASentence_NamingItsColumns()
    {
        string summary = Terminal.Summary;

        Assert.Multiple(() =>
        {
            Assert.That(summary, Does.Contain("Udgang").And.Contain("Datalinie 1.02"));
            Assert.That(summary, Does.Contain("Brun").And.Contain("Ved døren"));
            Assert.That(summary, Does.Contain("Navn").And.Contain("Adresse")
                .And.Contain("Ledningsfarve").And.Contain("Note"),
                "the captions travel with the values — the header grid is not associated with the row");
        });
    }

    /// <summary>And it carries none of the record's plumbing: no brace syntax, no IsOutput flag, and above all
    /// no PinId, which is an internal element token that means nothing to an installer.</summary>
    [Test]
    public void ATerminalRow_AnnouncesNoInternals()
    {
        string summary = Terminal.Summary;

        Assert.Multiple(() =>
        {
            Assert.That(summary, Does.Not.Contain("_0x525b"), "the element id is internal");
            Assert.That(summary, Does.Not.Contain("IsOutput"));
            Assert.That(summary, Does.Not.Contain("ProductTerminal {"), "…and it is not the record's ToString()");
        });
    }

    /// <summary>The rendered ITEM must carry it. Asserted on the realized container's automation name rather than
    /// on the property alone: a Summary that nothing binds is exactly the state this finding describes, and the
    /// row would go on announcing its ToString().</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheRealizedRow_AnnouncesTheSummary()
    {
        var window = new ProductPropertiesWindow();
        CurrentTestWindow = window;
        window.Populate(new ProductPropertiesInput(
            "Lampeudtag", "Lampeudtag", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
            CurrentLocalityId: string.Empty, Terminals: [Terminal]));
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        ListBoxItem row = window.GetVisualDescendants().OfType<ListBoxItem>().First();
        AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(row);

        Assert.That(peer.GetName(), Is.EqualTo(Terminal.Summary));
    }
}
