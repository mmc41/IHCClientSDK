using System.Threading.Tasks;

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
    public Task ShowAboutAsync() => Task.CompletedTask;
    public Task ShowSettingsAsync(string settingsText) => Task.CompletedTask;
    public Task OpenExternalUrlAsync(string url) => Task.CompletedTask;
}

/// <summary>A no-op <see cref="IThemeService"/> for the designer/design-time view-model.</summary>
public sealed class NullThemeService : IThemeService
{
    public AppTheme Current { get; private set; } = AppTheme.System;
    public void Apply(AppTheme theme) => Current = theme;
}
