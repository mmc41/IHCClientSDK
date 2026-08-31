using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis.Problems;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace safe_visual_tests;

/// <summary>A scriptable <see cref="IDialogService"/> for headless tests: canned answers for the save prompt,
/// recovery confirm, and file pickers, plus call counters — no real UI.</summary>
public sealed class FakeDialogService : IDialogService
{
    public SaveChangesResult SaveChangesResult { get; set; } = SaveChangesResult.Discard;
    public bool ConfirmResult { get; set; }
    public string? OpenPath { get; set; }
    public string? SavePath { get; set; }
    public int ConfirmSaveCalls { get; private set; }
    public string? LastMessage { get; private set; }
    public PropertiesResult? PropertiesResult { get; set; }
    public string? LastPropertiesTitle { get; private set; }
    public string? LastPropertiesName { get; private set; }
    public string? LastPropertiesNote { get; private set; }
    public int EditPropertiesCalls { get; private set; }
    public VariablePropertiesResult? VariablePropertiesResult { get; set; }
    public VariablePropertiesInput? LastVariablePropertiesInput { get; private set; }
    public int EditVariablePropertiesCalls { get; private set; }

    public SceneContainerResult? SceneContainerResult { get; set; }
    public SceneContainerInput? LastSceneContainerInput { get; private set; }
    public int EditSceneContainerCalls { get; private set; }
    public PinPropertiesResult? PinPropertiesResult { get; set; }
    public PinPropertiesInput? LastPinPropertiesInput { get; private set; }
    public int EditPinPropertiesCalls { get; private set; }
    // The ONE generic product dialog. There is no *Input record to record: the dialog's input IS the composed
    // descriptor, so a test that wants to know what the installer was offered asserts on LastProductDialog.
    public ProductDialogDescriptor? LastProductDialog { get; private set; }
    public int EditProductDialogCalls { get; private set; }
    /// <summary>Answers the dialog. Null (the default) means CANCEL; return an empty edit list for the ordinary
    /// "OK without touching anything", which is a commit and not a cancel.</summary>
    public Func<ProductDialogDescriptor, ProductDialogEdits?>? ProductDialogResponder { get; set; }

    /// <summary>
    /// What the installer STEPS INTO before answering — a terminal row, a settings row — or null to answer
    /// straight away. Asked repeatedly, so a script can step into several composites in one visit.
    /// <para>Its own channel since T058. It used to ride on the result record's <c>WidgetAction</c>, which was
    /// the close-then-reopen protocol: the dialog closed and handed the caller what to open next. The window
    /// stays open now, so a step is not something the result can express, and a fake that kept modelling it
    /// that way would be scripting a protocol the product no longer has.</para>
    /// </summary>
    public Func<ProductDialogDescriptor, ProductDialogWidgetAction?>? ProductDialogStepper { get; set; }
    /// <summary>What <i>Rediger konstant</i> answers. Null is the dismissal, as for every other editor here.</summary>
    public string? ConstantResult { get; set; }
    public ConstantEditorInput? LastConstantInput { get; private set; }
    public int EditConstantCalls { get; private set; }
    public SceneValueResult? SceneValueResult { get; set; }
    public SceneValueInput? LastSceneValueInput { get; private set; }
    public int EditSceneValueCalls { get; private set; }
    public EnumDefinitionResult? EnumDefinitionResult { get; set; }
    public Func<EnumTypeManagerInput, Task>? EnumTypeManagerScript { get; set; }
    public EnumTypeManagerInput? LastEnumTypeManagerInput { get; private set; }
    public string? NamePromptResult { get; set; }
    public NamePromptInput? LastNamePromptInput { get; private set; }
    public EnumDefinitionInput? LastEnumDefinitionInput { get; private set; }
    public int EditEnumDefinitionCalls { get; private set; }
    public Func<EnumDefinitionInput, EnumDefinitionResult?>? EnumDefinitionResponder { get; set; }
    public ProjectInfoData? ProjectInfoResult { get; set; }
    public ProjectInfoData? LastProjectInfoInput { get; private set; }
    public int EditProjectInfoCalls { get; private set; }
    public Func<ProjectInfoData, ProjectInfoData?>? ProjectInfoResponder { get; set; }

    /// <summary>When set, the save prompt throws this instead of answering — the fault-injection seam for the
    /// window-lifecycle containment tests (a Closing handler runs off the window message loop, outside every global
    /// exception handler, so what it does with a throw is a behaviour worth pinning).</summary>
    public Exception? ConfirmSaveChangesThrows { get; set; }

