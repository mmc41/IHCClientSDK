using System.Linq;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
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
        // Driven through the ONE dialog on a real wired descriptor (T030): the grids are a hand-written composite
        // hosted by the group whose descriptor declares them, so a stub descriptor would host nothing.
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId placed = session.Apply(new AddProduct(locality,
            app.GetAvailableProducts().First(p => p.ProductIdentifier == "_0x2202"))).Value;

        var window = new ProductDialogWindow();
        CurrentTestWindow = window;
        window.Populate(new ProductDialogViewModel(app.GetProductDialog(session.Current!, placed), [Terminal]));
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        ListBoxItem row = window.GetVisualDescendants().OfType<ListBoxItem>().First();
        AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(row);

        Assert.That(peer.GetName(), Is.EqualTo(Terminal.Summary));
    }

    /// <summary>
    /// A cell too narrow for its text ELLIPSIZES and offers the whole text as a tooltip, rather than
    /// hard-clipping mid-word.
    /// <para>The vendor's dialog is far wider than this one, so long terminal names and notes do not fit
    /// here — "Tilstedeværelses indikering" rendered as "Tilstedeværelses indik" with nothing to say it
    /// had been cut (seen on products 009, 019 and 028 before this was fixed; T062). Guessing a wider
    /// column would be inventing pixel parity the rubric puts out of scope; making the truncation visible
    /// and the text recoverable is not.</para>
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ARowsCells_EllipsizeAndKeepTheirFullTextReachable()
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId placed = session.Apply(new AddProduct(locality,
            app.GetAvailableProducts().First(p => p.ProductIdentifier == "_0x2202"))).Value;

        var window = new ProductDialogWindow();
        CurrentTestWindow = window;
        window.Populate(new ProductDialogViewModel(app.GetProductDialog(session.Current!, placed), [Terminal]));
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        TextBlock nameCell = window.GetVisualDescendants().OfType<TextBlock>()
            .First(t => (t.Text ?? string.Empty) == Terminal.Name);

        Assert.Multiple(() =>
        {
            Assert.That(nameCell.TextTrimming, Is.EqualTo(TextTrimming.CharacterEllipsis),
                "a cell that does not fit shows an ellipsis, not a word cut in half");
            Assert.That(ToolTip.GetTip(nameCell), Is.EqualTo(Terminal.Name),
                "and the whole text stays reachable");
        });
    }
}
