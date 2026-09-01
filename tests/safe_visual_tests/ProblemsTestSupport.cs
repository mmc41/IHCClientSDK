using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>The data and paths the Problemer tests build from: a constructed finding, and where things live.</summary>
internal static class ProblemsTestData
{
    /// <summary>One constructed finding — the only way to exercise a tier no shipped rule emits.</summary>
    public static ValidationFinding Finding(
        ValidationSeverity severity,
        string code,
        string message,
        ValidationCategory category = ValidationCategory.Documentation,
        string locator = "Stue") =>
        new(new Problem(new ProblemCode(code), message, EquatableArray<ProblemArgument>.Empty),
            severity, category, new FindingLocation(locator, null, null),
            EquatableArray<FindingLocation>.Empty);

    /// <summary>
    /// An Error finding whose row also REFUSES an operation — what the panel lists under <c>Fatale fejl</c>.
    /// <para>
    /// Built here rather than taken from a project, for the reason the Info tier's findings are: the tier is a
    /// property of the finding, so a constructed one exercises it exactly as a produced one would, and the
    /// alternative is a fixture that has to keep containing a refusing row for the test to mean anything.
    /// </para>
    /// </summary>
    public static ValidationFinding FatalFinding(
        string code,
        string message,
        ValidationCategory category = ValidationCategory.FileIntegrity,
        string locator = "Stue") =>
        Finding(ValidationSeverity.Error, code, message, category, locator) with
        {
            RefusedOperations = ImmutableArray.Create(OperationCodes.Save, OperationCodes.EditOpen),
        };

    /// <summary>
    /// One constructed fault — the sibling of <see cref="Finding"/> for the panel's other row kind.
    /// </summary>
    /// <remarks>
    /// Here rather than as a private factory per fixture, for the reason <see cref="Finding"/> is: the argument
    /// order of a six-field record is a thing each copy has to get right independently, and
    /// <see cref="InternalError.Message"/> next to <see cref="InternalError.Diagnostic"/> is a Danish sentence
    /// next to an English one — swapped, every assertion still passes and the row shows the wrong language.
    /// </remarks>
    public static InternalError Fault(
        string code = "internal.rule-failed",
        string? message = null,
        string diagnostic = "Rule threw",
        InternalErrorOrigin origin = InternalErrorOrigin.Sdk,
        string detail = "at Rule()") =>
        new(new ProblemCode(code), message ?? RuleFailedMessage, diagnostic, origin, detail,
            DateTimeOffset.UnixEpoch);

    /// <summary>
    /// The catalogue's sentence for a crashed validation rule, in ONE place. Retyped per fixture it was the
    /// template's text with as many test-side copies as there were fixtures asserting on it.
    /// </summary>
    public const string RuleFailedMessage =
        "Valideringsreglen 'name-empty' fejlede. Listen kan mangle fejl.";

    /// <summary>
    /// The fault a named rule's crash produces, for a fixture that needs the rule name in the sentence rather
    /// than only the default one <see cref="RuleFailedMessage"/> spells. Here for the reason its neighbours are:
    /// retyped per fixture, the Danish template gained a test-side copy per fixture asserting on it.
    /// </summary>
    public static InternalError RuleFailed(string rule = "name-empty") =>
        Fault(message: $"Valideringsreglen '{rule}' fejlede. Listen kan mangle fejl.",
              diagnostic: $"Rule '{rule}' threw");