    public Task<SaveChangesResult> ConfirmSaveChangesAsync(string documentName)
    {
        ConfirmSaveCalls++;
        if (ConfirmSaveChangesThrows is { } ex)
            throw ex;
        return Task.FromResult(SaveChangesResult);
    }

    public int ConfirmCalls { get; private set; }
    public string? LastConfirmTitle { get; private set; }
    public string? LastConfirmMessage { get; private set; }
    public Task<bool> ConfirmAsync(string title, string message)
    {
        ConfirmCalls++;
        LastConfirmTitle = title;
        LastConfirmMessage = message;
        return Task.FromResult(ConfirmResult);
    }
    /// <summary>The title of the last <see cref="ShowMessageAsync"/> box. A registered difference (the register's
    /// "a refused edit says what to do about it") is about the title AND the sentence, so both are recorded.</summary>
    public string? LastMessageTitle { get; private set; }
    public Task ShowMessageAsync(string title, string message)
    {
        LastMessageTitle = title;
        LastMessage = message;
        return Task.CompletedTask;
    }

    /// <summary>The last coded problem SHOWN, so a test can assert on its identity instead of on its prose.</summary>
    public Problem? LastProblem { get; private set; }

    /// <summary>Forgets what has been shown so far — how a test asserts on ITS OWN operation rather than on
    /// whatever set-up happened to raise.</summary>
    public void Reset()
    {
        LastProblem = null;
        LastProblemChain = null;
        LastOpenedUrl = null;
    }

    /// <summary>The chain, when the site framed an SDK failure — the operation is on it, unrendered.</summary>
    public ProblemChain? LastProblemChain { get; private set; }

    // The coded doors record BOTH: the identity for a code assertion, and the rendered text through the shell's own
    // presentation path, so the existing message assertions keep testing what the installer actually reads.
    public Task ShowProblemAsync(string title, Problem problem)
    {
        LastProblem = problem;
        LastProblemChain = null;
        return ShowMessageAsync(title, ProblemPresenter.Text(problem));
    }

    public Task ShowProblemAsync(string title, ProblemChain chain)
    {
        LastProblem = chain.Cause;
        LastProblemChain = chain;
        return ShowMessageAsync(title, ProblemPresenter.Text(chain));
    }

    /// <summary>The aggregate, when the site showed a head plus N independent items (a refused validation).</summary>
    public ProblemAggregate? LastProblemAggregate { get; private set; }

    public Task ShowProblemAsync(string title, ProblemAggregate aggregate)
    {
        LastProblem = aggregate.Head;
        LastProblemChain = null;
        LastProblemAggregate = aggregate;
        return ShowMessageAsync(title, ProblemPresenter.Text(aggregate));
    }
    public Task<string?> PickOpenProjectAsync(string? initialDirectory) => Task.FromResult(OpenPath);
    public Task<string?> PickSaveProjectAsync(string? initialDirectory, string suggestedFileName) => Task.FromResult(SavePath);
    public string? SaveReportPath { get; set; }
    public string? LastReportSuggestedName { get; private set; }

    /// <summary>The format the last REPORT save dialog was opened for, or null if none has been.</summary>
    public ReportFormat? LastReportFormat { get; private set; }

    /// <summary>Whether the last save dialog was the findings list's own door rather than a report's. The two
    /// are separate methods on the port, so which one a caller reached is the assertion — there is no longer a
    /// mimetype travelling through one door that a test could read a format off.</summary>
    public bool AskedForFindings { get; private set; }

    public Task<string?> PickSaveReportAsync(string suggestedFileName, ReportFormat format)
    {
        LastReportSuggestedName = suggestedFileName;
        LastReportFormat = format;
        AskedForFindings = false;
        return Task.FromResult(SaveReportPath);
    }

