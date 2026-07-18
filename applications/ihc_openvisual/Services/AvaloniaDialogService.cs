using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using ihc_openvisual.Configuration;
using ihc_openvisual.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ihc_openvisual.Services;

/// <summary>Avalonia implementation of <see cref="IDialogService"/>: modal confirm/message dialogs, native file
/// pickers via the owner window's <see cref="IStorageProvider"/>, and the About/settings windows.</summary>
public sealed class AvaloniaDialogService : IDialogService
{
    private readonly ILogger<AvaloniaDialogService> _logger;

    public AvaloniaDialogService(ILoggerFactory? loggerFactory = null) =>
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AvaloniaDialogService>();

    /// <summary>The main window, used as the modal owner and storage-provider source. Set after it is created.</summary>
    public Window? Owner { get; set; }

    private static readonly FilePickerFileType VisFileType = new("IHC project (*.vis)") { Patterns = new[] { "*.vis" } };
    private static readonly FilePickerFileType IfbFileType = new("IHC function block (*.ifb)") { Patterns = new[] { "*.ifb" } };
    private static readonly FilePickerFileType CatalogFileType =
        new("IHC catalog definition (*.def, *.ifb)") { Patterns = new[] { "*.def", "*.ifb" } };

    public async Task<SaveChangesResult> ConfirmSaveChangesAsync(string documentName)
    {
        var result = await ShowButtonsAsync(
            "Save changes?",
            $"Save changes to {documentName} before continuing?",
            ("Save", SaveChangesResult.Save),
            ("Don't save", SaveChangesResult.Discard),
            ("Cancel", SaveChangesResult.Cancel));
        return result;
    }

    public Task<bool> ConfirmAsync(string title, string message) =>
        ShowButtonsAsync(title, message, ("Yes", true), ("No", false));

    public Task ShowMessageAsync(string title, string message) =>
        ShowButtonsAsync(title, message, ("OK", true));

