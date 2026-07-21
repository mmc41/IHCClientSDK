using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Session;

namespace ihc_openvisual.Services;

/// <summary>A no-op <see cref="IDialogService"/> for the XAML designer and the parameterless design-time
/// view-model. Never shows UI; treats every save prompt as "discard" so design-time flows never block.</summary>
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
    public Task<PropertiesResult?> EditPropertiesAsync(string title, string name, string note) => Task.FromResult<PropertiesResult?>(null);
    public Task<ProductPropertiesResult?> EditProductPropertiesAsync(ProductPropertiesInput input) => Task.FromResult<ProductPropertiesResult?>(null);
    public Task<SceneContainerResult?> EditSceneContainerAsync(SceneContainerInput input) => Task.FromResult<SceneContainerResult?>(null);
    public Task<PinPropertiesResult?> EditPinPropertiesAsync(PinPropertiesInput input) => Task.FromResult<PinPropertiesResult?>(null);
    public Task<ModemPropertiesResult?> EditModemPropertiesAsync(ModemPropertiesInput input) => Task.FromResult<ModemPropertiesResult?>(null);
    public Task<AdvancedDimmerResult?> EditAdvancedDimmerAsync(AdvancedDimmerInput input) => Task.FromResult<AdvancedDimmerResult?>(null);
    public Task<SceneValueResult?> EditSceneValueAsync(SceneValueInput input) => Task.FromResult<SceneValueResult?>(null);
    public Task<EnumDefinitionResult?> EditEnumDefinitionAsync(EnumDefinitionInput input) => Task.FromResult<EnumDefinitionResult?>(null);
    public Task<ProjectInfoData?> EditProjectInfoAsync(ProjectInfoData current) => Task.FromResult<ProjectInfoData?>(null);
    public Task ShowDataTablesAsync(IDataTablesDialogViewModel viewModel) => Task.CompletedTask;
    public Task ShowModuleMapAsync(ModuleAddressMap map) => Task.CompletedTask;
    public Task<string?> PickCatalogFileAsync() => Task.FromResult<string?>(null);
    public Task<string?> PickCatalogFolderAsync() => Task.FromResult<string?>(null);
}

/// <summary>A no-op <see cref="IThemeService"/> for the designer/design-time view-model.</summary>
public sealed class NullThemeService : IThemeService
{
    public AppTheme Current { get; private set; } = AppTheme.System;
    public void Apply(AppTheme theme) => Current = theme;
}
