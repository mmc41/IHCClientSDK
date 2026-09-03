using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;

namespace Ihc.Vis.Tests;

/// <summary>
/// Inserting an empty function block must respond the way IHC Visual does (uxparity S-18). Two observations,
/// both measured against the vendor on `Project1-SimpelWired.vis`:
///
/// <list type="bullet">
/// <item>The block's placeholder name is <c>Tom blok</c>. It is written into the `.vis` as the block's
/// <c>name</c>, so it is project data like the default room names and the <c>Lokalitet</c> placeholder —
/// not UI text.</item>
/// <item>The insert takes the window straight into programming mode for the NEW block: in the vendor both
/// panes re-root from <c>Lokaliteter</c> to the block (left = its Input/Output/Indstillinger/Interne
/// variable sections, right = its Programmer/Program subtree). Authoring a blank block is the whole point
/// of creating one, so the vendor opens it rather than leaving the installer to find and F3 it.</item>
/// </list>
/// </summary>
public class InsertEmptyFunctionBlockParityTests
{
    [Test]
    public async Task InsertEmptyFunctionBlock_CarriesTheVendorPlaceholderName()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectNode(vm.InstallationNodes[0].Children[7]);   // Garage

        await vm.InsertEmptyFunctionBlockCommand.ExecuteAsync(null);
        vm.LeaveProgrammingModeCommand.Execute(null);   // the insert opens the block; read it back in its locality

        // The one place the placeholder's WORDING is pinned: everywhere else the tests address the block through
        // ProjectWorkflow.EmptyBlockName, so this assertion is what would fail if the vendor name were changed.
        Assert.That(vm.FunctionNodes[0].Children[7].Children[0].DisplayName, Is.EqualTo("Tom blok"),
            "the placeholder name is project data, so it matches the file format's own");
    }

    /// <summary>
    /// The program subtree's container captions are the containers' STORED names, not English words invented by
    /// the projector — the S-33 rule, which configuration mode already follows. A blank block's `programs`,
    /// `events` and `actions` elements all carry a name in the file (`Programmer`, `Hændelser`, `Kommandoer`),
    /// and the vendor renders exactly those.
    /// </summary>
    [Test]
    public async Task InsertEmptyFunctionBlock_ProgramCaptions_ComeFromTheStoredNames()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectNode(vm.InstallationNodes[0].Children[7]);   // Garage

        await vm.InsertEmptyFunctionBlockCommand.ExecuteAsync(null);

        var programs = vm.FunctionNodes[0].Children[0];
        var program = programs.Children[0];
        Assert.Multiple(() =>
        {
            Assert.That(programs.DisplayName, Is.EqualTo("Programmer"));
            Assert.That(program.Children.Select(c => c.DisplayName),
                Is.EqualTo(new[] { "Hændelser", "Kommandoer" }));
        });
    }

    /// <summary>
    /// The mode entry must target the block that was just inserted, not whatever happened to be selected —
    /// with an existing block in the same locality, picking the wrong one is invisible to a name check.
    /// </summary>
    [Test]
    public async Task InsertEmptyFunctionBlock_ProgramsTheNewBlock_NotAnExistingSibling()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[7].ElementId!.Value);
        harness.Dialogs.PropertiesResult = new PropertiesResult("Første", string.Empty);
        await vm.PropertiesCommand.ExecuteAsync(vm.FunctionNodes[0].Children[7].Children[0]);
        vm.SelectNode(vm.InstallationNodes[0].Children[7]);   // Garage

        await vm.InsertEmptyFunctionBlockCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsProgrammingMode, Is.True, "the insert enters programming mode");
            Assert.That(vm.FunctionNodes[0].DisplayName, Is.EqualTo(ProjectWorkflow.EmptyBlockName),
                "and it opens the block just created, not its already-named sibling");
        });
    }
}
