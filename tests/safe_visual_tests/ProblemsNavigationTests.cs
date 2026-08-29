using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// One-click navigation from a finding to the element it is about.
///
/// <para><b>Every assertion here is PANE-SPECIFIC, and that is the lesson the mechanism was built on.</b> The
/// shell has two tree panes, each with its own selected-node property, and <c>SelectNode</c> — the obvious-looking
/// method — updates neither of them: it moves the view-model's idea of the selection without moving anything on
/// screen. A navigation test that asserts on the aggregate therefore passes while the user sees nothing happen.
/// So these tests read <c>SelectedInstallationNode</c> and <c>SelectedFunctionsNode</c> directly.</para>
///
/// <para><b>Mode-awareness is the second half.</b> A finding inside a block's program has no row in the
/// configuration tree at all, and a finding about a locality has none in the programming tree — so navigating
/// means switching mode first, in whichever direction the target needs.</para>
/// </summary>
public class ProblemsNavigationTests
{
    private static ValidationFinding About(ElementId? element, string locator = "utcs_project") =>
        new(new Problem(new ProblemCode("doc-name-empty"), "Navnet mangler.", EquatableArray<ProblemArgument>.Empty),
            ValidationSeverity.Warning, ValidationCategory.Documentation,
            new FindingLocation(locator, element, null), EquatableArray<FindingLocation>.Empty);

    /// <summary>Places one row for the given element and returns it, without depending on a real rule firing.</summary>
    private static ProblemRowViewModel RowFor(ProblemsShellRig rig, ElementId? element, string locator = "utcs_project")
    {
        ProblemRowViewModel row = new(
            About(element, locator), element, element is null ? locator : "navn",
            element is null ? NavigationKind.None : NavigationKind.Tree,
            $"doc-name-empty@{locator}");
        rig.Panel.Rows.Add(row);
        return row;
    }

