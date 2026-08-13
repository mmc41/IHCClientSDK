using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace safe_visual_tests;

/// <summary>
/// Opening a project must leave the document in the state IHC Visual leaves it in (uxparity S-03), so that
/// saving it straight back produces the same bytes the vendor would. Opening is not passive there: the
/// built-in catalog enum definitions are re-hoisted to the bottom of <c>enum_definitions</c> with freshly
/// allocated ids, every time the file is opened. The SDK's own <c>Load</c> stays byte-faithful — that is a
/// library contract other callers depend on — so the editor asks for the normalization explicitly.
/// </summary>
public class OpenLoadNormalizationParityTests
{
    private static string SampleProject() =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Project1-SimpelWired.vis");

    private static ProjectElement[] EnumDefinitions(Project project) =>
        project.Child("enum_definitions")!.Children.Where(c => c.Tag == "enum_definition").ToArray();

    private static bool IsCatalogEnum(ProjectElement definition) =>
        definition.GetAttribute("typeid") is { } typeid && typeid != ElementId.NullToken;

    [Test]
    public async Task Open_ReHoistsTheCatalogEnums_ToTheBottomWithFreshIds()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();

        Project asStored = await harness.ProjectService.Load(SampleProject());
        Assert.That(ElementId.TryParse(asStored.LastUniqueId, out ElementId storedLast), Is.True);
        int storedCounter = storedLast.Counter;
        ProjectElement[] storedDefinitions = EnumDefinitions(asStored);
        Assert.That(storedDefinitions.Count(IsCatalogEnum), Is.EqualTo(2),
            "the sample project contains exactly the two built-in catalog enum definitions");
        Assert.That(storedDefinitions.Take(2).All(IsCatalogEnum), Is.True,
            "precondition: on disk the two catalog enums come FIRST");

        await harness.Session.OpenAsync(SampleProject());
        ProjectElement[] opened = EnumDefinitions(harness.Session.Current!);

        Assert.Multiple(() =>
        {
            Assert.That(opened.Count(IsCatalogEnum), Is.EqualTo(2),
                "normalization must preserve both catalog enum definitions");
            Assert.That(opened.TakeLast(2).All(IsCatalogEnum), Is.True,
                "after opening they are the LAST two definitions");
            Assert.That(opened.Take(opened.Length - 2).Any(IsCatalogEnum), Is.False,
                "and no catalog enum is left above them");
            Assert.That(opened.TakeLast(2).All(d => d.Id!.Value.Counter > storedCounter), Is.True,
                "each carries an id minted after the counter the file arrived with");
            Assert.That(harness.Session.IsDirty, Is.False,
                "the normalization is part of opening, not an edit the user has to be warned about");
        });
    }

    [Test]
    public async Task Open_ThenSaveUnchanged_WritesTheNormalizedLayout()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        string copy = harness.TempPath("roundtrip.vis");
        File.Copy(SampleProject(), copy);

        await harness.Session.OpenAsync(copy);
        await harness.Session.SaveAsync();

        Project reloaded = await harness.ProjectService.Load(copy);
        ProjectElement[] reloadedDefinitions = EnumDefinitions(reloaded);
        Assert.That(reloadedDefinitions.Count(IsCatalogEnum), Is.EqualTo(2),
            "save must persist both catalog enum definitions");
        Assert.That(reloadedDefinitions.TakeLast(2).All(IsCatalogEnum), Is.True,
            "the re-hoisted layout is what reaches the file");
    }
}
