using System;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Session;

namespace ihc_openvisual.Services;

/// <summary>An inert <see cref="IDialogService"/>: never shows UI, and treats every save prompt as "discard" so a
/// non-interactive flow never blocks. Retained as the null-object seam for a host that has no dialog surface (the
/// headless suites use <c>FakeDialogService</c>, which additionally records calls and can answer them).</summary>
public sealed class NullDialogService : IDialogService
{
    public Task<SaveChangesResult> ConfirmSaveChangesAsync(string documentName) => Task.FromResult(SaveChangesResult.Discard);
    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);
    public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
    public Task<string?> PickOpenProjectAsync(string? initialDirectory) => Task.FromResult<string?>(null);
    public Task<string?> PickSaveProjectAsync(string? initialDirectory, string suggestedFileName) => Task.FromResult<string?>(null);
    public Task<string?> PickSaveFunctionBlockAsync(string suggestedFileName) => Task.FromResult<string?>(null);
    public Task ShowAboutAsync() => Task.CompletedTask;
    public Task ShowSettingsAsync(string settingsText) => Task.CompletedTask;
    public Task OpenExternalUrlAsync(string url) => Task.CompletedTask;
    public Task<PropertiesResult?> EditPropertiesAsync(string title, string name, string note, LibraryOrigin? origin = null, string affirmative = "OK") => Task.FromResult<PropertiesResult?>(null);
    public Task<VariablePropertiesResult?> EditVariablePropertiesAsync(VariablePropertiesInput input) => Task.FromResult<VariablePropertiesResult?>(null);
    public Task<ProductPropertiesResult?> EditProductPropertiesAsync(ProductPropertiesInput input) => Task.FromResult<ProductPropertiesResult?>(null);
    public Task<SceneContainerResult?> EditSceneContainerAsync(SceneContainerInput input) => Task.FromResult<SceneContainerResult?>(null);
    public Task<PinPropertiesResult?> EditPinPropertiesAsync(PinPropertiesInput input, Func<PinPropertiesResult, Task>? onApply = null) => Task.FromResult<PinPropertiesResult?>(null);
    public Task<ModemPropertiesResult?> EditModemPropertiesAsync(ModemPropertiesInput input) => Task.FromResult<ModemPropertiesResult?>(null);
    public Task<AdvancedDimmerResult?> EditAdvancedDimmerAsync(AdvancedDimmerInput input) => Task.FromResult<AdvancedDimmerResult?>(null);
    public Task<SceneValueResult?> EditSceneValueAsync(SceneValueInput input) => Task.FromResult<SceneValueResult?>(null);
    public Task<EnumDefinitionResult?> EditEnumDefinitionAsync(EnumDefinitionInput input) => Task.FromResult<EnumDefinitionResult?>(null);
    public Task<EnumTypeManagerResult?> ManageEnumTypesAsync(EnumTypeManagerInput input) => Task.FromResult<EnumTypeManagerResult?>(null);
    public Task<ProjectInfoData?> EditProjectInfoAsync(ProjectInfoData current) => Task.FromResult<ProjectInfoData?>(null);
    public Task ShowDataTablesAsync(IDataTablesDialogViewModel viewModel) => Task.CompletedTask;
    public Task ShowReportPickerAsync(IReportPickerViewModel viewModel) => Task.CompletedTask;
    public Task<string?> PickSaveReportAsync(string suggestedFileName, string mimeType) => Task.FromResult<string?>(null);
    public Task ShowModuleMapAsync(DatalineModuleMap map) => Task.CompletedTask;
    public Task<string?> PickCatalogFileAsync() => Task.FromResult<string?>(null);
    public Task<string?> PickCatalogFolderAsync() => Task.FromResult<string?>(null);
}

/// <summary>An inert <see cref="IThemeService"/> that records the choices without touching Avalonia — the theme
/// port's null object, used by the headless test harness.</summary>
public sealed class NullThemeService : IThemeService
{
    public AppTheme Current { get; private set; } = AppTheme.System;
    public TextScale TextScale { get; private set; } = TextScale.Normal;
    public bool IsHighContrast { get; private set; }
    public void Apply(AppTheme theme) => Current = theme;
    public void ApplyTextScale(TextScale scale) => TextScale = scale;
    public void ApplyContrast(bool isHighContrast) => IsHighContrast = isHighContrast;
}