    public Task<string?> PickSaveFindingsAsync(string suggestedFileName)
    {
        LastReportSuggestedName = suggestedFileName;
        AskedForFindings = true;
        return Task.FromResult(SaveReportPath);
    }
    public string? CatalogFilePath { get; set; }
    public string? CatalogFolderPath { get; set; }
    public Task<string?> PickCatalogFileAsync() => Task.FromResult(CatalogFilePath);
    public Task<string?> PickCatalogFolderAsync() => Task.FromResult(CatalogFolderPath);
    public Task ShowAboutAsync() => Task.CompletedTask;
    /// <summary>Every internal error the shell asked to show, in order — what an activation test asserts on.</summary>
    public List<Ihc.Vis.Problems.InternalError> ShownInternalErrors { get; } = [];
    public Task ShowInternalErrorAsync(Ihc.Vis.Problems.InternalError error)
    {
        ShownInternalErrors.Add(error);
        return Task.CompletedTask;
    }
    /// <summary>The text the settings dialog was last asked to show, so a test can assert on the readout.</summary>
    public string? LastSettingsText { get; private set; }
    public Task ShowSettingsAsync(string settingsText) { LastSettingsText = settingsText; return Task.CompletedTask; }
    public string? LastOpenedUrl { get; private set; }
    /// <summary>What the next external-open reports. False simulates a machine with no handler for the document —
    /// the case that used to be swallowed as success (UX review CORE-03).</summary>
    public bool OpenExternalUrlSucceeds { get; set; } = true;
    public Task<bool> OpenExternalUrlAsync(string url) { LastOpenedUrl = url; return Task.FromResult(OpenExternalUrlSucceeds); }

    public LibraryOrigin? LastPropertiesOrigin { get; private set; }
    public string? LastPropertiesAffirmative { get; private set; }

    /// <summary>The caption asked for over the editable Name/Note pair — "Bruger egenskaber" on a function block,
    /// null elsewhere (F-24).</summary>
    public string? LastPropertiesUserGroup { get; private set; }

    public bool? LastPropertiesConditionsOr { get; private set; }

    public Task<PropertiesResult?> EditPropertiesAsync(string title, string name, string note, LibraryOrigin? origin = null,
        string affirmative = "OK", string? userGroupCaption = null, bool? conditionsOr = null,
        ElementDialogField? focus = null)
    {
        EditPropertiesCalls++;
        LastPropertiesFocus = focus;
        LastPropertiesTitle = title;
        LastPropertiesName = name;
        LastPropertiesNote = note;
        LastPropertiesOrigin = origin;
        LastPropertiesAffirmative = affirmative;
        LastPropertiesUserGroup = userGroupCaption;
        LastPropertiesConditionsOr = conditionsOr;
        return Task.FromResult(PropertiesResult);
    }

    /// <summary>Which field a route asked the element dialog to open on (T044); null for an ordinary open.</summary>
    public ElementDialogField? LastPropertiesFocus { get; private set; }

    public Task<VariablePropertiesResult?> EditVariablePropertiesAsync(VariablePropertiesInput input)
    {
        EditVariablePropertiesCalls++;
        LastVariablePropertiesInput = input;
        return Task.FromResult(VariablePropertiesResult);
    }

    public Task<SceneContainerResult?> EditSceneContainerAsync(SceneContainerInput input)
    {
        EditSceneContainerCalls++;
        LastSceneContainerInput = input;
        return Task.FromResult(SceneContainerResult);
    }

    /// <summary>
    /// Answers the terminal editor, and runs WHILE the visit that opened it is still in progress.
    /// <para>A test that needs to observe the document mid-visit — has anything been written yet? — uses this
    /// rather than <see cref="PinPropertiesResult"/>, because the plain value cannot say WHEN it was read.</para>
    /// </summary>
    public Func<PinPropertiesInput, PinPropertiesResult?>? PinPropertiesResponder { get; set; }

    /// <summary>What the next dialog presses Anvend with, before answering — the vendor's apply-and-stay-open.
    /// Null for a dialog that only ever answers.</summary>
    public PinPropertiesResult? PinPropertiesApply { get; set; }

    /// <summary>
    /// The Anvend callback the last dialog was handed, KEPT so a test can invoke it after the opening call has
    /// returned — which is the only way to reproduce what the real window does. A window presses Anvend on its
    /// OWN stack, long after the flow that supplied the callback left its error boundary; a fake that only
    /// invoked it inside the awaited call would be back inside that boundary and would prove nothing about it.
    /// </summary>
    public System.Func<PinPropertiesResult, Task>? LastPinApply { get; private set; }

    public async Task<PinPropertiesResult?> EditPinPropertiesAsync(PinPropertiesInput input, System.Func<PinPropertiesResult, Task>? onApply = null)
    {
        EditPinPropertiesCalls++;
        LastPinPropertiesInput = input;
        LastPinApply = onApply;
        // Anvend BEFORE the answer, through the same callback the real window uses, so a test can exercise the
        // apply-and-stay-open path rather than only the closing one.
        if (PinPropertiesApply is { } applied && onApply is not null)
        {
            await onApply(applied);
        }
        return PinPropertiesResponder is not null ? PinPropertiesResponder(input) : PinPropertiesResult;
    }

