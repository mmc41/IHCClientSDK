using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Configuration;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// W6 / F8 / D06+D08 (uxparity2 T028): IHC OpenVisual's OWN chrome is Danish — the menu-bar titles, the program-tree
/// container labels it invents, and the untitled-document name.
/// <para>
/// F8 turned out sharper than "the app mixes two languages": the app was OVERRIDING Danish that is already in the
/// project file. The `.vis` stores <c>Betingelser</c> and <c>Under program</c>; the tree showed <c>Conditions (&amp;)</c>
/// and <c>Sub-program</c>. So this is not a translation project — it is removing an English layer the app applied on
/// top of the project's own language.
/// </para>
/// <para>
/// D08 scopes it: labels that come from the CATALOG (product categories, function-block library folders) keep their
/// own names and are deliberately NOT touched — US-010 and US-063 own those.
/// </para>
/// </summary>
public class DanishChromeTests : AvaloniaTestBase
{
    [Test]
    public void UntitledDocument_IsDanish()
    {
        Assert.That(Constants.UntitledDocument, Is.EqualTo("unavngivet"),
            "a new, unsaved project is named in the application's own language");
    }

    // Registered difference (alignment F, 2026-08-11): the original leaves its save-changes MessageBox in ENGLISH
    // ("Save changes to …?" — Yes/No/Cancel), a vendor localization gap; IHC OpenVisual follows its Danish-everywhere
    // rule. Pins the exact Danish strings so they cannot drift back to the vendor's un-localized wording.
    [Test]
    public void SaveChangesGuardIsDanish()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ihc_openvisual.Services.AvaloniaDialogService.SaveChangesTitle, Is.EqualTo("Gem ændringer?"));
            Assert.That(ihc_openvisual.Services.AvaloniaDialogService.SaveChangesMessage("unavngivet"),
                Is.EqualTo("Gem ændringer i unavngivet før du fortsætter?"));
            Assert.That(ihc_openvisual.Services.AvaloniaDialogService.SaveChangesSaveLabel, Is.EqualTo("Gem"));
            Assert.That(ihc_openvisual.Services.AvaloniaDialogService.SaveChangesDiscardLabel, Is.EqualTo("Gem ikke"));
            Assert.That(ihc_openvisual.Services.AvaloniaDialogService.SaveChangesCancelLabel, Is.EqualTo("Annuller"));
        });
    }

    [AvaloniaTest]
    public async Task MenuBarTitles_AreDanish()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        var menu = window.GetVisualDescendants().OfType<Menu>().First();
        var titles = menu.Items.OfType<MenuItem>()
            .Select(m => (m.Header as string ?? string.Empty).Replace("_", string.Empty))
            .ToList();

        Assert.That(titles, Is.EqualTo(new[]
        {
            "Filer", "Rediger", "Vis", "Indsæt", "Bibliotek", "Controller", "Dokumentation", "Hjælp",
        }), "the eight menu-bar titles are Danish (Controller is the same word in both languages)");
    }

    // The four labels the app invents for program containers. The project file already says these in Danish; the
    // app must not restate them in English.
    [Test]
    public async Task ProgramTreeContainerLabels_AreDanish()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        TreeNodeViewModel commands = TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsCommandsContainer)!;
        await vm.AddSubProgramCommand.ExecuteAsync(commands);

        TreeNodeViewModel sub = TreeNodes.FindFirst(vm.FunctionNodes, n => n.Kind == TreeNodeKind.SubProgram)!;
        var childLabels = sub.Children.Select(c => c.DisplayName).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(sub.DisplayName, Is.EqualTo("Under program"),
                "the sub-program label matches what the file itself stores");
            Assert.That(childLabels.Any(l => l.StartsWith("Betingelser")), Is.True,
                $"the conditions group is 'Betingelser (…)' — got [{string.Join(" | ", childLabels)}]");
            Assert.That(childLabels, Does.Contain("Kommandoer ved betingelser sande"));
            Assert.That(childLabels, Does.Contain("Kommandoer ved betingelser falske"));
        });
    }

    // D08's carve-out: catalog-derived labels are NOT translated. A product category comes from the catalog and
    // keeps its own name, whatever language that is.
    [Test]
    public async Task CatalogDerivedLabels_AreLeftAlone()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        // The function-block library folders come straight from the catalog (US-063 keeps their names verbatim), and
        // the product categories likewise (US-010). Neither is translated by this task — the app passes the catalog's
        // own strings through untouched, whatever language they happen to be in.
        Assert.That(vm.FunctionBlocksMenu, Is.Not.Empty, "the catalog-derived menu is populated");
        var folders = vm.FunctionBlocksMenu.Select(m => m.Header).ToList();
        var catalogFolders = harness.ProjectService.GetFunctionBlockCatalogItems()
            .Select(i => (i.CategoryPath ?? string.Empty).Split('\\')[0])
            .Where(s => s.Length > 0)
            .Distinct()
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(catalogFolders, Is.Not.Empty, "the catalog really declares category folders");
            Assert.That(folders, Is.SupersetOf(catalogFolders),
                "every library folder shown is the CATALOG's own name, verbatim — D08 excludes catalog-derived "
                + "labels from W6, so W6 must not have rewritten any of them");
        });
        await Task.CompletedTask;
    }
}