    /// <summary>A fixture under <c>tests/testdata/projects</c>, beside the built test assembly.</summary>
    public static string FixturePath(string name) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "projects", name);

    /// <summary>
    /// A route planner over the SDK's real compose door — the same wiring the panel uses. Row projection needs
    /// one, and a stubbed descriptor would let a projected row claim a field the dialog does not offer.
    /// </summary>
    public static ihc_openvisual.ViewModels.ProblemNavigationPlanner Planner(Ihc.Vis.ProjectAppService service) =>
        ihc_openvisual.ViewModels.ProblemNavigationPlanner.Over(service.GetProductDialog);

    /// <inheritdoc cref="Planner(Ihc.Vis.ProjectAppService)"/>
    public static ProblemNavigationPlanner Planner(ShellHarness harness) =>
        ProblemNavigationPlanner.Over(harness.Session.GetProductDialog);

    /// <summary>
    /// For a projection with NO snapshot, where the planner is never consulted — there is no project to plan
    /// over, so the row's kind is decided without it.
    /// </summary>
    public static ihc_openvisual.ViewModels.ProblemNavigationPlanner UnusedPlanner { get; } =
        ihc_openvisual.ViewModels.ProblemNavigationPlanner.Over(
            (_, _) => new Ihc.Vis.Products.ProductDialogDescriptor("", []));

    /// <summary>
    /// A row as the panel would have built it — carrying the FINDING, attribute included.
    /// <para>The attribute has to be on the finding, not merely used to compute the kind: activation re-plans
    /// from the finding over the current document, which is what makes the route right for the document the
    /// installer is about to edit rather than for the one the run saw.</para>
    /// </summary>
    public static ProblemRowViewModel RowFor(
        NavigationPlan plan, ElementId? site, string? attribute, string code,
        ValidationSeverity severity = ValidationSeverity.Warning,
        ValidationCategory category = ValidationCategory.Documentation,
        string elementName = "navn") =>
        new(new ValidationFinding(
                new Problem(new ProblemCode(code), "Besked", EquatableArray<ProblemArgument>.Empty),
                severity, category,
                new FindingLocation(site?.ToToken() ?? "utcs_project", site, null),
                EquatableArray<FindingLocation>.Empty)
            { TargetAttribute = attribute },
            site, elementName, plan.Kind, $"{code}@x");

    /// <summary>A row over a finding the caller already has — the shape a rule's real emission takes.</summary>
    public static ProblemRowViewModel RowFor(
        NavigationPlan plan, ValidationFinding finding, ElementId? site, string elementName = "navn") =>
        new(finding, site, elementName, plan.Kind, $"{finding.Code.Value}@x");

    /// <summary>Activates a finding through the panel, exactly as a double-click or Enter would.</summary>
    public static Task ActivateAsync(
        ShellHarness harness, MainWindowViewModel vm, ElementId? site, string? attribute, string code)
    {
        NavigationPlan plan =
            Planner(harness).Plan(harness.Session.Current!, site, attribute, new ProblemCode(code));
        return vm.Problems.ActivateRowAsync(RowFor(plan, site, attribute, code));
    }
}

/// <summary>
/// The panel ALONE, over a validation that returns exactly what the test hands it. The shape for anything about
/// rows, tiers, sorting or state, because it makes the result the test's own input rather than a fixture's.
///
/// <para>Shared rather than repeated per file because the panel's construction is not a detail a test gets to
/// have an opinion about: the marshal must be the synchronous <c>action =&gt; action()</c> and the clock must be
/// the fake one, or the debounce never elapses and the test hangs instead of failing.</para>
///
/// <para><b>Settling is the whole protocol.</b> Validation is debounced and then runs on the pool, so every
/// assertion about rows, counts or state has to advance the clock past the quiet period and then await the
/// worker going idle. <see cref="SettleAsync"/> is that step; an assertion made without it is racing the panel
/// rather than testing it.</para>
/// </summary>
internal sealed class ProblemsRig : IDisposable
{
    public FakeTimeProvider Clock { get; } = new();

    public ShellHarness Harness { get; }

    /// <summary>The monitor the panel presents — this rig's own, over the findings the test wrote.</summary>
    public ValidationMonitor Validation { get; }

    public ProblemsPanelViewModel Panel { get; }

    public ProblemsRig(params ValidationFinding[] findings)
        : this(_ => ImmutableArray.Create(findings))
    {
    }

    /// <summary>Every export request the panel handed over, in order — what an export test asserts against.</summary>
    public List<FindingsExportRequest> Exported { get; } = [];

    /// <summary>The fault sink the panel presents beside its findings — this rig owns it, so a test can append
    /// to it directly.</summary>
    public ihc_openvisual.Services.InternalErrorLog InternalErrors { get; } = new();

    public ProblemsRig(Func<Ihc.Vis.Projects.Project, EquatableArray<ValidationFinding>> validate)
        : this(project => new StructuredValidationResult(validate(project), EquatableArray<InternalError>.Empty))
    {
    }