    /// <summary>Makes the generic product dialog answer Cancel. Needed explicitly because the DEFAULT is
    /// OK-without-edits: the dialog opens as part of PLACING a product (uxparity S-12), so "no result configured"
    /// has to mean the ordinary path — a test that wanted a product would otherwise silently get none.</summary>
    public bool CancelProductDialog { get; set; }

    /// <summary>The terminal rows handed to the last dialog — what the grids would have shown.</summary>
    public IReadOnlyList<ProductTerminal>? LastProductDialogTerminals { get; private set; }

    /// <summary>The settings rows handed to the last dialog — what the Indstillinger grid would have shown.</summary>
    public IReadOnlyList<ProductSetting>? LastProductDialogSettings { get; private set; }

    /// <summary>The options the last dialog was opened with — where a route asked it to land.</summary>
    public ProductDialogShowOptions? LastProductDialogOptions { get; private set; }

    /// <summary>
    /// The composites the fake stepped into on the caller's behalf, in order.
    /// <para>The real window calls the step handler and STAYS OPEN. The fake plays that: it invokes the handler
    /// for whatever widget action the responder asked for, then returns the responder's edits as the dialog's
    /// own result — so a test sees one dialog visit with a sub-dialog inside it, not two visits.</para>
    /// </summary>
    public List<ProductDialogWidgetAction> SteppedInto { get; } = [];

    /// <summary>
    /// What the dialog was told to SHOW after the last step — the re-projection a test reads to see the visit's
    /// pending state as the installer would, without touching the document.
    /// </summary>
    public ProductDialogRefresh? LastRefresh { get; private set; }

    public async Task<ProductDialogEdits?> EditProductDialogAsync(
        ProductDialogDescriptor descriptor, IReadOnlyList<ProductTerminal>? terminals = null,
        IReadOnlyList<ProductSetting>? settings = null,
        ProductDialogShowOptions? options = null,
        ProductDialogStep? onStep = null)
    {
        EditProductDialogCalls++;
        LastProductDialog = descriptor;
        LastProductDialogTerminals = terminals;
        LastProductDialogSettings = settings;
        LastProductDialogOptions = options;
        if (CancelProductDialog)
            return null;
        // The ROUTE's own step, before the installer does anything. The real window fires the arrival's initial
        // action as it opens, through the same door a click uses; a fake that skipped it would let a routing test
        // pass while the route never actually stepped anywhere.
        if (options?.InitialAction is { } arrival && onStep is not null)
        {
            SteppedInto.Add(arrival);
            LastRefresh = await onStep(arrival);
        }

        // ONE dialog, asked repeatedly what the installer did next. A step keeps it open — the real window runs
        // the handler and stays put — so the fake runs the handler and asks again; the answer comes only once
        // the script steps into nothing more, and null there is Annuller.
        //
        // The two are separate channels because they are separate acts: conflating them made "step into a
        // terminal, then cancel the dialog" inexpressible, since one return value had to be both.
        if (ProductDialogStepper is not null && onStep is not null)
        {
            for (int step = 0; step < 16; step++)
            {
                if (ProductDialogStepper(descriptor) is not { } action)
                {
                    break;
                }
                SteppedInto.Add(action);
                LastRefresh = await onStep(action);
                if (step == 15)
                {
                    throw new InvalidOperationException(
                        "The product-dialog stepper kept stepping into composites and never stopped.");
                }
            }
        }

        if (ProductDialogResponder is not null)
        {
            return ProductDialogResponder(descriptor);
        }
        // The default is OK with NOTHING changed — the untouched-OK commit. Echoing every field back as an "edit"
        // would make every test that opens this dialog also a test of writing every attribute.
        return new ProductDialogEdits([]);
    }

    // ── Reading and answering the composed dialog, by CAPTION ────────────────────────────────────────────────
    // A test asks what the installer was OFFERED and answers as they would. Caption rather than attribute name
    // because the caption is what the dialog puts on screen, and it is what the vendor oracle records.

    private IEnumerable<DialogDescriptorField> OfferedFields =>
        LastProductDialog?.Groups.SelectMany(g => g.Fields) ?? [];

    /// <summary>Whether the last dialog offered a field with this caption at all.</summary>
    public bool Offered(string caption) => OfferedFields.Any(f => f.Caption == caption);