    public async Task<string?> PickOpenProjectAsync(string? initialDirectory)
    {
        if (Owner is null)
            return null;
        IReadOnlyList<IStorageFile> files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open project",
            AllowMultiple = false,
            FileTypeFilter = new[] { VisFileType },
            SuggestedStartLocation = await GetFolderAsync(initialDirectory)
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveProjectAsync(string? initialDirectory, string suggestedFileName)
    {
        if (Owner is null)
            return null;
        IStorageFile? file = await Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save project as",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "vis",
            FileTypeChoices = new[] { VisFileType },
            SuggestedStartLocation = await GetFolderAsync(initialDirectory)
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickSaveFunctionBlockAsync(string suggestedFileName)
    {
        if (Owner is null)
            return null;
        IStorageFile? file = await Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save function block",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "ifb",
            FileTypeChoices = new[] { IfbFileType }
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickCatalogFileAsync()
    {
        if (Owner is null)
            return null;
        IReadOnlyList<IStorageFile> files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import catalog file",
            AllowMultiple = false,
            FileTypeFilter = new[] { CatalogFileType }
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickCatalogFolderAsync()
    {
        if (Owner is null)
            return null;
        IReadOnlyList<IStorageFolder> folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Import catalog folder",
            AllowMultiple = false
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task ShowAboutAsync()
    {
        if (Owner is null)
            return;
        await new AboutWindow().ShowDialog(Owner);
    }

    public Task ShowSettingsAsync(string settingsText) =>
        ShowButtonsAsync("Effective settings", settingsText, selectable: true, ("Close", true));

    public async Task<PropertiesResult?> EditPropertiesAsync(string title, string name, string note)
    {
        if (Owner is null)
            return null;
        return await PropertiesWindow.ShowAsync(Owner, title, name, note);
    }

    public async Task<ProductPropertiesResult?> EditProductPropertiesAsync(ProductPropertiesInput input)
    {
        if (Owner is null)
            return null;
        return await ProductPropertiesWindow.ShowAsync(Owner, input);
    }

    public async Task<SceneContainerResult?> EditSceneContainerAsync(SceneContainerInput input)
    {
        if (Owner is null)
            return null;
        return await SceneContainerWindow.ShowAsync(Owner, input);
    }

    public async Task<PinPropertiesResult?> EditPinPropertiesAsync(PinPropertiesInput input)
    {
        if (Owner is null)
            return null;
        return await PinPropertiesWindow.ShowAsync(Owner, input);
    }

    public async Task<ModemPropertiesResult?> EditModemPropertiesAsync(ModemPropertiesInput input)
    {
        if (Owner is null)
            return null;
        return await ModemPropertiesWindow.ShowAsync(Owner, input);
    }

    public async Task<AdvancedDimmerResult?> EditAdvancedDimmerAsync(AdvancedDimmerInput input)
    {
        if (Owner is null)
            return null;
        return await AdvancedDimmerWindow.ShowAsync(Owner, input);
    }

    public async Task<SceneValueResult?> EditSceneValueAsync(SceneValueInput input)
    {
        if (Owner is null)
            return null;
        return await SceneValueWindow.ShowAsync(Owner, input);
    }

    public async Task<EnumDefinitionResult?> EditEnumDefinitionAsync(EnumDefinitionInput input)
    {
        if (Owner is null)
            return null;
        return await EnumDefinitionWindow.ShowAsync(Owner, input);
    }

    public async Task<ProjectInfoData?> EditProjectInfoAsync(ProjectInfoData current)
    {
        if (Owner is null)
            return null;
        return await ProjectInfoWindow.ShowAsync(Owner, current);
    }

    public async Task ShowDataTablesAsync(ihc_openvisual.ViewModels.DataTablesViewModel viewModel)
    {
        if (Owner is null)
            return;
        await DataTablesWindow.ShowAsync(Owner, viewModel);
    }

    public async Task ShowModuleMapAsync(ModuleAddressMap map)
    {
        if (Owner is null)
            return;
        await ModuleMapWindow.ShowAsync(Owner, map);
    }

    public Task OpenExternalUrlAsync(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open URL {Url}", url);
        }
        return Task.CompletedTask;
    }

    private async Task<IStorageFolder?> GetFolderAsync(string? directory)
    {
        if (Owner is null || string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;
        return await Owner.StorageProvider.TryGetFolderFromPathAsync(directory);
    }

    private Task<T> ShowButtonsAsync<T>(string title, string message, params (string Label, T Value)[] buttons) =>
        ShowButtonsAsync(title, message, selectable: false, buttons);

    private Task<T> ShowButtonsAsync<T>(string title, string message, bool selectable, params (string Label, T Value)[] buttons)
    {
        var tcs = new TaskCompletionSource<T>();

        var content = selectable
            ? (Control)new SelectableTextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }
            : new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        // Expose the message as the content's accessible name so screen readers announce it (this dialog is
        // built in code and has no XAML LabeledBy plumbing). The buttons carry their own text content.
        Avalonia.Automation.AutomationProperties.SetName(content, message);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 16, 0, 0)
        };

        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 360,
            MaxWidth = 640,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };

        T defaultValue = buttons.Length > 0 ? buttons[^1].Value : default!;
        Button? safeButton = null;
        foreach ((string label, T value) in buttons)
        {
            var button = new Button { Content = label, MinWidth = 84 };
            button.Click += (_, _) =>
            {
                tcs.TrySetResult(value);
                dialog.Close();
            };
            buttonPanel.Children.Add(button);
            safeButton = button;   // the last button is the safe (negative) default
        }

        // Keyboard operability (A-9/A-10): the safe (last) button holds focus on open, and Escape dismisses the
        // dialog — resolving, via the Closed handler below, to that same safe default.
        WireKeyboardDismissal(dialog, safeButton);

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Children =
            {
                content,
                buttonPanel
            }
        };

        // If the window is closed via the title bar, resolve to the last (safest) option.
        dialog.Closed += (_, _) => tcs.TrySetResult(defaultValue);

        if (Owner is not null)
            _ = dialog.ShowDialog(Owner);
        else
            dialog.Show();

        return tcs.Task;
    }

    /// <summary>Makes a code-built dialog keyboard-operable (A-9/A-10): <paramref name="focusOnOpen"/> (the safe,
    /// default control) takes focus when the dialog opens, and Escape closes it — the dialog's <c>Closed</c> handler
    /// then resolves it to its safe default. Public so a headless test can verify the wiring on a real window.</summary>
    public static void WireKeyboardDismissal(Window dialog, Control? focusOnOpen)
    {
        dialog.Opened += (_, _) => focusOnOpen?.Focus();
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
                dialog.Close();
        };
    }
}