    /// <summary>
    /// The rig over a STRUCTURED result — the door for a test about the fault channel rather than the findings
    /// one, and for a <paramref name="validate"/> that throws.
    /// </summary>
    /// <remarks>
    /// The findings overload above delegates here. Rebuilt by hand per fixture it was the same
    /// harness→monitor→panel wiring three times over, each free to wire <c>onFault</c> or <c>internalErrors</c>
    /// slightly differently from the rig it was copied from — and a panel wired to a different sink than its
    /// monitor reports to is a test that can only ever pass.
    /// </remarks>
    public ProblemsRig(Func<Ihc.Vis.Projects.Project, StructuredValidationResult> validate)
    {
        Harness = ShellHarness.Create(Clock);
        // A monitor of its own rather than the session's: the panel is the thing under test here, and it must be
        // drivable over findings the test chose rather than over whatever the real engine happens to produce.
        Validation = new ValidationMonitor(Harness.Session, validate, onFault: InternalErrors.Append);
        // The export delegate RECORDS rather than writes: what the panel decides to export is the panel's
        // behaviour, and where it ends up is the workflow's.
        Panel = new ProblemsPanelViewModel(Harness.Session, Validation, export: request =>
        {
            Exported.Add(request);
            return Task.CompletedTask;
        }, internalErrors: InternalErrors);
    }

    /// <summary>Advances past the quiet period and waits for the run it starts to finish.</summary>
    public async Task SettleAsync()
    {
        Clock.Advance(ValidationWorker.DefaultDebounce);
        await Panel.Idle.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>A new project, validated once — the state most assertions start from.</summary>
    public async Task<ProblemsRig> WithNewProjectAsync()
    {
        await Harness.Session.NewAsync();
        await SettleAsync();
        return this;
    }

    public void Dispose()
    {
        Panel.Dispose();
        Validation.Dispose();
        Harness.Dispose();
    }
}

/// <summary>
/// The WHOLE shell, panel included. The shape for anything that crosses the panel's edge — navigation into the
/// trees, the send gate, the view's own rendering — because those need the shell's commands and its real
/// <see cref="ProjectAppService"/>-backed validation, not a stand-in.
/// </summary>
internal sealed class ProblemsShellRig : IDisposable
{
    public FakeTimeProvider Clock { get; } = new();

    public ShellHarness Harness { get; }

    public MainWindowViewModel Shell { get; }

    public ProblemsPanelViewModel Panel => Shell.Problems;

    /// <summary>The session's own validation — the one the send gate reads, not the panel's view of it.</summary>
    public ValidationMonitor Validation => Harness.Session.Validation;

    public ProblemsShellRig()
    {
        Harness = ShellHarness.Create(Clock);
        Shell = Harness.CreateViewModel();
    }

    /// <inheritdoc cref="ProblemsRig.SettleAsync"/>
    public async Task SettleAsync()
    {
        Clock.Advance(ValidationWorker.DefaultDebounce);
        await Panel.Idle.WaitAsync(TimeSpan.FromSeconds(30));
    }

    public void Dispose()
    {
        Shell.Dispose();
        Harness.Dispose();
    }
}

/// <summary>
/// The whole shell over the authored error corpus, in a SHOWN window. The shape for anything that measures what a
/// reader actually gets — realized rows, their automation identities, the panel's type and spacing — because none
/// of that exists until a window has been shown and the dispatcher has run.
/// </summary>
/// <remarks>
/// <para>Composes <see cref="ProblemsShellRig"/> and adds only the window, so the harness, the fake clock and the
/// settle protocol stay defined once.</para>
/// <para>Only ONE window is ever shown: under Avalonia headless a second window renders blank, which reads at the
/// assertion as content that failed to load rather than as a rig that showed too much.</para>
/// </remarks>
internal sealed class ProblemsWindowRig : IDisposable
{
    private readonly ProblemsShellRig _inner = new();

    public FakeTimeProvider Clock => _inner.Clock;

    public ShellHarness Harness => _inner.Harness;

    public MainWindowViewModel Shell => _inner.Shell;

    public MainWindow Window { get; }

    private ProblemsWindowRig()
    {
        Window = new MainWindow { DataContext = Shell };
    }

    /// <summary>Loads the authored error fixture, settles the validation run and shows the window.</summary>
    public static async Task<ProblemsWindowRig> ShowingFindingsAsync()
    {
        ProblemsWindowRig rig = new();
        await rig.Shell.InitializeAsync(ProblemsTestData.FixturePath("Project6-Errors.vis"));
        await rig._inner.SettleAsync();

        // The screenshot-on-failure hook: a failing test over this rig produces a PNG of THIS window.
        AvaloniaTestBase.CurrentTestWindow = rig.Window;
        rig.Window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(rig.Shell.Problems.Rows, Is.Not.Empty,
            "sanity: the fixture must produce findings, or every assertion over this rig is vacuous");
        return rig;
    }

    public void Dispose()
    {
        Window.Close();
        _inner.Dispose();
    }
}