    /// <summary>The value the last dialog SHOWED for a field, or null when it offered no such field.</summary>
    public string? OfferedValue(string caption) =>
        OfferedFields.FirstOrDefault(f => f.Caption == caption)?.Value;

    /// <summary>Whether the last dialog showed a field greyed out (a locked product's Navn).</summary>
    public bool OfferedReadOnly(string caption) =>
        OfferedFields.FirstOrDefault(f => f.Caption == caption)?.ReadOnly ?? false;

    /// <summary>Whether the last dialog hosted a hand-written composite — the terminal grids, the Avanceret button.</summary>
    public bool OfferedWidget(DialogWidgetKind kind) =>
        LastProductDialog?.Groups.Any(g => g.Widgets.Contains(kind)) ?? false;

    /// <summary>Answers OK, having edited the named fields. A caption the dialog does not offer is an ERROR, not a
    /// silent no-op: a test that thinks it typed into a field which is not there is testing nothing.</summary>
    public void RespondWithEdits(params (string Caption, string Value)[] edits) =>
        ProductDialogResponder = descriptor => new ProductDialogEdits(
            [.. edits.Select(e =>
            {
                DialogDescriptorField field = descriptor.Groups.SelectMany(g => g.Fields)
                    .FirstOrDefault(f => f.Caption == e.Caption)
                    ?? throw new InvalidOperationException(
                        $"The dialog for '{descriptor.Title}' offers no field captioned '{e.Caption}'. "
                        + $"It offers: {string.Join(", ", descriptor.Groups.SelectMany(g => g.Fields).Select(f => f.Caption))}");
                return new ProductDialogEdit(field.Target, field.Attribute, e.Value);
            })]);

    /// <summary>
    /// Steps into a hand-written composite, as clicking <i>Avanceret</i> or a terminal row does.
    /// <para>ONCE PER OPEN, not once per fake. The dialog stays open across a step and is asked again what the
    /// installer did next, so a stepper that returned the same action every time would step for ever — which is
    /// what the installer pressing the button once does not do. Keyed on the open count rather than a plain
    /// flag, so a test that opens the dialog twice — typically to read back what the second open was offered —
    /// steps in both times, exactly as an installer pressing the button in each would.</para>
    /// </summary>
    private void StepOncePerOpen(DialogWidgetKind kind, ElementId? target)
    {
        int steppedInOpen = -1;
        ProductDialogStepper = _ =>
        {
            if (steppedInOpen == EditProductDialogCalls)
            {
                return null;
            }
            steppedInOpen = EditProductDialogCalls;
            return new ProductDialogWidgetAction(kind, target);
        };
    }

    /// <summary>Steps into one terminal row, once per open, and leaves the answer to the caller's responder.</summary>
    public void StepIntoTerminalOnce(ElementId pin) =>
        StepOncePerOpen(DialogWidgetKind.TerminalGrids, pin);

    public void RespondWithWidget(DialogWidgetKind kind, ElementId? target = null)
    {
        StepOncePerOpen(kind, target);
        ProductDialogResponder = _ => new ProductDialogEdits([]);
    }

    public Task<string?> EditConstantAsync(ConstantEditorInput input)
    {
        EditConstantCalls++;
        LastConstantInput = input;
        return Task.FromResult(ConstantResult);
    }

    public Task<SceneValueResult?> EditSceneValueAsync(SceneValueInput input)
    {
        EditSceneValueCalls++;
        LastSceneValueInput = input;
        return Task.FromResult(SceneValueResult);
    }

    public Task<EnumDefinitionResult?> EditEnumDefinitionAsync(EnumDefinitionInput input)
    {
        EditEnumDefinitionCalls++;
        LastEnumDefinitionInput = input;
        return Task.FromResult(EnumDefinitionResponder is not null ? EnumDefinitionResponder(input) : EnumDefinitionResult);
    }

    // The manager applies LIVE (there is nothing to return), so the fake plays the installer: it runs whatever
    // script the test set against the same (Types, Apply) pair the real dialog is handed.
    public async Task ManageEnumTypesAsync(EnumTypeManagerInput input)
    {
        LastEnumTypeManagerInput = input;
        if (EnumTypeManagerScript is not null)
        {
            await EnumTypeManagerScript(input);
        }
    }

    public Task<string?> PromptForNameAsync(NamePromptInput input)
    {
        LastNamePromptInput = input;
        return Task.FromResult(NamePromptResult);
    }

