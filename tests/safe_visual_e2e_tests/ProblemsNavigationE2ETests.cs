using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

using Ihc.Tests.Shared;

namespace safe_visual_e2e_tests;

/// <summary>
/// Activating a finding and arriving at the element it is about — driven end to end, with real pointer input.
///
/// <para><b>This is the one behaviour that cannot be proved headlessly.</b> The headless tests assert that the
/// owning pane's selected property moves; only a live run proves that a POINTER GESTURE on a row produces that,
/// through the app's real hit-testing, selection and mode machinery. The gesture is deliberately not a
/// selection call: setting the selection directly would reach the outcome by a route no user can take, and the
/// route is the thing under test.</para>
///
/// <para><b>The gesture is the DOUBLE-click, never the single one.</b> A single click selects the row and must
/// leave the trees, the mode and every window exactly as they were — the panel is a list to read down, and
/// arrowing through it may not drag the installer around the project.</para>
///
/// <para><b>Targets are DISCOVERED from the oracle at run time, never hard-coded by index.</b> Row indices move
/// with every sort and filter, and a test pinned to "row 3" silently starts asserting about a different finding.
/// Each test below asks the oracle for a finding of the right SHAPE and then finds that row in the panel.</para>
/// </summary>
public class ProblemsNavigationE2ETests
{
    private const string FixtureFile = "Project6-Errors.vis";
    private const string OracleCase = "Project6-Errors";

    /// <summary>The fixture's whole-project finding: its first oracle row, and the panel's non-navigable one.</summary>
    private const string WholeProjectCode = "doc-project-info-blank";

    /// <summary>A finding on a product terminal — a configuration-tree target.</summary>
    private const string ConfigurationCode = "doc-not-linked";

    /// <summary>A terminal's missing cable colour — the deep route's own example (E1).</summary>
    private const string CableColourCode = "doc-cable-colour";

    /// <summary>A PRODUCT's own missing documentation field — the flat route (E2).</summary>
    private const string ProductFieldCode = "doc-cabletype";

    /// <summary>A block INPUT with no note — the variable editor's own route (E3).</summary>
    private const string InputNoteCode = "name-note-missing";

    /// <summary>A finding on a variable inside a function block's program — reachable only in programming view.</summary>
    private const string ProgrammingCode = "logic-variable-write-only";

    /// <summary>
    /// The fixture's bytes as the run found them. E1 opens two editors, and an editor that committed and saved
    /// would rewrite a byte-exact oracle — so the teardown proves the file came back exactly as it went in.
    /// </summary>
    /// <remarks>
    /// The fixture is driven IN PLACE, as every other E2E class drives it, and guarded by this hash rather than
    /// by a copy. A scratch copy was tried first and the app would not open from it — the launch reported ready
    /// and the title stayed <c>unavngivet</c>, reproducibly, though the identical launch works by hand. Rather
    /// than ship a harness with an unexplained failure mode, the guard asserts the property the gate actually
    /// wants: the fixture is unchanged afterwards.
    /// </remarks>
    private byte[] _fixtureBefore = [];

    [OneTimeSetUp]
    public void LaunchApp()
    {
        _fixtureBefore = System.IO.File.ReadAllBytes(E2E.Fixture(FixtureFile));
        E2E.Launch(E2E.Fixture(FixtureFile));
    }

    [OneTimeTearDown]
    public void CloseApp()
    {
        E2E.KillApp();
        Assert.That(System.IO.File.ReadAllBytes(E2E.Fixture(FixtureFile)),
            Is.EqualTo(_fixtureBefore).AsCollection,
            "a scenario modified the fixture — this feature navigates, it does not autofix, so every scenario "
            + "must cancel out of whatever it opened");
    }

    [SetUp]
    public void ReturnToConfigurationView()
    {
        if (!E2E.Run("problems", "state").Ok)
        {
            E2E.RunOk("view", "problems-toggle");
        }

        // Every test states its own starting mode rather than inheriting whatever the last one left.
        if (E2E.PaneRootLabel() != E2E.ConfigurationRootLabel)
        {
            E2E.RunOk("view", "configuration");
        }
    }

