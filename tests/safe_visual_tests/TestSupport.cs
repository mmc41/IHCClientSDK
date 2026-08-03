using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
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
    public ProductPropertiesResult? ProductPropertiesResult { get; set; }
    public ProductPropertiesInput? LastProductPropertiesInput { get; private set; }
    public int EditProductPropertiesCalls { get; private set; }
    public Func<ProductPropertiesInput, ProductPropertiesResult?>? ProductPropertiesResponder { get; set; }
    public SceneContainerResult? SceneContainerResult { get; set; }
    public SceneContainerInput? LastSceneContainerInput { get; private set; }
    public int EditSceneContainerCalls { get; private set; }
    public PinPropertiesResult? PinPropertiesResult { get; set; }
    public PinPropertiesInput? LastPinPropertiesInput { get; private set; }
    public int EditPinPropertiesCalls { get; private set; }
    public ModemPropertiesResult? ModemPropertiesResult { get; set; }
    public ModemPropertiesInput? LastModemPropertiesInput { get; private set; }
    public int EditModemPropertiesCalls { get; private set; }
    public Func<ModemPropertiesInput, ModemPropertiesResult?>? ModemPropertiesResponder { get; set; }
    public AdvancedDimmerResult? AdvancedDimmerResult { get; set; }
    public AdvancedDimmerInput? LastAdvancedDimmerInput { get; private set; }
    public int EditAdvancedDimmerCalls { get; private set; }
    public SceneValueResult? SceneValueResult { get; set; }
    public SceneValueInput? LastSceneValueInput { get; private set; }
    public int EditSceneValueCalls { get; private set; }
    public EnumDefinitionResult? EnumDefinitionResult { get; set; }
    public EnumTypeManagerResult? EnumTypeManagerResult { get; set; }
    public EnumTypeManagerInput? LastEnumTypeManagerInput { get; private set; }
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
    public Task ShowMessageAsync(string title, string message) { LastMessage = message; return Task.CompletedTask; }
    public string? SaveBlockPath { get; set; }
    public Task<string?> PickOpenProjectAsync(string? initialDirectory) => Task.FromResult(OpenPath);
    public Task<string?> PickSaveProjectAsync(string? initialDirectory, string suggestedFileName) => Task.FromResult(SavePath);
    public Task<string?> PickSaveFunctionBlockAsync(string suggestedFileName) => Task.FromResult(SaveBlockPath);
    public string? SaveReportPath { get; set; }
    public string? LastReportSuggestedName { get; private set; }
    public string? LastReportMimeType { get; private set; }
    public Task<string?> PickSaveReportAsync(string suggestedFileName, string mimeType)
    {
        LastReportSuggestedName = suggestedFileName;
        LastReportMimeType = mimeType;
        return Task.FromResult(SaveReportPath);
    }
    public string? CatalogFilePath { get; set; }
    public string? CatalogFolderPath { get; set; }
    public Task<string?> PickCatalogFileAsync() => Task.FromResult(CatalogFilePath);
    public Task<string?> PickCatalogFolderAsync() => Task.FromResult(CatalogFolderPath);
    public Task ShowAboutAsync() => Task.CompletedTask;
    public Task ShowSettingsAsync(string settingsText) => Task.CompletedTask;
    public string? LastOpenedUrl { get; private set; }
    public Task OpenExternalUrlAsync(string url) { LastOpenedUrl = url; return Task.CompletedTask; }

    public LibraryOrigin? LastPropertiesOrigin { get; private set; }
    public string? LastPropertiesAffirmative { get; private set; }

    public Task<PropertiesResult?> EditPropertiesAsync(string title, string name, string note, LibraryOrigin? origin = null,
        string affirmative = "OK")
    {
        EditPropertiesCalls++;
        LastPropertiesTitle = title;
        LastPropertiesName = name;
        LastPropertiesNote = note;
        LastPropertiesOrigin = origin;
        LastPropertiesAffirmative = affirmative;
        return Task.FromResult(PropertiesResult);
    }

    public Task<VariablePropertiesResult?> EditVariablePropertiesAsync(VariablePropertiesInput input)
    {
        EditVariablePropertiesCalls++;
        LastVariablePropertiesInput = input;
        return Task.FromResult(VariablePropertiesResult);
    }

    /// <summary>Makes the product dialog answer Cancel. Needed explicitly because the DEFAULT is now OK-without-edits:
    /// the dialog opens as part of placing a product (uxparity S-12), so "no result configured" has to mean the
    /// ordinary path — a test that wanted a product would otherwise silently get none.</summary>
    public bool CancelProductProperties { get; set; }

    public Task<ProductPropertiesResult?> EditProductPropertiesAsync(ProductPropertiesInput input)
    {
        EditProductPropertiesCalls++;
        LastProductPropertiesInput = input;
        if (CancelProductProperties)
            return Task.FromResult<ProductPropertiesResult?>(null);
        if (ProductPropertiesResponder is not null)
            return Task.FromResult(ProductPropertiesResponder(input));
        return Task.FromResult(ProductPropertiesResult ?? EchoUnchanged(input));
    }

    // "OK without editing anything": every field handed straight back, so an insert keeps the catalog defaults.
    private static ProductPropertiesResult? EchoUnchanged(ProductPropertiesInput i) =>
        new(i.Name, i.CurrentLocalityId, i.Note, i.CableType, i.CableNumber, i.IdentificationCode, i.LightGroup,
            OpenAdvanced: false, ConfigureTerminalPinId: null, Position: i.Position, EndUserReport: i.EndUserReport);

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

    /// <summary>Makes the modem dialog answer Cancel — same reason as <see cref="CancelProductProperties"/>: the
    /// dialog now opens as part of placing a modem, so the default has to be the ordinary OK path.</summary>
    public bool CancelModemProperties { get; set; }

    public Task<ModemPropertiesResult?> EditModemPropertiesAsync(ModemPropertiesInput input)
    {
        EditModemPropertiesCalls++;
        LastModemPropertiesInput = input;
        if (CancelModemProperties)
            return Task.FromResult<ModemPropertiesResult?>(null);
        if (ModemPropertiesResponder is not null)
            return Task.FromResult(ModemPropertiesResponder(input));
        return Task.FromResult<ModemPropertiesResult?>(ModemPropertiesResult ?? new ModemPropertiesResult(
            input.Name, input.CurrentLocalityId, input.Note, input.IdentificationCode,
            input.Cable0V, input.Cable24V, input.CableRS485Minus, input.CableRS485Plus,
            input.PinCode, input.PhoneNumbers));
    }

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

    public Task<EnumTypeManagerResult?> ManageEnumTypesAsync(EnumTypeManagerInput input)
    {
        LastEnumTypeManagerInput = input;
        return Task.FromResult(EnumTypeManagerResult);
    }

    public Task<ProjectInfoData?> EditProjectInfoAsync(ProjectInfoData current)
    {
        EditProjectInfoCalls++;
        LastProjectInfoInput = current;
        return Task.FromResult(ProjectInfoResponder is not null ? ProjectInfoResponder(current) : ProjectInfoResult);
    }

    public ihc_openvisual.ViewModels.DataTablesViewModel? LastDataTablesViewModel { get; private set; }
    public int ShowDataTablesCalls { get; private set; }
    public Task ShowDataTablesAsync(IDataTablesDialogViewModel viewModel)
    {
        ShowDataTablesCalls++;
        LastDataTablesViewModel = viewModel as ihc_openvisual.ViewModels.DataTablesViewModel;   // tests use the concrete VM (T020 seam)
        return Task.CompletedTask;
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
    public BackupService Backup { get; }
    public RecentProjectsStore Recent { get; }
    public ProjectWorkflow Session { get; }

    /// <summary>The same <see cref="ProjectAppService"/> instance <see cref="Session"/> runs on — the SDK facade
    /// tests read the rich catalog (<c>GetAvailableProducts</c>/<c>GetAvailableFunctionBlocks</c>) and other SDK
    /// operations from directly, without going through the GUI workflow's thin delegators (refac3 T007).</summary>
    public ProjectAppService ProjectService { get; }

    private readonly bool _ownsDir;

    private ShellHarness(string dir, bool ownsDir, int changeThreshold,
        System.TimeProvider? timeProvider, TimeSpan? autoBackupInterval)
    {
        TempDir = dir;
        _ownsDir = ownsDir;
        Directory.CreateDirectory(TempDir);
        Backup = new BackupService(Path.Combine(TempDir, "recovery"));
        Recent = new RecentProjectsStore(Path.Combine(TempDir, "recent.json"));
        // When a clock is injected (a FakeTimeProvider), the facade uses it too — so report generation timestamps
        // (T022) are deterministic; otherwise the default file-only service on the system clock (kept lazy).
        ProjectService = timeProvider is null
            ? new ProjectAppService(new IhcSettings())
            : new ProjectAppService(new IhcSettings(), new Ihc.Vis.Catalog.BuiltInCatalog(), timeProvider);
        // By default a one-hour timer never fires during a test; a FakeTimeProvider (passed in) drives it
        // deterministically. The catalog dir is a subfolder of TempDir so Restart(dir) reuses it (US-061).
        Session = new ProjectWorkflow(ProjectService, Backup, Recent, Dialogs, null,
            autoBackupInterval ?? TimeSpan.FromHours(1), changeThreshold, Path.Combine(TempDir, "catalog"), timeProvider);
    }

    public static ShellHarness Create(int changeThreshold = 10,
        System.TimeProvider? timeProvider = null, TimeSpan? autoBackupInterval = null) =>
        new(Path.Combine(Path.GetTempPath(), "ihc_ov_tests", Guid.NewGuid().ToString("N")), ownsDir: true,
            changeThreshold, timeProvider, autoBackupInterval);

    /// <summary>A second session over an existing directory — simulates restarting the app after a crash so the
    /// recovery backup left in <paramref name="dir"/> is discovered.</summary>
    public static ShellHarness Restart(string dir, int changeThreshold = 10) =>
        new(dir, ownsDir: false, changeThreshold, null, null);

    public string TempPath(string fileName) => Path.Combine(TempDir, fileName);

    /// <summary>The shell view-model over this harness. Pass <paramref name="loggerFactory"/> (a
    /// <see cref="CapturingLoggerFactory"/>) when the test needs to prove a failure reached the logging pipeline,
    /// and <paramref name="theme"/> (the real <c>ThemeService</c>) when it needs the appearance choices to reach
    /// the running application's resources rather than being recorded inertly.</summary>
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