    public ProjectInfoSuggestions? LastProjectInfoSuggestions { get; private set; }

    public Task<ProjectInfoData?> EditProjectInfoAsync(ProjectInfoData current, ProjectInfoSuggestions suggestions)
    {
        EditProjectInfoCalls++;
        LastProjectInfoInput = current;
        LastProjectInfoSuggestions = suggestions;
        return Task.FromResult(ProjectInfoResponder is not null ? ProjectInfoResponder(current) : ProjectInfoResult);
    }

    public ihc_openvisual.ViewModels.ReportPickerViewModel? LastReportPickerViewModel { get; private set; }
    public int ShowReportPickerCalls { get; private set; }
    public Task ShowReportPickerAsync(IReportPickerViewModel viewModel)
    {
        ShowReportPickerCalls++;
        LastReportPickerViewModel = viewModel as ihc_openvisual.ViewModels.ReportPickerViewModel;   // tests use the concrete VM (T015 seam)
        return Task.CompletedTask;
    }

    public DatalineModuleMap? LastModuleMap { get; private set; }
    public int ShowModuleMapCalls { get; private set; }
    public Task ShowModuleMapAsync(DatalineModuleMap map)
    {
        ShowModuleMapCalls++;
        LastModuleMap = map;
        return Task.CompletedTask;
    }
}

/// <summary>
/// A real <see cref="ILogger"/> that records what was written to it. NOT a mock: main code depends only on the
/// <see cref="ILogger"/> abstraction (never on an implementation), so tests assert on real logged OUTPUT — which is
/// also why the repo forbids mocking logger interfaces. Shared by every suite that has to prove a failure reached
/// the logging pipeline (and therefore OTLP) rather than vanishing.
/// </summary>
public sealed class CapturingLogger : ILogger
{
    public List<string> Messages { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Messages.Add($"{logLevel}: {formatter(state, exception)}{(exception is null ? "" : " | " + exception.Message)}");
}

/// <summary>An <see cref="ILoggerFactory"/> that hands every category the SAME <see cref="CapturingLogger"/>, so a
/// test can inject it into a component and read back everything that component logged.</summary>
public sealed class CapturingLoggerFactory : ILoggerFactory
{
    public CapturingLogger Logger { get; } = new();
    public List<string> Messages => Logger.Messages;

    /// <summary>Forgets what has been logged so far, so a test asserts on its own operation.</summary>
    public void Clear() => Logger.Messages.Clear();
    public ILogger CreateLogger(string categoryName) => Logger;
    public void AddProvider(ILoggerProvider provider) { }
    public void Dispose() { }
}

/// <summary>Builds file-only <see cref="ProjectWorkflow"/>/<see cref="MainWindowViewModel"/> instances over a
/// throwaway temp directory, with a fake dialog service and no controller — the whole shell is exercised without
/// a network, controller or IHC install.</summary>
public sealed class ShellHarness : IDisposable
{
    public string TempDir { get; }
    public FakeDialogService Dialogs { get; } = new();
    public RecentProjectsStore Recent { get; }
    public ProjectWorkflow Session { get; }

    /// <summary>The same <see cref="ProjectAppService"/> instance <see cref="Session"/> runs on — the SDK facade
    /// tests read the rich catalog (<c>GetAvailableProducts</c>/<c>GetAvailableFunctionBlocks</c>) and other SDK
    /// operations from directly, without going through the GUI workflow's thin delegators (refac3 T007).</summary>
    public ProjectAppService ProjectService { get; }

    /// <summary>
    /// The ONE clock this harness runs on — the workflow's debounces and delays and the facade's timestamps.
    /// A <see cref="FakeTimeProvider"/> unless the caller passed something else, so the test decides when time
    /// moves. Use <see cref="SettleValidationAsync"/> rather than advancing it by hand.
    /// </summary>
    public System.TimeProvider Time { get; }

    private readonly bool _ownsDir;
    private bool _disposed;

    private static int _live;

    /// <summary>
    /// How many harnesses have been constructed and not yet disposed. Asserted back to its pre-test value by
    /// <see cref="NoLeakedHarnessAttribute"/>, so a helper that builds one and drops it is reported against the
    /// test that did it rather than against whichever test is running when its timers next fire.
    /// </summary>
    internal static int Live => System.Threading.Volatile.Read(ref _live);