    /// <summary>
    /// Selects the fixture's finding with this code — a single click — and returns what was clicked.
    /// </summary>
    /// <remarks>
    /// BY CODE, never by index — the task's own warning, and the list makes it more than a style point: the
    /// panel virtualizes, so a row 90 places down does not exist as an element until the viewport reaches it,
    /// and an index only ever addresses the handful currently realized. The driver scrolls to find a named code;
    /// the clicked row comes back in the envelope, so the element name never has to be guessed either.
    /// </remarks>
    private static E2E.Row ClickFinding(string code)
    {
        E2E.WaitForBoundProblems();

        Assert.That(FindingOracleRows.Codes(OracleCase), Does.Contain(code),
            $"the oracle must actually record '{code}' for this fixture, or the test is asserting about nothing");

        return ClickRow(code);
    }

    /// <summary>Clicks by whatever selector the driver accepts — a code, or one row's occurrence identity.</summary>
    private static E2E.Row ClickRow(string selector) =>
        E2E.ToRow(E2E.RunOk("problems", "click", "--row", selector).Field("clicked"));

    /// <summary>
    /// ACTIVATES the fixture's finding with this code — the double-click — and returns the row it acted on.
    /// </summary>
    /// <remarks>
    /// The row is found by a single click first, which is what yields its occurrence identity; the activation
    /// then addresses that one occurrence rather than whichever row of the code the scroll meets next.
    /// </remarks>
    private static E2E.Row ActivateFinding(string code)
    {
        E2E.Row row = ClickFinding(code);
        E2E.RunOk("problems", "click", "--row", row.Occurrence, "--double");
        return row;
    }

    [Test]
    public void ActivatingAnElementAnchoredRowSelectsThatElementInTheOwningTreePane()
    {
        E2E.Row target = ActivateFinding(ConfigurationCode);
        Assert.That(target.Element, Is.Not.Empty, "precondition: the row names an element to navigate to");

        IReadOnlyList<E2E.Selection> selections = E2E.Selections();

        Assert.Multiple(() =>
        {
            Assert.That(selections, Is.Not.Empty,
                "activating an element-anchored row must leave a pane with something selected");
            Assert.That(selections.Select(s => s.Name), Has.Some.Contains(target.Element),
                $"the selected row names the finding's element ('{target.Element}'); selections were: "
                + string.Join(", ", selections.Select(s => $"{s.Tree}={s.Name}")));
            Assert.That(selections.Select(s => s.Tree), Has.Some.EqualTo("InstallationTree"),
                "and it is the OWNING pane that moved — a configuration target belongs to the installation tree");
        });
    }

    /// <summary>
    /// THE FIRST TIER, live: a single click selects the row and moves nothing else.
    ///
    /// <para>Two rows, because they fail differently. The program row's element exists only in programming view,
    /// so a selection that navigated would switch the whole application into it under the reader; the
    /// whole-project row names no element at all and would have nowhere to go even if it did.</para>
    /// </summary>
    [Test]
    public void ASingleClickSelectsTheRowAndMovesNothingElse()
    {
        // Give the panel a selection to disturb, so "unchanged" is a real observation rather than the
        // vacuous truth of nothing having been selected in the first place.
        ActivateFinding(ConfigurationCode);
        IReadOnlyList<E2E.Selection> before = E2E.Selections();
        Assert.That(before, Is.Not.Empty, "precondition: something is selected before the single clicks");
        Assert.That(E2E.PaneRootLabel(), Is.EqualTo(E2E.ConfigurationRootLabel), "precondition: configuration view");

        E2E.Row wholeProject = ClickFinding(WholeProjectCode);
        E2E.Row program = ClickFinding(ProgrammingCode);

        Assert.Multiple(() =>
        {
            Assert.That(wholeProject.Element, Is.EqualTo("utcs_project"),
                "its primary location has no parsed element, so the Element cell shows the raw locator");
            Assert.That(program.Element, Is.Not.Empty, "precondition: the program row does name an element");
            Assert.That(E2E.PaneRootLabel(), Is.EqualTo(E2E.ConfigurationRootLabel),
                "and the view did not change: the program row's element is reachable only in programming view, "
                + "which is exactly why a single click must not be what takes the installer there");
            Assert.That(E2E.Selections().Select(s => $"{s.Tree}={s.Name}"),
                Is.EqualTo(before.Select(s => $"{s.Tree}={s.Name}")),
                "no tree moved either — reading down the panel leaves the installer where they were");
            Assert.That(E2E.OpenModalIds(), Is.Empty, "and nothing opened");
        });
    }

