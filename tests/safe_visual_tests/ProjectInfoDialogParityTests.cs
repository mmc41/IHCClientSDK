using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Ihc.Vis;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// The project-information dialog must offer the same field inventory as IHC Visual's (uxparity S-32). Measured
/// against the vendor's <c>Rediger projekt oplysninger</c> on `Project1-SimpelWired.vis`: it has three groups —
///
/// <list type="bullet">
/// <item><c>Projekt oplysninger</c>: Projektnummer, Projekt type, Programmør, Tegning, Beskrivelse — FIVE fields.</item>
/// <item><c>Installatør information</c> and <c>Kunde oplysninger</c>: Navn, Vej, Telefon, Postnummer,
/// Mobil telefon, By, Email, Land — eight each.</item>
/// </list>
///
/// OpenVisual offered 3 + 8 + 8, missing <c>Projekt type</c> and <c>Tegning</c>. Both are declared in the format
/// (<c>project_info</c>'s DTD lists <c>type</c> and <c>drawing</c>), so this was surfacing data the file already
/// carries — see <c>MetadataCommandTests</c> for the engine half of the same gap.
/// </summary>
public class ProjectInfoDialogParityTests
{
    // The logical tree, not the visual one: an unshown window has applied no templates, so a ScrollViewer's content
    // is not yet in its visual tree — enumerating that way would find zero boxes and the assertion would be vacuous.
    private static string[] EditableFieldNames(Window window) =>
        window.GetLogicalDescendants().OfType<TextBox>().Select(b => b.Name!).ToArray();

    [AvaloniaTest]
    public void Dialog_OffersEveryProjectLevelFieldTheVendorOffers()
    {
        var window = new ProjectInfoWindow();

        Assert.That(EditableFieldNames(window).Where(n => n.StartsWith("Proj")), Is.EquivalentTo(new[]
        {
            "ProjNumberBox", "ProjTypeBox", "ProjProgrammerBox", "ProjDrawingBox", "ProjDescriptionBox",
        }));
    }

    /// <summary>The two new fields must be wired both ways, not merely present: shown from the loaded project and
    /// returned on OK. A dialog that displays a field it then discards is the failure mode worth pinning.</summary>
    [AvaloniaTest]
    public void TypeAndDrawing_RoundTripThroughTheDialog()
    {
        var window = new ProjectInfoWindow();
        var loaded = new ProjectInfoData("d", "n", "p", "Villa", "Tegning 4b", ContactInfo.Empty, ContactInfo.Empty);

        window.Populate(loaded);
        ProjectInfoData shown = window.Collect();
        window.ProjTypeBox.Text = "Erhverv";
        window.ProjDrawingBox.Text = "T-12";
        ProjectInfoData edited = window.Collect();

        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.EqualTo(loaded), "what was loaded is what is shown");
            Assert.That(edited.Type, Is.EqualTo("Erhverv"));
            Assert.That(edited.Drawing, Is.EqualTo("T-12"));
        });
    }

    /// <summary>End to end through the shell: edit the two fields in the dialog, then save, and assert the values
    /// reached the saved <c>.vis</c>. This is the parity scenario's own assertion — the vendor's dialog writes
    /// <c>type</c>/<c>drawing</c> into <c>project_info</c>, so OpenVisual's must too.</summary>
    [AvaloniaTest]
    public async Task EditingTypeAndDrawing_ReachesTheSavedFile()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.OpenAsync(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Project1-SimpelWired.vis"));
        harness.Dialogs.ProjectInfoResponder = current => current with { Type = "Villa", Drawing = "Tegning 4b" };
        string target = harness.TempPath("s32.vis");
        harness.Dialogs.SavePath = target;

        await vm.ProjectInfoCommand.ExecuteAsync(null);
        await harness.Session.SaveAsAsync();

        string saved = await File.ReadAllTextAsync(target, System.Text.Encoding.Latin1);
        Assert.Multiple(() =>
        {
            Assert.That(saved, Does.Contain("type=\"Villa\""));
            Assert.That(saved, Does.Contain("drawing=\"Tegning 4b\""));
        });
    }
}