    private ShellHarness(string dir, bool ownsDir, System.TimeProvider? timeProvider,
                         ILoggerFactory? loggerFactory = null)
    {
        TempDir = dir;
        _ownsDir = ownsDir;
        Directory.CreateDirectory(TempDir);
        Recent = new RecentProjectsStore(Path.Combine(TempDir, "recent.json"));
        // A FAKE clock by default, because the harness marshals INLINE (see the Post argument below). On a system
        // clock every document change arms a real timer, and ~300 ms later a whole-project validation completes on
        // a POOL thread and binds straight into ProblemsPanelViewModel.Rows — a collection bound live from
        // ProblemsPanel.axaml. ADR-001 says that mutation is silently lossy rather than loud, the worker's own
        // catch swallows the fault, and the timer keeps the harness alive past the test that built it, so the
        // damage lands in whatever test is running 300 ms later. A clock nothing advances cannot do any of that.
        // A test that genuinely wants wall-clock behaviour asks for it: Create(System.TimeProvider.System).
        Time = timeProvider ?? new FakeTimeProvider();
        // The facade shares that clock, so metadata and report generation timestamps (T022) are deterministic too.
        ProjectService = new ProjectAppService(new IhcSettings(), new Ihc.Vis.Catalog.BuiltInCatalog(), Time);
        // The catalog dir is a subfolder of TempDir so Restart(dir) reuses it (US-061).
        // The marshal is SYNCHRONOUS here, which is what makes an inline post safe: nothing may reach bound state
        // off the caller's thread, so the clock above must never fire on its own.
        Session = new ProjectWorkflow(
            ProjectService, Recent, Dialogs, loggerFactory, Path.Combine(TempDir, "catalog"),
            post: action => action(), timeProvider: Time);
        System.Threading.Interlocked.Increment(ref _live);
    }

    /// <param name="timeProvider">
    /// The clock to run on. Omit it for a <see cref="FakeTimeProvider"/> the harness owns — the right choice for
    /// every test that does not assert about validation. Pass your own when you need to drive the debounce and
    /// hold the handle; pass <see cref="System.TimeProvider.System"/> to opt back into wall-clock time.
    /// </param>
    public static ShellHarness Create(System.TimeProvider? timeProvider = null,
                                     ILoggerFactory? loggerFactory = null) =>
        new(Path.Combine(Path.GetTempPath(), "ihc_ov_tests", Guid.NewGuid().ToString("N")), ownsDir: true,
            timeProvider, loggerFactory);

    /// <summary>A second session over an existing directory — simulates restarting the app, so the per-user state
    /// left in <paramref name="dir"/> (e.g. persisted catalog imports) is picked up again.</summary>
    public static ShellHarness Restart(string dir) => new(dir, ownsDir: false, null);

    public string TempPath(string fileName) => Path.Combine(TempDir, fileName);

    /// <summary>
    /// Moves this harness's clock, firing whatever its timers owe. A no-op on a real clock, which only a test
    /// that asked for <see cref="System.TimeProvider.System"/> can have.
    /// </summary>
    public void Advance(TimeSpan by)
    {
        if (Time is FakeTimeProvider fake)
        {
            fake.Advance(by);
        }
    }