    /// <summary>
    /// E1 — THE DEEP ROUTE, end to end. Activating a <c>doc-cable-colour</c> row lands the product selected in
    /// the tree, its dialog open, the terminal's own editor stacked ON TOP of that still-open dialog, and the
    /// caret in Ledningsfarve.
    ///
    /// <para>Run twice: once by double-click and once by Enter. Parity is a requirement rather than a nicety —
    /// a keyboard user must reach the fix context by the route a mouse user takes — and only a live run can show
    /// it, because the two gestures travel different input paths to the same entry point.</para>
    ///
    /// <para><b>What is asserted about the parent dialog, and why it is the stack rather than its selected
    /// row.</b> The driver reads the TOPMOST modal, which here is the terminal editor; reading a covered window
    /// is not a capability it has. So the parent is asserted through <c>openModals</c> — the product dialog is
    /// still there, underneath — and WHICH terminal was selected is asserted through the editor's own title,
    /// which names the terminal it was opened for.</para>
    /// </summary>
    [Test]
    [Category(E2E.DesktopOnly)]
    public void ActivatingACableColourRowLandsInTheTerminalsLedningsfarveField()
    {
        foreach (bool byKeyboard in new[] { false, true })
        {
            E2E.WaitForBoundProblems();
            Assert.That(FindingOracleRows.Codes(OracleCase), Does.Contain(CableColourCode),
                "the oracle must record this code for the fixture, or the scenario asserts about nothing");

            // Select first, then activate — the same two-tier gesture the panel offers. The keyboard run
            // activates with Enter from the selection the click produced.
            E2E.Row row = ClickRow(CableColourCode);
            if (byKeyboard)
            {
                E2E.RunOk("key", "send", "--gesture", "{ENTER}");
            }
            else
            {
                E2E.RunOk("problems", "click", "--row", row.Occurrence, "--double");
            }

            try
            {
                IReadOnlyList<string> modals = E2E.OpenModalIds();
                E2E.Envelope read = E2E.RunOk("dialog", "read");
                string gesture = byKeyboard ? "Enter" : "double-click";

                Assert.Multiple(() =>
                {
                    Assert.That(E2E.Selections().Select(s => s.Tree), Has.Some.EqualTo("InstallationTree"),
                        $"{gesture}: the tree moved to the owning product");
                    Assert.That(modals, Has.Count.EqualTo(2),
                        $"{gesture}: BOTH windows are open — the editor did not replace its parent, it stacked "
                        + $"on it. Modals were: {string.Join(", ", modals)}");
                    Assert.That(modals[0], Is.EqualTo("PinPropertiesWindow"),
                        $"{gesture}: the terminal editor is on top");
                    Assert.That(modals[1], Is.EqualTo("ProductDialogWindow"),
                        $"{gesture}: with the product dialog still beneath it");
                    Assert.That(read.Field("focused").GetProperty("id").GetString(),
                        Is.EqualTo("CableColourBox"),
                        $"{gesture}: and the caret is in Ledningsfarve — the field the finding is about");
                    Assert.That(read.Field("dialog").GetProperty("title").GetString(),
                        Does.Contain(row.Element).IgnoreCase.Or.Contain("gang"),
                        $"{gesture}: the editor was opened for the terminal the row named");
                });
            }
            finally
            {
                // Out of EVERY window the route opened, innermost first. Counted rather than fixed at two: the
                // scenario must leave nothing behind whatever it managed to open, including on the way out of a
                // failed assertion.
                E2E.CloseAllModals(4);
            }
        }
    }

