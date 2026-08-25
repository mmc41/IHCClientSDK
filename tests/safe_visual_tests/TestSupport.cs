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
    public AdvancedDimmerResult? AdvancedDimmerResult { get; set; }
    public AdvancedDimmerInput? LastAdvancedDimmerInput { get; private set; }
    public int EditAdvancedDimmerCalls { get; private set; }
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
    public Task ShowSettingsAsync(string settingsText) => Task.CompletedTask;
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
        string affirmative = "OK", string? userGroupCaption = null, bool? conditionsOr = null)
    {
        EditPropertiesCalls++;
        LastPropertiesTitle = title;
        LastPropertiesName = name;
        LastPropertiesNote = note;
        LastPropertiesOrigin = origin;
        LastPropertiesAffirmative = affirmative;
        LastPropertiesUserGroup = userGroupCaption;
        LastPropertiesConditionsOr = conditionsOr;
        return Task.FromResult(PropertiesResult);
    }

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

    public Task<PinPropertiesResult?> EditPinPropertiesAsync(PinPropertiesInput input, System.Func<PinPropertiesResult, Task>? onApply = null)
    {
        EditPinPropertiesCalls++;
        LastPinPropertiesInput = input;
        return Task.FromResult(PinPropertiesResult);
    }

    /// <summary>Makes the generic product dialog answer Cancel. Needed explicitly because the DEFAULT is
    /// OK-without-edits: the dialog opens as part of PLACING a product (uxparity S-12), so "no result configured"
    /// has to mean the ordinary path — a test that wanted a product would otherwise silently get none.</summary>
    public bool CancelProductDialog { get; set; }

    /// <summary>The terminal rows handed to the last dialog — what the grids would have shown.</summary>
    public IReadOnlyList<ProductTerminal>? LastProductDialogTerminals { get; private set; }

    /// <summary>The settings rows handed to the last dialog — what the Indstillinger grid would have shown.</summary>
    public IReadOnlyList<ProductSetting>? LastProductDialogSettings { get; private set; }

    public Task<ProductDialogEdits?> EditProductDialogAsync(
        ProductDialogDescriptor descriptor, IReadOnlyList<ProductTerminal>? terminals = null,
        IReadOnlyList<ProductSetting>? settings = null)
    {
        EditProductDialogCalls++;
        LastProductDialog = descriptor;
        LastProductDialogTerminals = terminals;
        LastProductDialogSettings = settings;
        if (CancelProductDialog)
            return Task.FromResult<ProductDialogEdits?>(null);
        if (ProductDialogResponder is not null)
            return Task.FromResult(ProductDialogResponder(descriptor));
        // The default is OK with NOTHING changed — the untouched-OK commit. Echoing every field back as an "edit"
        // would make every test that opens this dialog also a test of writing every attribute.
        return Task.FromResult<ProductDialogEdits?>(new ProductDialogEdits([]));
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

    /// <summary>Answers OK and steps into a hand-written composite, as clicking <i>Avanceret</i> or a terminal row does.</summary>
    public void RespondWithWidget(DialogWidgetKind kind, ElementId? target = null) =>
        ProductDialogResponder = _ => new ProductDialogEdits([], new ProductDialogWidgetAction(kind, target));

    public Task<AdvancedDimmerResult?> EditAdvancedDimmerAsync(AdvancedDimmerInput input)
    {
        EditAdvancedDimmerCalls++;
        LastAdvancedDimmerInput = input;
        return Task.FromResult(AdvancedDimmerResult);
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

    private readonly bool _ownsDir;

    private ShellHarness(string dir, bool ownsDir, System.TimeProvider? timeProvider)
    {
        TempDir = dir;
        _ownsDir = ownsDir;
        Directory.CreateDirectory(TempDir);
        Recent = new RecentProjectsStore(Path.Combine(TempDir, "recent.json"));
        // When a clock is injected (a FakeTimeProvider), the facade uses it too — so report generation timestamps
        // (T022) are deterministic; otherwise the default file-only service on the system clock (kept lazy).
        ProjectService = timeProvider is null
            ? new ProjectAppService(new IhcSettings())
            : new ProjectAppService(new IhcSettings(), new Ihc.Vis.Catalog.BuiltInCatalog(), timeProvider);
        // The catalog dir is a subfolder of TempDir so Restart(dir) reuses it (US-061).
        // The marshal is SYNCHRONOUS here and the clock is whatever the test injected: the workflow's validation
        // monitor uses both, and a test that could not advance the debounce would hang rather than fail.
        Session = new ProjectWorkflow(
            ProjectService, Recent, Dialogs, null, Path.Combine(TempDir, "catalog"),
            post: action => action(), timeProvider: timeProvider);
    }

    public static ShellHarness Create(System.TimeProvider? timeProvider = null) =>
        new(Path.Combine(Path.GetTempPath(), "ihc_ov_tests", Guid.NewGuid().ToString("N")), ownsDir: true,
            timeProvider);

    /// <summary>A second session over an existing directory — simulates restarting the app, so the per-user state
    /// left in <paramref name="dir"/> (e.g. persisted catalog imports) is picked up again.</summary>
    public static ShellHarness Restart(string dir) => new(dir, ownsDir: false, null);

    public string TempPath(string fileName) => Path.Combine(TempDir, fileName);

    /// <summary>The per-test LIBRARY folder — the same one the session was constructed with, so a test can assert
    /// where "save to the library" put the block without reaching the real %APPDATA% catalog.</summary>
    public string CatalogDir => Path.Combine(TempDir, "catalog");

    /// <summary>The shell view-model over this harness. Pass <paramref name="loggerFactory"/> (a
    /// <see cref="CapturingLoggerFactory"/>) when the test needs to prove a failure reached the logging pipeline,
    /// and <paramref name="theme"/> (the real <c>ThemeService</c>) when it needs the appearance choices to reach
    /// the running application's resources rather than being recorded inertly.</summary>
    /// <para>The marshal and the clock the Problemer panel runs on come from <see cref="Session"/>, so a test
    /// that needs a controllable debounce passes its clock to <see cref="Create"/> rather than here.</para>
    public MainWindowViewModel CreateViewModel(ILoggerFactory? loggerFactory = null, IThemeService? theme = null) =>
        new(Session, Dialogs, Recent, theme ?? new NullThemeService(), null, loggerFactory);

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
        MainWindowViewModel vm = CreateViewModel();
        await vm.InitializeAsync();
        ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await Session.AddEmptyFunctionBlockAsync(locality);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        return vm;
    }

    public void Dispose()
    {
        Session.Dispose();
        if (_ownsDir)
        {
            try { Directory.Delete(TempDir, recursive: true); } catch { /* best-effort temp cleanup */ }
        }
    }
}