    /// <summary>
    /// Advances past the validation quiet period and waits for the run it starts to finish — the step any
    /// assertion about findings, the Problemer panel or the transfer gate has to take first.
    /// <para>Validation is debounced and then runs on the pool, so a result exists only once the clock has moved
    /// and the worker has gone idle. Asserting without this races the panel rather than testing it; on this
    /// harness's fake clock the run never starts at all, so the assertion is simply wrong rather than flaky.</para>
    /// </summary>
    /// <param name="monitor">
    /// The monitor to wait on — pass one when the test built a <see cref="ValidationMonitor"/> of its own over
    /// this session, since the clock is shared but the idle signal is not.
    /// </param>
    public async Task SettleValidationAsync(ValidationMonitor? monitor = null)
    {
        Advance(ValidationWorker.DefaultDebounce);
        await (monitor ?? Session.Validation).Idle.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>The per-test LIBRARY folder — the same one the session was constructed with, so a test can assert
    /// where "save to the library" put the block without reaching the real %APPDATA% catalog.</summary>
    public string CatalogDir => Path.Combine(TempDir, "catalog");

    /// <summary>The shell view-model over this harness. Pass <paramref name="loggerFactory"/> (a
    /// <see cref="CapturingLoggerFactory"/>) when the test needs to prove a failure reached the logging pipeline,
    /// and <paramref name="theme"/> (the real <c>ThemeService</c>) when it needs the appearance choices to reach
    /// the running application's resources rather than being recorded inertly.</summary>
    /// <para>The marshal and the clock the Problemer panel runs on come from <see cref="Session"/>, so a test
    /// that needs a controllable debounce passes its clock to <see cref="Create"/> rather than here.</para>
    public MainWindowViewModel CreateViewModel(ILoggerFactory? loggerFactory = null, IThemeService? theme = null,
                                               ihc_openvisual.Configuration.AppConfiguration? config = null,
                                               InternalErrorLog? internalErrors = null) =>
        new(Session, Dialogs, Recent, theme ?? new NullThemeService(), config, loggerFactory, internalErrors);

    /// <summary>
    /// The setup every programming-mode test shares: an initialized shell with an empty (unlocked) function block
    /// inserted into the first locality, already switched into programming mode on that block. One home for the
    /// <c>FunctionNodes[0].Children[0].Children[0]</c> tree path, so a tree-shape change is one edit rather than ten.
    /// <para>
    /// In programming mode the block's four variable sections are <c>vm.InstallationNodes[0].Children[…]</c>
    /// (0 = Inputs, 3 = Internal variables) — the caller picks the one it needs.
    /// </para>
    /// </summary>
    public async Task<MainWindowViewModel> EnterProgrammingModeOnNewBlockAsync()
    {
        MainWindowViewModel vm = await WithNewFunctionBlockAsync();
        vm.EnterProgrammingModeCommand.Execute(NewBlockNode(vm));
        return vm;
    }

    /// <summary>The same placement WITHOUT entering programming mode — for the gestures that act on the block
    /// from the configuration tree, where entering the block would change what is under test.</summary>
    public async Task<MainWindowViewModel> WithNewFunctionBlockAsync()
    {
        MainWindowViewModel vm = CreateViewModel();
        await vm.InitializeAsync();
        ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await Session.AddEmptyFunctionBlockAsync(locality);
        return vm;
    }

    /// <summary>The placed block's own tree row — the <c>FunctionNodes[0].Children[0].Children[0]</c> path, in
    /// one place so a tree-shape change is one edit.</summary>
    public static TreeNodeViewModel NewBlockNode(MainWindowViewModel vm) =>
        vm.FunctionNodes[0].Children[0].Children[0];

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        System.Threading.Interlocked.Decrement(ref _live);
        Session.Dispose();
        if (_ownsDir)
        {
            try { Directory.Delete(TempDir, recursive: true); } catch { /* best-effort temp cleanup */ }
        }
    }
}

/// <summary>
/// The static supervisor port, attached for the life of a <c>using</c> and detached on the way out.
/// </summary>
/// <remarks>
/// The port is process-wide static, so a test that attaches one and returns without detaching leaks it into
/// whatever runs next — the fixture that follows then collects another test's faults, or reports into a list that
/// has gone out of scope. Written out per test it was a <c>try</c>/<c>finally</c> whose <c>finally</c> is the line
/// a new test forgets; a <c>using</c> cannot be forgotten halfway.
/// </remarks>
internal sealed class SupervisedFaults : IDisposable
{
    /// <summary>Every fault the supervisor reported while this capture was attached, in order.</summary>
    public List<Ihc.Vis.Problems.InternalError> Rows { get; } = [];

    private SupervisedFaults() => TaskSupervisor.ReportTo(Rows.Add);

    /// <summary>Attaches a fresh capture to the supervisor's port.</summary>
    public static SupervisedFaults Capture() => new();

    public void Dispose() => TaskSupervisor.ReportTo(null);
}

/// <summary>
/// A temporary directory that removes itself, whatever the test does.
/// </summary>
/// <remarks>
/// A trailing <c>Directory.Delete</c> at the end of a test body is skipped by every assertion that fails above
/// it, so a red run is also the run that leaks its scratch directories. Deletion is best-effort for the reason
/// <see cref="ShellHarness"/>'s is: a file still held open by the code under test must not turn a passing test
/// into a failing teardown.
/// </remarks>
internal sealed class ScratchDir : IDisposable
{
    /// <summary>The created directory's full path.</summary>
    public string Path { get; }

    /// <param name="prefix">Names the directory, so a leaked one says which fixture made it.</param>
    public ScratchDir(string prefix)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>A path INSIDE this directory. The file need not exist.</summary>
    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { /* best-effort temp cleanup */ }
    }
}