    /// <summary>
    /// E2 — THE FLAT ROUTE. A finding about a field on the PRODUCT itself opens that product's dialog with the
    /// field focused and scrolled into view, and steps into nothing: the value lives on this dialog, so there is
    /// no sub-item to select.
    ///
    /// <para>It is the shallow counterpart to E1 and crosses a different seam combination — panel to composed
    /// dialog, with no stacking — which is why both exist rather than one standing for the other.</para>
    /// </summary>
    [Test]
    [Category(E2E.DesktopOnly)]
    public void ActivatingAProductFieldRowOpensTheDialogOnThatField()
    {
        E2E.WaitForBoundProblems();
        Assert.That(FindingOracleRows.Codes(OracleCase), Does.Contain(ProductFieldCode),
            "the oracle must record this code for the fixture, or the scenario asserts about nothing");

        E2E.Row row = ClickRow(ProductFieldCode);
        E2E.RunOk("problems", "click", "--row", row.Occurrence, "--double");

        try
        {
            IReadOnlyList<string> modals = E2E.OpenModalIds();
            E2E.Envelope read = E2E.RunOk("dialog", "read");
            string focused = read.Field("focused").GetProperty("id").GetString() ?? string.Empty;

            Assert.Multiple(() =>
            {
                Assert.That(modals, Is.EqualTo(new[] { "ProductDialogWindow" }).AsCollection,
                    "ONE window — a product's own field needs no sub-dialog, so nothing is stacked");
                Assert.That(focused, Does.StartWith("dlg."),
                    "the caret is in a composed field, addressed by the descriptor's own id");
                Assert.That(
                    read.Field("controls").EnumerateArray()
                        .Where(c => (c.GetProperty("id").GetString() ?? string.Empty) == focused)
                        .Select(c => c.GetProperty("visible").GetBoolean()),
                    Has.All.True,
                    "and it is ON SCREEN — a field focused below the fold is a caret the installer cannot see");
            });
        }
        finally
        {
            E2E.CloseAllModals(4);
        }
    }

    /// <summary>
    /// E6 — THE DOCUMENT MOVING UNDER THE PANEL. Deleting the element a row is about drops that row, and undoing
    /// the delete brings it back.
    ///
    /// <para><b>What this scenario is NOT.</b> The scenario this was planned as asks for a click on a row whose
    /// element is already gone — the dead-end status line. That is not drivable through this harness, and the
    /// reason is
    /// measured rather than assumed: the panel is stale-while-revalidate, the worker debounces 300 ms, and every
    /// driver command here costs a <c>pwsh</c> start of roughly half a second. The re-validation therefore
    /// always binds before a second command can arrive, and the stale row is gone by the time anything could
    /// click it. Writing that scenario anyway would have meant a retry loop around a race — a flaky test
    /// asserting a real behaviour is worse than an honest one asserting a smaller claim. The dead-end status
    /// line itself is pinned headlessly, where the validation clock can be held still, by
    /// <c>ProblemsActivationDeadEndTests</c>.</para>
    ///
    /// <para>What IS driven here is the property that makes the dead end rare: the panel follows the document.
    /// A row never outlives the element it names, and the round trip through undo proves the list is derived
    /// rather than cached.</para>
    /// </summary>
    [Test]
    [Category(E2E.DesktopOnly)]
    public void DeletingARowsElementDropsTheRowAndUndoBringsItBack()
    {
        E2E.Row target = ActivateFinding(ProductFieldCode);
        // The activation's own dialog is dismissed before anything else — the subject here is the row's
        // lifetime, not the route, and the delete below needs the tree rather than a modal.
        E2E.CloseAllModals(3);
        Assert.That(target.Element, Is.Not.Empty, "precondition: the row names the element about to be deleted");
        Assert.That(E2E.Selections().Select(s => s.Tree), Has.Some.EqualTo("InstallationTree"),
            "precondition: the activation left the product selected, which is what the delete then acts on");

        bool deleted = false;
        try
        {
            // The driver gates a destructive command behind this flag, which is the point of the gate: a
            // scenario that deletes has to say so.
            E2E.RunOk("node", "delete", "--confirm-destructive");
            deleted = true;
            // Whatever the delete raised — a confirmation — is answered before the panel is read.
            E2E.CloseAllModals(2);

            E2E.WaitForBoundProblems();
            // Asked through the driver's own row SEARCH, not through the realized-row list: the panel
            // virtualizes, so a row scrolled out of view is absent from that list while being perfectly
            // present — an assertion over it would pass whether or not the row was really dropped.
            Assert.That(E2E.Run("problems", "click", "--row", target.Occurrence).Ok, Is.False,
                "the row named an element that is gone, so the re-validated list no longer carries it — a panel "
                + "that kept it would be offering a journey to nowhere");
        }
        finally
        {
            if (deleted)
            {
                E2E.RunOk("edit", "undo");
            }
        }

        E2E.WaitForBoundProblems();
        // Asked by CODE rather than by the deleted row's occurrence. The selector addresses the FIRST matching
        // row, this code fires on several products, and the panel's order depends on whatever sort the run left
        // behind — so an identity comparison here would be measuring the sort, not the undo. What the scenario
        // claims is that the finding came back, and that is what is asserted.
        E2E.Envelope byCode = E2E.Run("problems", "click", "--row", ProductFieldCode);
        Assert.That(byCode.Ok, Is.True,
            "and undoing the delete brings the finding back — the list is derived from the document, not "
            + "cached beside it");
    }

