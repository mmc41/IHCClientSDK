using System;
using System.IO;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;

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
    public EnumDefinitionInput? LastEnumDefinitionInput { get; private set; }
    public int EditEnumDefinitionCalls { get; private set; }
    public Func<EnumDefinitionInput, EnumDefinitionResult?>? EnumDefinitionResponder { get; set; }
    public ProjectInfoData? ProjectInfoResult { get; set; }
    public ProjectInfoData? LastProjectInfoInput { get; private set; }
    public int EditProjectInfoCalls { get; private set; }
    public Func<ProjectInfoData, ProjectInfoData?>? ProjectInfoResponder { get; set; }

    public Task<SaveChangesResult> ConfirmSaveChangesAsync(string documentName)
    {
        ConfirmSaveCalls++;
        return Task.FromResult(SaveChangesResult);
    }

    public int ConfirmCalls { get; private set; }
    public Task<bool> ConfirmAsync(string title, string message) { ConfirmCalls++; return Task.FromResult(ConfirmResult); }
    public Task ShowMessageAsync(string title, string message) { LastMessage = message; return Task.CompletedTask; }
    public string? SaveBlockPath { get; set; }
    public Task<string?> PickOpenProjectAsync(string? initialDirectory) => Task.FromResult(OpenPath);
    public Task<string?> PickSaveProjectAsync(string? initialDirectory, string suggestedFileName) => Task.FromResult(SavePath);
    public Task<string?> PickSaveFunctionBlockAsync(string suggestedFileName) => Task.FromResult(SaveBlockPath);
    public string? CatalogFilePath { get; set; }
    public string? CatalogFolderPath { get; set; }
    public Task<string?> PickCatalogFileAsync() => Task.FromResult(CatalogFilePath);
    public Task<string?> PickCatalogFolderAsync() => Task.FromResult(CatalogFolderPath);
    public Task ShowAboutAsync() => Task.CompletedTask;
    public Task ShowSettingsAsync(string settingsText) => Task.CompletedTask;
    public string? LastOpenedUrl { get; private set; }
    public Task OpenExternalUrlAsync(string url) { LastOpenedUrl = url; return Task.CompletedTask; }

    public Task<PropertiesResult?> EditPropertiesAsync(string title, string name, string note)
    {
        EditPropertiesCalls++;
        LastPropertiesTitle = title;
        LastPropertiesName = name;
        LastPropertiesNote = note;
        return Task.FromResult(PropertiesResult);
    }

    public Task<ProductPropertiesResult?> EditProductPropertiesAsync(ProductPropertiesInput input)
    {
        EditProductPropertiesCalls++;
        LastProductPropertiesInput = input;
        return Task.FromResult(ProductPropertiesResponder is not null ? ProductPropertiesResponder(input) : ProductPropertiesResult);
    }

    public Task<SceneContainerResult?> EditSceneContainerAsync(SceneContainerInput input)
    {
        EditSceneContainerCalls++;
        LastSceneContainerInput = input;
        return Task.FromResult(SceneContainerResult);
    }

    public Task<PinPropertiesResult?> EditPinPropertiesAsync(PinPropertiesInput input)
    {
        EditPinPropertiesCalls++;
        LastPinPropertiesInput = input;
        return Task.FromResult(PinPropertiesResult);
    }

    public Task<ModemPropertiesResult?> EditModemPropertiesAsync(ModemPropertiesInput input)
    {
        EditModemPropertiesCalls++;
        LastModemPropertiesInput = input;
        return Task.FromResult(ModemPropertiesResponder is not null ? ModemPropertiesResponder(input) : ModemPropertiesResult);
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

    public Task<ProjectInfoData?> EditProjectInfoAsync(ProjectInfoData current)
    {
        EditProjectInfoCalls++;
        LastProjectInfoInput = current;
        return Task.FromResult(ProjectInfoResponder is not null ? ProjectInfoResponder(current) : ProjectInfoResult);
    }

    public ihc_openvisual.ViewModels.DataTablesViewModel? LastDataTablesViewModel { get; private set; }
    public int ShowDataTablesCalls { get; private set; }
    public Task ShowDataTablesAsync(ihc_openvisual.ViewModels.DataTablesViewModel viewModel)
    {
        ShowDataTablesCalls++;
        LastDataTablesViewModel = viewModel;
        return Task.CompletedTask;
    }

    public ModuleAddressMap? LastModuleMap { get; private set; }
    public int ShowModuleMapCalls { get; private set; }
    public Task ShowModuleMapAsync(ModuleAddressMap map)
    {
        ShowModuleMapCalls++;
        LastModuleMap = map;
        return Task.CompletedTask;
    }
}

/// <summary>Builds file-only <see cref="ProjectSession"/>/<see cref="MainWindowViewModel"/> instances over a
/// throwaway temp directory, with a fake dialog service and no controller — the whole shell is exercised without
/// a network, controller or IHC install.</summary>
public sealed class ShellHarness : IDisposable
{
    public string TempDir { get; }
    public FakeDialogService Dialogs { get; } = new();
    public BackupService Backup { get; }
    public RecentProjectsStore Recent { get; }
    public ProjectSession Session { get; }

    private readonly bool _ownsDir;

    private ShellHarness(string dir, bool ownsDir, int changeThreshold)
    {
        TempDir = dir;
        _ownsDir = ownsDir;
        Directory.CreateDirectory(TempDir);
        Backup = new BackupService(Path.Combine(TempDir, "recovery"));
        Recent = new RecentProjectsStore(Path.Combine(TempDir, "recent.json"));
        var service = new ProjectAppService(new IhcSettings());
        // A one-hour timer never fires during a test; backup triggers are driven explicitly via MarkChangedAsync.
        // The catalog dir is a subfolder of TempDir so Restart(dir) reuses the same persisted catalog (US-061).
        Session = new ProjectSession(service, Backup, Recent, Dialogs, null, TimeSpan.FromHours(1), changeThreshold,
            Path.Combine(TempDir, "catalog"));
    }

    public static ShellHarness Create(int changeThreshold = 10) =>
        new(Path.Combine(Path.GetTempPath(), "ihc_ov_tests", Guid.NewGuid().ToString("N")), ownsDir: true, changeThreshold);

    /// <summary>A second session over an existing directory — simulates restarting the app after a crash so the
    /// recovery backup left in <paramref name="dir"/> is discovered.</summary>
    public static ShellHarness Restart(string dir, int changeThreshold = 10) =>
        new(dir, ownsDir: false, changeThreshold);

    public string TempPath(string fileName) => Path.Combine(TempDir, fileName);

    public MainWindowViewModel CreateViewModel() =>
        new(Session, Dialogs, Recent, new NullThemeService());

    public void Dispose()
    {
        Session.Dispose();
        if (_ownsDir)
        {
            try { Directory.Delete(TempDir, recursive: true); } catch { /* best-effort temp cleanup */ }
        }
    }
}