    // ── Configuration-tree targets ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ClickingARowSelectsItsElementInTheOwningPaneNotJustInTheViewModel()
    {
        using ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();
        ElementId locality = rig.Shell.InstallationNodes[0].Children[0].ElementId!.Value;

        rig.Panel.SelectedRow = RowFor(rig, locality);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Shell.SelectedInstallationNode?.ElementId, Is.EqualTo(locality),
                "the INSTALLATION pane's own selected property — the one the TreeView binds. Setting the "
                + "view-model's aggregate instead moves nothing on screen");
            Assert.That(rig.Shell.SelectedFunctionsNode?.ElementId, Is.Not.EqualTo(locality),
                "and the other pane is left alone");
        });
    }

    [Test]
    public async Task NavigationExpandsTheAncestorsSoTheRowIsActuallyRealized()
    {
        using ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();
        TreeNodeViewModel root = rig.Shell.InstallationNodes[0];
        TreeNodeViewModel locality = root.Children[0];
        root.IsExpanded = false;

        rig.Panel.SelectedRow = RowFor(rig, locality.ElementId!.Value);

        Assert.That(root.IsExpanded, Is.True,
            "a selection on a collapsed branch sticks to nothing — the ancestors have to be opened first");
    }

    // ── Non-navigable rows ──────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ARowWithNoElementChangesNothingWhenClicked()
    {
        using ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();
        ElementId locality = rig.Shell.InstallationNodes[0].Children[0].ElementId!.Value;
        rig.Panel.SelectedRow = RowFor(rig, locality);
        TreeNodeViewModel? before = rig.Shell.SelectedInstallationNode;

        rig.Panel.SelectedRow = RowFor(rig, null);

        Assert.That(rig.Shell.SelectedInstallationNode, Is.SameAs(before),
            "a whole-project finding names no single site, so there is nowhere to go and the selection stays put");
    }

    [Test]
    public async Task ANonNavigableRowSaysSoRatherThanLookingLikeADeadClick()
    {
        using ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();
        ProblemRowViewModel navigable = RowFor(rig, rig.Shell.InstallationNodes[0].Children[0].ElementId!.Value);
        ProblemRowViewModel not = RowFor(rig, null);

        Assert.Multiple(() =>
        {
            Assert.That(navigable.NavigationKind, Is.EqualTo(NavigationKind.Tree));
            Assert.That(not.NavigationKind, Is.EqualTo(NavigationKind.None));
            Assert.That(not.ElementEmphasis, Is.LessThan(navigable.ElementEmphasis),
                "the element cell is visibly de-emphasised, so the row does not invite a click that does nothing");
            Assert.That(not.NavigationHint, Is.Not.EqualTo(navigable.NavigationHint));
            Assert.That(not.NavigationHint, Is.Not.Empty, "and it says why in Danish");
        });
    }

    // ── Ambiguous ids ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>Project6-Errors</c> with the scene <c>Alt slukket</c> re-stamped with <c>Tom scene</c>'s id, so two
    /// <c>resource_scene</c> pins carry <c>_0x1f44a</c> and all three of the fixture's scenes are still called
    /// from no program — three <c>scene-unreferenced</c> findings, two of them anchored at the same token.
    /// </summary>
    /// <remarks>
    /// Patched as BYTES over a copy in the harness' temp directory. The two tokens are the same length, so
    /// nothing after the replacement shifts and the fixture's Latin-1 bytes and CRLFs reach the loader exactly as
    /// committed — a re-encode through a string would rewrite the whole file to make one attribute collide.
    /// </remarks>
    private static string ProjectWithTwoScenesSharingAnId(ShellHarness harness)
    {
        byte[] bytes = File.ReadAllBytes(ProblemsTestData.FixturePath("Project6-Errors.vis"));
        int at = bytes.AsSpan().IndexOf("_0x1f54a"u8);
        Assert.That(at, Is.GreaterThanOrEqualTo(0), "the fixture still carries the id token this test re-stamps");
        "_0x1f44a"u8.CopyTo(bytes.AsSpan(at));

        string path = harness.TempPath("two-scenes-one-id.vis");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    /// The panel's own projection over that file, run directly rather than through the shell.
    /// </summary>
    /// <remarks>
    /// Directly because the shell cannot be driven here: opening normalizes, normalizing builds a
    /// <c>ProjectEditor</c>, and the editor refuses a tree whose ids collide — so <c>Åbn</c> reports the file as
    /// unopenable and no finding about it ever binds
    /// (<see cref="ADuplicateIdProjectNeverReachesThePanelBecauseTheOpenPathRefusesIt"/> pins that). The
    /// projection is still where the defect lives and still the panel's own code, and <c>Load</c> without
    /// <c>NormalizeOnOpen</c> is the same door the engine's own duplicate-id corpus goes through.
    /// </remarks>
    private static async Task<ProblemRowViewModel[]> ProjectedRowsAsync(ShellHarness harness)
    {
        Project snapshot = await harness.ProjectService.Load(ProjectWithTwoScenesSharingAnId(harness));
        Dictionary<ElementId, ProjectElement?> byId = ProblemsPanelViewModel.IndexById(snapshot);
        return [.. harness.ProjectService.ValidateStructured(snapshot)
            .Select(f => ProblemsPanelViewModel.ToRow(f, snapshot, byId, ProblemsTestData.Planner(harness.ProjectService)))];
    }

    /// <summary>The unreferenced-scene row for one scene, found by the name its own message carries.</summary>
    private static ProblemRowViewModel UnreferencedSceneRow(ProblemRowViewModel[] rows, string name) =>
        rows.Single(r => r.Code == "scene-unreferenced" && r.Message.Contains($"'{name}'"));

    /// <summary>
    /// Two elements carrying one id — the state the engine has an <c>id-duplicate-token</c> rule for. The panel
    /// resolves a row's element through an index built in one walk of the tree, and an index cannot hold two
    /// elements under one key: the second is either recorded as a collision or silently dropped, and dropping it
    /// makes EVERY row anchored at that token wear the first holder's name and navigate there — the rows about
    /// the second holder included, with nothing on screen saying which of the two was meant.
    /// </summary>
    [Test]
    public async Task RowsAnchoredAtASharedIdLeadNowhereRatherThanAllLandingOnTheFirstHolder()
    {
        using ShellHarness harness = ShellHarness.Create();
        ProblemRowViewModel[] rows = await ProjectedRowsAsync(harness);
        Assert.That(rows.Select(r => r.Code), Does.Contain("id-duplicate-token"),
            "precondition: the re-stamped file really does collide, or nothing below proves anything");

        ProblemRowViewModel restamped = UnreferencedSceneRow(rows, "Alt slukket");
        ProblemRowViewModel original = UnreferencedSceneRow(rows, "Tom scene");

        Assert.Multiple(() =>
        {
            Assert.That(restamped.ElementName, Is.EqualTo("_0x1f44a"),
                "the row about 'Alt slukket' must not present itself as being about 'Tom scene'. The two share a "
                + "token, so the honest cell is the token the engine recorded — a name resolved through the index "
                + "would be whichever holder the tree walk reached first");
            Assert.That(original.ElementName, Is.EqualTo("_0x1f44a"),
                "and the first holder gets the same treatment: the ambiguity belongs to the TOKEN, so demoting "
                + "only the later holder would still show one of the two a name it cannot earn");
            Assert.That(restamped.NavigationKind, Is.EqualTo(NavigationKind.None),
                "a shared token names no single site, so there is nowhere to go — and a click that silently "
                + "landed on one of the two is the defect this test exists for");
            Assert.That(original.NavigationKind, Is.EqualTo(NavigationKind.None));
            Assert.That(rows.Single(r => r.Code == "id-duplicate-token").NavigationKind,
                Is.EqualTo(NavigationKind.None),
                "including the collision's own row: the engine anchors it at the first holder, which is the one "
                + "element it can name, but the id it reports still resolves to two");
        });
    }

    [Test]
    public async Task AnUnambiguousRowInTheSameProjectStillResolvesItsNameAndItsAnchor()
    {
        using ShellHarness harness = ShellHarness.Create();
        ProblemRowViewModel[] rows = await ProjectedRowsAsync(harness);

        ProblemRowViewModel untouched = UnreferencedSceneRow(rows, "Modstrid");

        Assert.Multiple(() =>
        {
            Assert.That(untouched.ElementName, Is.EqualTo("Modstrid"),
                "one collision in the file demotes that token and nothing else — every other element keeps its "
                + "name");
            Assert.That(untouched.NavigationKind, Is.EqualTo(NavigationKind.Tree));
        });
    }

    /// <summary>
    /// Why the two tests above project directly instead of driving the panel: OpenVisual will not open a project
    /// whose ids collide at all. <c>ProjectEditor</c> refuses it — id-addressed editing would resolve first-match
    /// and silently edit the wrong element — and every open normalizes through an editor.
    /// <para>
    /// That refusal is CODED as of the edit-open identity work: it arrives as <c>id-duplicate-token</c> under
    /// <c>edit.open</c> with its Danish sentence, where it used to arrive as a bare English
    /// <c>InvalidOperationException</c> that the host could only report as its own generic
    /// <c>app.openvisual.project-open-failed</c>. The dialog assertion below pins the better message; a host code
    /// reappearing there would mean the SDK stopped naming its cause.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Kept beside them so the pair cannot drift apart: if the open path ever admits such a file (US-080 does
    /// list a duplicate id among the findings the panel should SHOW, which it cannot while this holds), this test
    /// goes red and points at the projection tests that are ready for it.
    /// </remarks>
    [Test]
    public async Task ADuplicateIdProjectNeverReachesThePanelBecauseTheOpenPathRefusesIt()
    {
        using ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();

        bool opened = await rig.Harness.Session.OpenAsync(ProjectWithTwoScenesSharingAnId(rig.Harness));
        await rig.SettleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(opened, Is.False);
            Assert.That(rig.Harness.Dialogs.LastMessage, Does.Contain("id-duplicate-token"),
                "and it says so with the SDK's OWN cause. The host used to mint app.openvisual.project-open-failed "
                + "here, because the guard threw a bare InvalidOperationException and there was no code to pass "
                + "on; now that the guard carries an identity the host surfaces that refusal whole, and a "
                + "host-minted code would be hiding a more specific one");
            Assert.That(rig.Harness.Dialogs.LastMessage, Does.Contain("Dobbelt id"),
                "in Danish, naming the offending id — which is the point of giving the guard an identity: the "
                + "installer reads which id collided, not an English engine sentence");
            Assert.That(rig.Panel.Rows.Select(r => r.Code), Does.Not.Contain("id-duplicate-token"),
                "so no duplicate-id finding is on screen — the panel is showing the starter project it fell "
                + "back to, not the file that was asked for");
        });
    }

    // ── Mode-awareness ──────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ARowInsideABlocksProgramEntersProgrammingModeFirst()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel shell = await harness.EnterProgrammingModeOnNewBlockAsync();
        Assert.That(shell.IsProgrammingMode, Is.True, "precondition");

        // An INTERNAL VARIABLE of the block. The block's variable SECTIONS are visible in the Functions tree even
        // in configuration mode, so a section id would be reachable without a mode change and would prove nothing;
        // an internal variable is program-only, which is what makes this test about mode-awareness.
        ElementId section = shell.InstallationNodes[0].Children[3].ElementId!.Value;   // Internal variables
        await harness.Session.AddVariableAsync(section, "resource_integer", "Tæller");
        ElementId inProgram = TreeNodes.FindPin(shell.InstallationNodes, "Tæller")!.ElementId!.Value;

        shell.LeaveProgrammingModeCommand.Execute(null);
        Assert.That(shell.IsProgrammingMode, Is.False, "precondition: back in configuration");

        bool reached = shell.RevealAndSelect(inProgram);

        Assert.Multiple(() =>
        {
            Assert.That(reached, Is.True);
            Assert.That(shell.IsProgrammingMode, Is.True,
                "the target has no row in the configuration tree at all, so the mode has to change before it exists");
            Assert.That(shell.SelectedInstallationNode?.ElementId, Is.EqualTo(inProgram));
        });
    }

    [Test]
    public async Task AConfigurationRowLeavesProgrammingModeBeforeSelecting()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel shell = await harness.EnterProgrammingModeOnNewBlockAsync();
        Assert.That(shell.IsProgrammingMode, Is.True, "precondition");

        ElementId locality = harness.Session.Current!.Groups[0].Id!.Value;

        bool reached = shell.RevealAndSelect(locality);

        Assert.Multiple(() =>
        {
            Assert.That(reached, Is.True);
            Assert.That(shell.IsProgrammingMode, Is.False, "a locality is a configuration-tree target");
            Assert.That(shell.SelectedInstallationNode?.ElementId, Is.EqualTo(locality));
        });
    }

    [Test]
    public async Task AnUnreachableIdIsReportedRatherThanSilentlyDoingNothing()
    {
        using ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();

        Assert.That(rig.Shell.RevealAndSelect(new ElementId(0x7FFFFFF, 0x11)), Is.False,
            "an id in no tree and no block returns false, so a caller can tell 'nowhere to go' from 'went there'");
    }
}