    /// <summary>
    /// E5 — THE HOST ROUTE. The fixture's whole-project row names no element, and activating it opens the
    /// project-information window anyway.
    ///
    /// <para>Everything else here routes from an element. This row has none — <i>every masthead is blank</i> is
    /// about the project — so the destination comes from the finding's CODE, and this is the live proof that the
    /// host table is wired rather than merely written.</para>
    ///
    /// <para>It is the exact counterpart of the single-click test above, and the pair is the point: a single
    /// click on this row moves nothing at all; a double-click opens the one window that repairs it. A route that
    /// had confused the two would fail one of them.</para>
    /// </summary>
    [Test]
    public void ActivatingTheWholeProjectRowOpensTheProjectInformationWindow()
    {
        Assert.That(E2E.OpenModalIds(), Is.Empty, "precondition: nothing is open before the gesture");
        E2E.Row row = ClickFinding(WholeProjectCode);
        Assert.That(row.Element, Is.Not.Empty.And.Not.Null,
            "precondition: the row still shows its raw locator in the element cell");

        IReadOnlyList<E2E.Selection> before = E2E.Selections();
        E2E.RunOk("problems", "click", "--row", row.Occurrence, "--double");

        try
        {
            IReadOnlyList<string> modals = E2E.OpenModalIds();

            Assert.Multiple(() =>
            {
                Assert.That(modals, Has.Count.EqualTo(1),
                    $"one window, and only one. Modals were: {string.Join(", ", modals)}");
                Assert.That(modals[0], Is.EqualTo("ProjectInfoWindow"),
                    "the window the row's CODE names — there is no element to have derived it from");
                Assert.That(E2E.Selections().Select(s => $"{s.Tree}={s.Name}"),
                    Is.EqualTo(before.Select(s => $"{s.Tree}={s.Name}")),
                    "and no tree moved on the way: this route has no tree leg at all");
            });
        }
        finally
        {
            E2E.CloseAllModals(3);
        }
    }

    /// <summary>
    /// E4 — THE PROGRAM ROUTE, and the one that must open NOTHING.
    ///
    /// <para>A Logic finding lives inside a block's program, which the configuration tree does not draw at all:
    /// the mode switch is what makes the element exist to select. Activating one therefore has to enter
    /// programming mode and select the row — and then stop, because a program row is repaired by editing the
    /// program in place, not through a properties dialog.</para>
    ///
    /// <para><b>The absence is the assertion.</b> Every other scenario here proves a window opened; this one
    /// proves none did, which is the half that a route eager to open something would break silently. It is
    /// checked after the DOUBLE-click, so a second gesture on an already-selected row cannot smuggle a dialog
    /// in.</para>
    /// </summary>
    [Test]
    public void ActivatingAProgramRowEntersProgrammingModeAndOpensNoDialog()
    {
        Assert.That(E2E.PaneRootLabel(), Is.EqualTo(E2E.ConfigurationRootLabel), "precondition: configuration view");
        Assert.That(E2E.OpenModalIds(), Is.Empty, "precondition: nothing is open before the gesture");

        E2E.Row row = ClickFinding(ProgrammingCode);
        E2E.RunOk("problems", "click", "--row", row.Occurrence, "--double");

        try
        {
            IReadOnlyList<string> modals = E2E.OpenModalIds();
            IReadOnlyList<E2E.Selection> selections = E2E.Selections();

            Assert.Multiple(() =>
            {
                Assert.That(E2E.PaneRootLabel(), Is.Not.EqualTo(E2E.ConfigurationRootLabel),
                    "activation still switches the mode — the element has no configuration-tree row, so this is "
                    + "what makes it reachable at all");
                Assert.That(selections, Is.Not.Empty, "and the program row is selected");
                Assert.That(modals, Is.Empty,
                    "but NOTHING opened: a program row is repaired by editing the program in place, and a "
                    + $"dialog would be a modal to dismiss first. Modals were: {string.Join(", ", modals)}");
            });
        }
        finally
        {
            E2E.CloseAllModals(3);
        }
    }

