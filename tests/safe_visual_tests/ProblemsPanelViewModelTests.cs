using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The panel's view-model: what it binds, and — the harder half — WHEN it decides the document moved.
///
/// <para><b>Why it owns a generation counter at all.</b> The workflow publishes a monotone
/// <c>Version</c> and a <c>StateChanged</c> event, and neither one distinguishes "the user edited the project"
/// from "the user opened a different file". <c>Open</c> BUMPS the version rather than resetting it, and
/// <c>MarkSaved</c> raises the event with the version unchanged — so a loop keyed on version alone would either
/// re-validate on every save (wasted CPU on a document that did not change) or, far worse, stale-while-revalidate
/// the PREVIOUS file's rows into a freshly opened project. The three branches below are that derivation, and each
/// is its own test because each is a distinct way to get it wrong.</para>
///
/// <para><b>Determinism.</b> Everything here runs on a FakeTimeProvider with the marshal delegate invoking
/// inline, so a debounce, a run and a staleness threshold are all advanced explicitly rather than waited for.</para>
/// </summary>
public class ProblemsPanelViewModelTests
{
    private static readonly TimeSpan Debounce = ValidationWorker.DefaultDebounce;
    private static readonly TimeSpan StaleDelay = ProblemsPanelViewModel.StaleIndicatorDelay;

    /// <summary>Drives a real <see cref="ProjectWorkflow"/> with a validate delegate the test controls.</summary>
    /// <summary>
    /// The shared panel rig plus the two things only these tests need: a RECORD of every snapshot the validation
    /// was handed (which is how "did a run happen at all" is asserted), and a body the test swaps between runs.
    /// </summary>
    private sealed class Rig : IDisposable
    {
        private readonly ProblemsRig _inner;

        public List<Project> Validated { get; } = [];

        public Func<Project, EquatableArray<ValidationFinding>>? Body { get; set; }

        public Rig() => _inner = new ProblemsRig(project =>
        {
            Validated.Add(project);
            return Body?.Invoke(project) ?? EquatableArray<ValidationFinding>.Empty;
        });

        public ShellHarness Harness => _inner.Harness;

        public FakeTimeProvider Clock => _inner.Clock;

        public ProblemsPanelViewModel Panel => _inner.Panel;

        public ValidationMonitor Validation => _inner.Validation;

        /// <summary>Opens the fresh project the shell starts every session with.</summary>
        public Task OpenAsync() => Harness.Session.NewAsync();

        /// <summary>Lets the quiet period elapse and waits for whatever run it starts.</summary>
        public Task SettleAsync() => _inner.SettleAsync();

        public void Dispose() => _inner.Dispose();
    }

    private static ValidationFinding Finding(
        string code, ValidationSeverity severity, string message,
        ValidationCategory category = ValidationCategory.Documentation,
        FindingLocation? primary = null) =>
        new(new Problem(new ProblemCode(code), message, EquatableArray<ProblemArgument>.Empty),
            severity, category, primary, EquatableArray<FindingLocation>.Empty);