    /// <summary>
    /// E3 — THE VARIABLE ROUTE. Activating a <c>name-note-missing</c> row lands in the block variable's own
    /// editor with the caret in its documentation note.
    ///
    /// <para>It crosses a seam combination neither E1 nor E2 does: the FUNCTIONS pane rather than the
    /// installation one, and a hand-written editor rather than the composed product dialog. The route also has
    /// to have been decided before the planner's row test — a variable HAS a tree row, so without its own class
    /// this row would land on that row and stop there, one gesture short of the field.</para>
    ///
    /// <para>Nothing is committed: the editor is cancelled, and the fixture hash checked in the teardown proves
    /// the visit wrote nothing.</para>
    /// </summary>
    [Test]
    [Category(E2E.DesktopOnly)]
    public void ActivatingAnInputNoteRowLandsInTheVariablesNoteField()
    {
        E2E.Row row = ClickFinding(InputNoteCode);
        E2E.RunOk("problems", "click", "--row", row.Occurrence, "--double");

        try
        {
            IReadOnlyList<string> modals = E2E.OpenModalIds();
            E2E.Envelope read = E2E.RunOk("dialog", "read");

            Assert.Multiple(() =>
            {
                Assert.That(modals, Has.Count.EqualTo(1),
                    "one window: a variable's values are its own dialog's fields, so there is nothing to stack. "
                    + $"Modals were: {string.Join(", ", modals)}");
                Assert.That(modals[0], Is.EqualTo("VariablePropertiesWindow"),
                    "the variable editor, not the product dialog its block sits beside");
                Assert.That(read.Field("focused").GetProperty("id").GetString(), Is.EqualTo("NoteBox"),
                    "and the caret is in the note — the field the finding is about, not the name this dialog "
                    + "opens on for every ordinary Egenskaber");
                Assert.That(E2E.Selections().Select(s => s.Tree), Has.Some.EqualTo("FunctionsTree"),
                    "reached through the FUNCTIONS pane, which is where a block variable lives");
            });
        }
        finally
        {
            E2E.CloseAllModals(3);
        }
    }

    /// <summary>
    /// Two rows of the SAME code are separately addressable. A code names a group — this fixture emits several
    /// of them many times over — so <c>--row &lt;code&gt;</c> reaches whichever the scroll meets first, which is
    /// fine for the scenarios above and useless when a particular occurrence is the subject.
    /// <para>Sorted by code first, so a run of same-code rows falls inside the realized window: the list
    /// virtualizes, and reading two rows of one code out of an unsorted viewport is a gamble.</para>
    /// </summary>
    [Test]
    public void TwoRowsOfOneCodeAreSeparatelyAddressableByTheirOccurrenceIdentity()
    {
        E2E.WaitForBoundProblems();
        E2E.RunOk("problems", "sort", "--column", "code");
        try
        {
            List<E2E.Row> pair = [.. E2E.Rows()
                .GroupBy(r => r.Code)
                .Where(g => g.Count() > 1)
                .Select(g => g.Take(2).ToList())
                .FirstOrDefault() ?? []];
            Assert.That(pair, Has.Count.EqualTo(2),
                "precondition: the realized window holds two rows of one code once the list is sorted by code");
            Assert.That(pair[0].Occurrence, Is.Not.EqualTo(pair[1].Occurrence),
                "the whole point: the two rows do not share an identity the way they share a code");

            E2E.Row first = ClickRow(pair[0].Occurrence);
            E2E.Row second = ClickRow(pair[1].Occurrence);

            Assert.Multiple(() =>
            {
                Assert.That(first.Occurrence, Is.EqualTo(pair[0].Occurrence),
                    "the driver clicked the occurrence it was given, not merely a row of that code");
                Assert.That(second.Occurrence, Is.EqualTo(pair[1].Occurrence));
                Assert.That(ClickRow(pair[0].Code).Code, Is.EqualTo(pair[0].Code),
                    "and the code selector still works — every other scenario here addresses rows that way");
            });
        }
        finally
        {
            // Back to the default sort column, so the scenarios above meet the list they were written against.
            E2E.RunOk("problems", "sort", "--column", "severity");
        }
    }
}