    // ── The four-state model ────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task BeforeTheFirstResultThePanelSaysItIsValidatingAndNeverThatItIsClean()
    {
        using Rig rig = new();
        await rig.OpenAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Validating));
            Assert.That(rig.Panel.StateText, Is.EqualTo("Validerer projektet…"),
                "an unvalidated project must not read as problem-free");
            Assert.That(rig.Panel.StateText, Is.Not.EqualTo("Ingen problemer fundet"));
        });
    }

    [Test]
    public async Task ACleanResultReachesTheCleanStateAndItsEmptyStateText()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        await rig.SettleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Clean));
            Assert.That(rig.Panel.StateText, Is.EqualTo("Ingen problemer fundet"));
            Assert.That(rig.Panel.Rows, Is.Empty);
        });
    }

    [Test]
    public async Task AResultWithFindingsReachesTheFindingsState()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        rig.Body = _ => System.Collections.Immutable.ImmutableArray.Create(
            Finding("doc-name-empty", ValidationSeverity.Warning, "Navnet mangler."));
        await rig.SettleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Findings));
            Assert.That(rig.Panel.Rows, Has.Count.EqualTo(1));
        });
    }

    // ── D16's three branches ────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task AnEditRunsAgainInTheSameGeneration()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        await rig.SettleAsync();
        int generation = rig.Validation.Generation;
        int runs = rig.Validated.Count;

        await rig.Harness.Session.ApplyAsync(new AddLocality("Ny stue"));
        await rig.SettleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Validation.Generation, Is.EqualTo(generation), "an edit is the SAME document");
            Assert.That(rig.Validated, Has.Count.EqualTo(runs + 1), "and it re-validates");
        });
    }

    [Test]
    public async Task ASaveKeepsTheGenerationAndRunsNothingBecauseTheDocumentDidNotChange()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        await rig.SettleAsync();
        int generation = rig.Validation.Generation;
        int runs = rig.Validated.Count;

        // MarkSaved raises StateChanged with LastChange null AND the version unchanged — the one shape that
        // means "nothing about the document moved". Validating again would burn a whole-project run for rows
        // that cannot have changed.
        rig.Harness.Dialogs.SavePath = rig.Harness.TempPath("saved.vis");
        await rig.Harness.Session.SaveAsAsync();
        await rig.SettleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Validation.Generation, Is.EqualTo(generation), "a save is not a new document");
            Assert.That(rig.Validated, Has.Count.EqualTo(runs), "and it triggers no run");
            Assert.That(rig.Panel.State, Is.Not.EqualTo(ProblemsState.Stale), "nor does it leave the panel stale");
        });
    }

    [Test]
    public async Task AReplacementStartsANewGenerationClearsTheOldRowsAndValidatesTheNewProjectOnce()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        rig.Body = _ => System.Collections.Immutable.ImmutableArray.Create(
            Finding("doc-name-empty", ValidationSeverity.Warning, "Navnet mangler."));
        await rig.SettleAsync();
        Assert.That(rig.Panel.Rows, Is.Not.Empty, "precondition: the old document produced rows");

        int generation = rig.Validation.Generation;
        int runs = rig.Validated.Count;

        // Ny: SetProject → document.Open → StateChanged with no change set and a MOVED version.
        await rig.Harness.Session.NewAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Validation.Generation, Is.GreaterThan(generation), "a different document, not an edit");
            Assert.That(rig.Panel.Rows, Is.Empty,
                "the previous file's rows are cleared IMMEDIATELY — showing them over a new project is the "
                + "stale-rows defect this whole derivation exists to prevent");
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Validating),
                "and the panel says it is validating, not that the new project is clean");
        });

        await rig.SettleAsync();
        Assert.That(rig.Validated, Has.Count.EqualTo(runs + 1), "the replacement validates exactly once");
    }

    // ── Rows ────────────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ARowCarriesTheProblemsOwnCodeAndItsDanishMessageVerbatim()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        ElementId locality = rig.Harness.Session.Current!.Groups[0].Id!.Value;
        rig.Body = _ => System.Collections.Immutable.ImmutableArray.Create(
            Finding("doc-name-empty", ValidationSeverity.Warning, "Navnet på produktet mangler.",
                ValidationCategory.Documentation, new FindingLocation("_0x1", locality, null)));
        await rig.SettleAsync();

        ProblemRowViewModel row = rig.Panel.Rows.OfType<ProblemRowViewModel>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(row.Code, Is.EqualTo("doc-name-empty"), "the finding's own code, read off the problem");
            Assert.That(row.Message, Is.EqualTo("Navnet på produktet mangler."),
                "rendered WHOLE — the panel never re-derives or re-words a bound message");
            Assert.That(row.Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(row.Category, Is.EqualTo(ValidationCategory.Documentation));
            Assert.That(row.Element, Is.EqualTo(locality), "the navigation anchor is Primary.Element");
            Assert.That(row.NavigationKind, Is.EqualTo(NavigationKind.Tree));
            Assert.That(row.ElementName, Is.Not.Empty, "resolved against the snapshot the run used");
        });
    }

    /// <summary>
    /// The row shape a whole-project finding produces, and the reason navigability is keyed on
    /// <c>Primary?.Element</c> rather than on <c>Primary</c>. Both shapes exist in the engine: a capacity rule
    /// reports no location at all, while <c>doc-project-info-blank</c> reports the ROOT — which yields a non-null
    /// location whose Element is null, because the root carries no id attribute. Keying on Primary alone would
    /// call the second one navigable and then have nothing to navigate to.
    /// <para>Since T046 the two differ in one further way, and it is the point of the pair: neither has an
    /// ELEMENT, but the project-info row has a host WINDOW keyed on its code, while the capacity row has no
    /// destination at all.</para>
    /// </summary>
    [Test]
    public async Task AWholeProjectFindingIsListedNonNavigableAndFallsBackToItsRawLocator()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        rig.Body = _ => System.Collections.Immutable.ImmutableArray.Create(
            Finding("doc-project-info-blank", ValidationSeverity.Warning, "Projektoplysninger mangler.",
                ValidationCategory.Documentation, new FindingLocation("utcs_project", null, null)),
            Finding("prj-capacity-exceeded", ValidationSeverity.Warning, "For mange moduler.",
                ValidationCategory.ProjectStructure, null));
        await rig.SettleAsync();

        ProblemRowViewModel root = rig.Panel.Rows.OfType<ProblemRowViewModel>()
            .Single(r => r.Code == "doc-project-info-blank");
        ProblemRowViewModel nowhere = rig.Panel.Rows.OfType<ProblemRowViewModel>()
            .Single(r => r.Code == "prj-capacity-exceeded");

        Assert.Multiple(() =>
        {
            Assert.That(root.Element, Is.Null, "its Primary is NON-null but its Element is null");
            Assert.That(root.NavigationKind, Is.EqualTo(NavigationKind.Dialog),
                "and with no element it still has a destination — the host window its CODE names (T046)");
            Assert.That(root.NavigationHint, Does.Not.Contain("træet"),
                "whose hint promises no tree leg, because there is no element to reveal");
            Assert.That(root.ElementName, Is.EqualTo("utcs_project"),
                "so the element cell falls back to the raw locator rather than showing a blank cell");
            Assert.That(nowhere.NavigationKind, Is.EqualTo(NavigationKind.None),
                "and a finding with no location at all is listed too");
            Assert.That(nowhere.Element, Is.Null);
        });
    }

    /// <summary>
    /// One count per tier, all four of them. The two that ship empty — Fatal and Info — are the reason this is
    /// stated rather than assumed: no shipped rule emits either from the corpus, so a constructed row is the only
    /// way to ask whether the counter exists at all.
    /// <para>
    /// The Fatal row also proves the counts and the CLASSIFIER agree. Fatal and Error are one severity, so a
    /// counter reading the severity instead of <c>TierOf</c> would put this row under Errors and read 2/0 —
    /// which is exactly what a name promising all four tiers would then be hiding.
    /// </para>
    /// </summary>
    [Test]
    public async Task CountsAreKeptPerTierIncludingTheTwoThatShipEmpty()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        rig.Body = _ => System.Collections.Immutable.ImmutableArray.Create(
            ProblemsTestData.FatalFinding("z", "Filen kan ikke gemmes."),
            Finding("a", ValidationSeverity.Error, "Fejl."),
            Finding("b", ValidationSeverity.Warning, "Advarsel 1."),
            Finding("c", ValidationSeverity.Warning, "Advarsel 2."),
            Finding("d", ValidationSeverity.Info, "Oplysning."));
        await rig.SettleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Fatals.Count, Is.EqualTo(1), "the refusing Error, counted apart from the rest");
            Assert.That(rig.Panel.Errors.Count, Is.EqualTo(1),
                "and NOT 2 — the ordinary tier excludes the refusing row rather than containing it");
            Assert.That(rig.Panel.Warnings.Count, Is.EqualTo(2));
            Assert.That(rig.Panel.Infos.Count, Is.EqualTo(1));
            Assert.That(rig.Validation.HasBlockingFindings, Is.True, "what the send gate reads");
        });
    }

    [Test]
    public async Task ACleanBoundResultReportsNoErrorsSoTheSendGateReopens()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        rig.Body = _ => System.Collections.Immutable.ImmutableArray.Create(
            Finding("a", ValidationSeverity.Error, "Fejl."));
        await rig.SettleAsync();
        Assert.That(rig.Validation.HasBlockingFindings, Is.True, "precondition");

        rig.Body = _ => EquatableArray<ValidationFinding>.Empty;
        await rig.Harness.Session.ApplyAsync(new AddLocality("Ny stue"));
        await rig.SettleAsync();

        Assert.That(rig.Validation.HasBlockingFindings, Is.False, "fixing the last error reopens the gate once the next run binds");
    }

    // ── Staleness ───────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task RowsStayVisibleWhileStaleSoTheListIsNeverBlanked()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        rig.Body = _ => System.Collections.Immutable.ImmutableArray.Create(
            Finding("doc-name-empty", ValidationSeverity.Warning, "Navnet mangler."));
        await rig.SettleAsync();

        await rig.Harness.Session.ApplyAsync(new AddLocality("Ny stue"));

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Stale));
            Assert.That(rig.Panel.Rows, Has.Count.EqualTo(1), "stale-while-revalidate: the list is never blanked");
        });
    }

    [Test]
    public async Task TheStaleIndicatorDoesNotEngageForASubSecondEditValidateCycle()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        await rig.SettleAsync();

        await rig.Harness.Session.ApplyAsync(new AddLocality("Ny stue"));
        Assert.That(rig.Panel.IsStaleIndicatorEngaged, Is.False, "not immediately");

        rig.Clock.Advance(StaleDelay - TimeSpan.FromMilliseconds(1));
        Assert.That(rig.Panel.IsStaleIndicatorEngaged, Is.False, "and not a millisecond before the threshold");

        await rig.SettleAsync();
        Assert.That(rig.Panel.IsStaleIndicatorEngaged, Is.False,
            "a fast cycle shows no indicator at all — which is the whole point of having a threshold");
    }

    [Test]
    public async Task TheStaleIndicatorEngagesOnceStalenessPersistsPastTheThreshold()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        await rig.SettleAsync();

        // The run is HELD open first. The threshold (1 s) is longer than the debounce (300 ms), so advancing far
        // enough to engage the indicator also starts a run — and a run that completes clears the very state under
        // test. Without the hold this assertion is a race the test merely tends to win.
        using System.Threading.ManualResetEventSlim hold = new(false);
        rig.Body = _ =>
        {
            hold.Wait(TimeSpan.FromSeconds(10));
            return EquatableArray<ValidationFinding>.Empty;
        };

        await rig.Harness.Session.ApplyAsync(new AddLocality("Ny stue"));
        rig.Clock.Advance(StaleDelay);

        Assert.That(rig.Panel.IsStaleIndicatorEngaged, Is.True, "past the threshold the indicator is on");

        hold.Set();
        await rig.SettleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.IsStaleIndicatorEngaged, Is.False, "and it clears when the fresh result binds");
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Clean));
        });
    }

    [Test]
    public async Task DisposalDetachesThePanelFromTheMonitorSoNothingBindsIntoItAfterwards()
    {
        Rig rig = new();
        try
        {
            rig.Body = _ => System.Collections.Immutable.ImmutableArray.Create(
                ProblemsTestData.Finding(ValidationSeverity.Warning, "doc-name-empty", "Navnet mangler."));
            await rig.OpenAsync();
            rig.Panel.Dispose();
            await rig.SettleAsync();

            Assert.Multiple(() =>
            {
                // The run still happens — it belongs to the SESSION now, and the send gate depends on it whether
                // or not this panel exists. What disposal buys is that its result reaches no disposed view-model.
                Assert.That(rig.Validated, Is.Not.Empty, "the session keeps validating; only the panel let go");
                Assert.That(rig.Validation.HasBlockingFindings, Is.False, "a Warning does not block");
                Assert.That(rig.Panel.Rows, Is.Empty, "nothing bound into the disposed panel");
            });
            Assert.DoesNotThrow(rig.Panel.Dispose, "disposal is idempotent");
        }
        finally
        {
            rig.Dispose();
        }
    }

    [Test]
    public async Task DisposingTheMonitorStopsTheRunsThemselves()
    {
        Rig rig = new();
        try
        {
            await rig.OpenAsync();
            await rig.SettleAsync();
            rig.Validated.Clear();

            rig.Validation.Dispose();
            await rig.Harness.Session.AddLocalityAsync();
            rig.Clock.Advance(Debounce);

            Assert.That(rig.Validated, Is.Empty, "a disposed monitor starts nothing, however the document moves");
        }
        finally
        {
            rig.Dispose();
        }
    }
    /// <summary>
    /// Each row keeps the very <see cref="ValidationFinding"/> it was projected from, established by REFERENCE.
    ///
    /// <para><b>Why identity and not an index.</b> Comparing <c>Rows[i]</c> against <c>outcome.Findings[i]</c>
    /// would pass on a row that had merely been rebuilt from equal data, and would keep passing if the panel
    /// ever filtered or re-sorted between the two lists — which it does. Only reference identity says "this row
    /// is about THAT finding", which is the property an export needs: the file is built from each visible row's
    /// finding, so a row holding a look-alike would export a look-alike.</para>
    ///
    /// <para>The projection is exercised through <c>ToRow</c> directly, so the finding handed in is a value this
    /// test holds and can compare against, rather than one recovered from the panel's own output.</para>
    /// </summary>
    [Test]
    public void ARowKeepsTheVeryFindingItWasProjectedFrom()
    {
        ValidationFinding finding = Finding(
            "doc-name-empty", ValidationSeverity.Warning, "Navnet mangler.",
            ValidationCategory.Documentation, new FindingLocation("utcs_project", null, null));

        ProblemRowViewModel row = ProblemsPanelViewModel.ToRow(finding, null, [], ProblemsTestData.UnusedPlanner);

        Assert.Multiple(() =>
        {
            Assert.That(row.Finding, Is.SameAs(finding), "the instance, not an equal one");
            Assert.That(row.Severity, Is.EqualTo(finding.Severity));
            Assert.That(row.Code, Is.EqualTo(finding.Code.Value));
            Assert.That(row.Message, Is.EqualTo(finding.Problem.Message));
            Assert.That(row.Category, Is.EqualTo(finding.Category));
        });
    }

    /// <summary>
    /// The retained finding survives the collision branch, which is the one that DROPS the row's anchor. A row
    /// that leads nowhere is still about a real finding, and an export of it must carry that finding's own
    /// locator and path — the very things the panel deliberately stopped showing.
    /// </summary>
    [Test]
    public void ARowWhoseAnchorWasDroppedStillKeepsItsFinding()
    {
        ElementId shared = ElementId.ParseOrNull("_0x2132")!.Value;
        ValidationFinding finding = Finding(
            "id-duplicate-token", ValidationSeverity.Error, "Dobbelt id.",
            ValidationCategory.FileIntegrity,
            new FindingLocation("_0x2132", shared, null, "/utcs_project/groups/group[1]"));

        ProblemRowViewModel row = ProblemsPanelViewModel.ToRow(
            finding, null, new Dictionary<ElementId, ProjectElement?> { [shared] = null },
            ProblemsTestData.UnusedPlanner);

        Assert.Multiple(() =>
        {
            Assert.That(row.Finding, Is.SameAs(finding));
            Assert.That(
                row.Finding.Primary!.Xpath, Is.EqualTo("/utcs_project/groups/group[1]"),
                "the exact node the panel could not choose between is still on the finding");
        });
    }

    /// <summary>
    /// Every row the panel binds carries a finding — asserted over a real projected list rather than over a
    /// hand-called <c>ToRow</c>, so the binding path cannot lose it.
    /// </summary>
    [Test]
    public async Task EveryBoundRowCarriesItsFinding()
    {
        using Rig rig = new();
        await rig.OpenAsync();
        rig.Body = _ => System.Collections.Immutable.ImmutableArray.Create(
            Finding("doc-name-empty", ValidationSeverity.Warning, "Navnet mangler.",
                ValidationCategory.Documentation, new FindingLocation("utcs_project", null, null)),
            Finding("prj-capacity-exceeded", ValidationSeverity.Warning, "For mange moduler.",
                ValidationCategory.ProjectStructure, null));
        await rig.SettleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Rows, Has.Count.EqualTo(2), "non-vacuity");
            Assert.That(rig.Panel.Rows.OfType<ProblemRowViewModel>().Select(r => r.Finding), Is.All.Not.Null);
            Assert.That(
                rig.Panel.Rows.OfType<ProblemRowViewModel>().Select(r => r.Finding.Code.Value),
                Is.EqualTo(new[] { "doc-name-empty", "prj-capacity-exceeded" }));
        });
    }

    // ── The bulk copy of the internal rows (D05's counterpart to the export exclusion) ───────────────────────

    private static Ihc.Vis.Problems.InternalError Fault(string code, string sentence = "Uventet fejl.") =>
        ProblemsTestData.Fault(code, sentence, "boom", detail: $"at {code}()");

    /// <summary>Returns the rig too: it owns a validation timer that outlives the test, and the suite's own
    /// leak detector fails any test that drops one.</summary>
    private static (ProblemsShellRig Rig, ProblemsPanelViewModel Panel) PanelWith(
        params Ihc.Vis.Problems.InternalError[] faults)
    {
        ProblemsShellRig rig = new();
        InternalErrorLog log = new();
        foreach (Ihc.Vis.Problems.InternalError fault in faults)
        {
            log.Append(fault);
        }
        return (rig, new ProblemsPanelViewModel(rig.Harness.Session, rig.Harness.Session.Validation,
            internalErrors: log) { AppVersion = "1.2.3" });
    }

    /// <summary>
    /// The gate's assertion: EVERY listed internal error is in the copied text. The control exists for the fault
    /// storm, so copying some of them would be the failure, not a partial success.
    /// </summary>
    [Test]
    public void TheBulkCopyCarriesEveryListedInternalError()
    {
        var (rig, panel) = PanelWith(
            Fault("internal.rule-failed", "Reglen fejlede."),
            Fault("app.openvisual.unexpected", "Uventet fejl under 'Start'."),
            Fault("internal.edit-failed", "Redigeringen kunne ikke gennemføres."));
        using var _ = rig;
        using var _p = panel;

        string payload = panel.BuildInternalsPayload();

        Assert.Multiple(() =>
        {
            Assert.That(panel.Rows.OfType<InternalErrorRowViewModel>().Count(), Is.EqualTo(3),
                "non-vacuity: three rows are listed");
            foreach (string code in new[]
                     { "internal.rule-failed", "app.openvisual.unexpected", "internal.edit-failed" })
            {
                Assert.That(payload, Does.Contain(code), code);
            }
            Assert.That(payload, Does.Contain("Reglen fejlede."), "the Danish sentences travel too");
            Assert.That(payload, Does.Contain("at internal.edit-failed()"), "and the captured detail");
            Assert.That(payload, Does.Contain("1.2.3"), "and the build they were observed in");
        });
    }

    /// <summary>
    /// LISTED, not held: a tier switched off is not in the copy, because what the reader asked for is what the
    /// reader can see. The count on the chip still says how many there are — switching a tier off must never
    /// look like the faults were fixed — but the copy follows the list.
    /// </summary>
    [Test]
    public void TheBulkCopyFollowsTheTierToggle()
    {
        var (rig, panel) = PanelWith(Fault("internal.rule-failed"));
        using var _ = rig;
        using var _p = panel;

        panel.Internals.IsShown = false;

        Assert.Multiple(() =>
        {
            Assert.That(panel.BuildInternalsPayload(), Is.Empty);
            Assert.That(panel.Internals.Count, Is.EqualTo(1), "the count is of the whole result");
        });
    }

    /// <summary>The control appears with its subject and not before: the Internal tier is empty in every healthy
    /// session, and a permanent button would invite the reader to wonder what it would have copied.</summary>
    [Test]
    public void TheBulkCopyIsOfferedOnlyWhenThereAreInternalRows()
    {
        var (emptyRig, none) = PanelWith();
        using var _e = emptyRig;
        using var _n = none;
        var (rig, some) = PanelWith(Fault("internal.rule-failed"));
        using var _ = rig;
        using var _s = some;

        Assert.Multiple(() =>
        {
            Assert.That(none.CanCopyInternals, Is.False);
            Assert.That(some.CanCopyInternals, Is.True);
        });
    }

    /// <summary>The same in-place feedback the details dialog gives, and the same coded refusal.</summary>
    [Test]
    public void TheBulkCopyReportsInPlace()
    {
        var (rig, panel) = PanelWith(Fault("internal.rule-failed"));
        using var _ = rig;
        using var _p = panel;
        Assert.That(panel.CopyInternalsText, Is.EqualTo("Kopiér interne fejl"));

        panel.MarkInternalsCopied();
        Assert.That(panel.CopyInternalsText, Is.EqualTo("Kopieret"));

        panel.MarkInternalsCopyUnavailable();
        Assert.That(panel.CopyInternalsText,
            Is.EqualTo(HostProblems.ClipboardUnavailable().Message),
            "asserted against the catalogue, so the control and the catalogue cannot drift");
    }

    /// <summary>
    /// A confirmation left standing after the list moved would claim the reader has a copy of rows that were not
    /// in it. The label resets whenever the sink changes.
    /// </summary>
    [Test]
    public void TheConfirmationResetsWhenTheListMoves()
    {
        ProblemsShellRig rig = new();
        using var _ = rig;
        InternalErrorLog log = new();
        log.Append(Fault("internal.rule-failed"));
        using ProblemsPanelViewModel panel =
            new(rig.Harness.Session, rig.Harness.Session.Validation, internalErrors: log);
        panel.MarkInternalsCopied();

        log.Append(Fault("app.openvisual.unexpected"));

        Assert.That(panel.CopyInternalsText, Is.EqualTo("Kopiér interne fejl"));
    }
}
