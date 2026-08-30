using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Ihc.Vis;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Session;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using ihc_openvisual.Configuration;
using ihc_openvisual.ViewModels;
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

    /// <summary>
    /// The window a modal opened NOW should parent on: the innermost one still open, not the shell.
    /// <para>Once a dialog can be opened from inside another, "the owner" stops being the main window. A
    /// sub-dialog parented on the shell is not modal to the dialog that raised it, so the installer can reach
    /// behind it and edit the very values it was opened to change.</para>
    /// </summary>
    /// <remarks>
    /// READ from Avalonia's own ownership chain rather than maintained as a parallel stack of our own. A
    /// hand-kept stack has to be popped on every exit route a dialog has — OK, Cancel, Esc, the title-bar X, an
    /// exception on the way out — and a single missed pop leaves every later modal parented on a window that has
    /// closed. The chain below cannot drift, because it is the same fact the window manager is already keeping.
    /// </remarks>
    internal static Window Innermost(Window shell)
    {
        Window owner = shell;
        // Bounded: a cycle is impossible in a window-ownership tree, but a bound turns a hypothetical hang into
        // an ordinary wrong answer.
        for (int depth = 0; depth < 16; depth++)
        {
            if (owner.OwnedWindows.FirstOrDefault(child => child.IsVisible) is not { } nested)
            {
                return owner;
            }
            owner = nested;
        }
        return owner;
    }

    /// <summary>The one "there is no owner window yet" guard every modal shares — a headless or design-time
    /// instance has no <see cref="Owner"/>, and showing a modal without one throws. Having it in a single place
    /// means a newly added dialog inherits the guard instead of having to remember it, and each dialog member
    /// stays the one call that is actually its own.
    /// <para>It also decides WHICH window owns the modal: <see cref="Innermost"/>, so a dialog raised from
    /// inside another stacks on it.</para></summary>
    private Task<T?> WithOwnerAsync<T>(Func<Window, Task<T?>> show) where T : class =>
        Owner is { } owner ? show(Innermost(owner)) : Task.FromResult<T?>(null);

    /// <inheritdoc cref="WithOwnerAsync{T}"/>
    private Task WithOwnerAsync(Func<Window, Task> show) =>
        Owner is { } owner ? show(Innermost(owner)) : Task.CompletedTask;

    private static readonly FilePickerFileType VisFileType = new("IHC projekt (*.vis)") { Patterns = new[] { "*.vis" } };
    private static readonly FilePickerFileType CatalogFileType =
        new("IHC katalogdefinition (*.def, *.ifb)") { Patterns = new[] { "*.def", "*.ifb" } };

    // Danish save-changes guard. Registered difference: the original leaves this MessageBox in ENGLISH
    // ("Save changes to …?" — Yes/No/Cancel), where IHC OpenVisual follows its Danish-everywhere rule. The strings
    // are pinned (SaveChangesGuardIsDanish) so they cannot drift back to the vendor's un-localized wording.
    internal const string SaveChangesTitle = "Gem ændringer?";
    internal const string SaveChangesSaveLabel = "Gem";
    internal const string SaveChangesDiscardLabel = "Gem ikke";
    internal const string SaveChangesCancelLabel = "Annuller";
    internal static string SaveChangesMessage(string documentName) => $"Gem ændringer i {documentName} før du fortsætter?";

    public async Task<SaveChangesResult> ConfirmSaveChangesAsync(string documentName)
    {
        var result = await ShowButtonsAsync(
            SaveChangesTitle,
            SaveChangesMessage(documentName),
            (SaveChangesSaveLabel, SaveChangesResult.Save),
            (SaveChangesDiscardLabel, SaveChangesResult.Discard),
            (SaveChangesCancelLabel, SaveChangesResult.Cancel));
        return result;
    }

    public Task<bool> ConfirmAsync(string title, string message) =>
        ShowButtonsAsync(title, message, ("Ja", true), ("Nej", false));

    public Task ShowMessageAsync(string title, string message) =>
        ShowButtonsAsync(title, message, ("OK", true));

    // The coded doors render through the shell's ONE presentation path and then reuse the same box: identity is
    // decided there, never per dialog, so a problem shown here and one shown in a future findings pane read alike.
    //
    // They are also where problems are COUNTED, and that placement is the point. RaisedProblemDisplay is the
    // tidier-looking home, but call sites reach a dialog without going through it - so counting there would
    // under-report by exactly the paths nobody remembered. These three overloads are what a problem must pass
    // through to become something an installer actually sees.
    public Task ShowProblemAsync(string title, Problem problem)
    {
        CountProblem(problem.Code);
        return ShowMessageAsync(title, ProblemPresenter.Text(problem));
    }

    public Task ShowProblemAsync(string title, ProblemChain chain)
    {
        // The CAUSE, not the operation: the operation names what was being attempted, the cause names what
        // was wrong, and the second is the one worth counting.
        CountProblem(chain.Cause.Code);
        return ShowMessageAsync(title, ProblemPresenter.Text(chain));
    }

    public Task ShowProblemAsync(string title, ProblemAggregate aggregate)
    {
        // The HEAD, once - not once per item. An aggregate is ONE thing shown to the user, and counting its
        // items would make a single dialog about a many-findings validation look like many dialogs.
        CountProblem(aggregate.Head.Code);
        return ShowMessageAsync(title, ProblemPresenter.Text(aggregate));
    }

    /// <summary>Records one problem actually presented, keyed by its code and the family that code belongs to.</summary>
    private static void CountProblem(Ihc.Vis.Problems.ProblemCode code) =>
        AppTelemetryRegistry.ProblemRaised.Add(1,
            new KeyValuePair<string, object?>(AppTelemetryRegistry.Attributes.ProblemCode, code.Value),
            new KeyValuePair<string, object?>(AppTelemetryRegistry.Attributes.ProblemFamily, code.Family.ToString()));

    public async Task<string?> PickOpenProjectAsync(string? initialDirectory)
    {
        if (Owner is null)
            return null;
        IReadOnlyList<IStorageFile> files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Åbn projekt",
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
            Title = "Gem projekt som",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "vis",
            FileTypeChoices = new[] { VisFileType },
            SuggestedStartLocation = await GetFolderAsync(initialDirectory)
        });
        return file?.TryGetLocalPath();
    }

    /// <summary>
    /// How the save dialog describes ONE document this application writes: the Danish title over the dialog, the
    /// Danish entry in its file-type filter, and the extension it defaults to.
    /// </summary>
    /// <remarks>
    /// The three travel together because they are three views of one answer, and splitting them is how they came
    /// apart: the dialog read "Gem rapport" with an "HTML-rapport" filter for every caller, which was true while
    /// reports were the only caller and wrong on both lines for a findings list.
    /// </remarks>
    internal readonly record struct SaveFileDescription(string Title, string FileTypeLabel, string Extension);

    /// <summary>How a generated report is offered. The extension comes from the SDK, which is where the
    /// format↔extension mapping lives, so the name suggested here and the bytes written cannot drift apart.</summary>
    internal static SaveFileDescription DescribeReport(ReportFormat format) => format switch
    {
        ReportFormat.Text => new("Gem rapport", "Tekstrapport", ReportMimeTypes.FileExtensionFor(ReportMimeTypes.PlainText)),
        _ => new("Gem rapport", "HTML-rapport", ReportMimeTypes.FileExtensionFor(ReportMimeTypes.Html)),
    };

    /// <summary>How the Problemer panel's findings list is offered — one row, because it has one format.</summary>
    internal static readonly SaveFileDescription FindingsList =
        new("Gem fejlliste", "XML-fejlliste", FindingExportFormat.FileExtension);

    public Task<string?> PickSaveReportAsync(string suggestedFileName, ReportFormat format) =>
        PickSaveAsync(DescribeReport(format), suggestedFileName);

    public Task<string?> PickSaveFindingsAsync(string suggestedFileName) =>
        PickSaveAsync(FindingsList, suggestedFileName);

    // The one picker call both doors delegate to. The caller already chose the format, so the dialog offers
    // exactly that one rather than letting a typed extension contradict the choice; the suggested name carries
    // the same extension, but it is a display string and never the format's source.
    private async Task<string?> PickSaveAsync(SaveFileDescription description, string suggestedFileName)
    {
        if (Owner is null)
            return null;
        IStorageFile? file = await Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = description.Title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = description.Extension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(description.FileTypeLabel)
                {
                    Patterns = new[] { "*." + description.Extension },
                },
            }
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickCatalogFileAsync()
    {
        if (Owner is null)
            return null;
        IReadOnlyList<IStorageFile> files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importer katalogfil",
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
            Title = "Importer katalogmappe",
            AllowMultiple = false
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task ShowAboutAsync()
    {
        if (Owner is null)
            return;
        // Passing this: the About window's repository link opens through OpenExternalUrlAsync below rather than
        // launching for itself, so there is ONE external-open policy in the app.
        await new AboutWindow(this).ShowDialog(Owner);
    }

    public Task ShowSettingsAsync(string settingsText) =>
        ShowButtonsAsync("Effektive indstillinger", settingsText, selectable: true, ("Luk", true));

    public Task<PropertiesResult?> EditPropertiesAsync(string title, string name, string note, LibraryOrigin? origin = null,
        string affirmative = "OK", string? userGroupCaption = null, bool? conditionsOr = null,
        ElementDialogField? focus = null) =>
        WithOwnerAsync(owner => PropertiesWindow.ShowAsync(owner, title, name, note, origin, affirmative,
            userGroupCaption, conditionsOr, focus));

    public Task<VariablePropertiesResult?> EditVariablePropertiesAsync(VariablePropertiesInput input) =>
        WithOwnerAsync(owner => VariablePropertiesWindow.ShowAsync(owner, input));

    public Task<SceneContainerResult?> EditSceneContainerAsync(SceneContainerInput input) =>
        WithOwnerAsync(owner => SceneContainerWindow.ShowAsync(owner, input));

    public Task<PinPropertiesResult?> EditPinPropertiesAsync(PinPropertiesInput input, Func<PinPropertiesResult, Task>? onApply = null) =>
        WithOwnerAsync(owner => PinPropertiesWindow.ShowAsync(owner, input, onApply));

    public Task<ProductDialogEdits?> EditProductDialogAsync(
        ProductDialogDescriptor descriptor, IReadOnlyList<ProductTerminal>? terminals = null,
        IReadOnlyList<ProductSetting>? settings = null,
        ProductDialogShowOptions? options = null,
        ProductDialogStep? onStep = null) =>
        WithOwnerAsync(owner => ProductDialogWindow.ShowAsync(
            owner, new ProductDialogViewModel(descriptor, terminals, settings), options, onStep));

    public Task<string?> EditConstantAsync(ConstantEditorInput input) =>
        WithOwnerAsync(owner => ConstantEditorWindow.ShowAsync(owner, input));

    public Task<SceneValueResult?> EditSceneValueAsync(SceneValueInput input) =>
        WithOwnerAsync(owner => SceneValueWindow.ShowAsync(owner, input));

    public Task<EnumDefinitionResult?> EditEnumDefinitionAsync(EnumDefinitionInput input) =>
        WithOwnerAsync(owner => EnumDefinitionWindow.ShowAsync(owner, input));

    public Task ManageEnumTypesAsync(EnumTypeManagerInput input) =>
        WithOwnerAsync(owner => EnumTypeManagerWindow.ShowAsync(owner, input));

    public Task<string?> PromptForNameAsync(NamePromptInput input) =>
        WithOwnerAsync(owner => NamePromptWindow.ShowAsync(owner, input));

    public Task<ProjectInfoData?> EditProjectInfoAsync(ProjectInfoData current, ProjectInfoSuggestions suggestions) =>
        WithOwnerAsync(owner => ProjectInfoWindow.ShowAsync(owner, current, suggestions));

    public Task ShowReportPickerAsync(IReportPickerViewModel viewModel) =>
        WithOwnerAsync(owner => ReportPickerWindow.ShowAsync(owner, viewModel));

    public Task ShowModuleMapAsync(DatalineModuleMap map) =>
        WithOwnerAsync(owner => ModuleMapWindow.ShowAsync(owner, map));

    /// <summary>Hands a generated report file, or a configured URL, to whatever the desktop associates with it.
    /// <para>Through Avalonia's <see cref="ILauncher"/>, not <c>Process.Start(UseShellExecute: true)</c>: shell
    /// execute is a Windows concept that the .NET runtime emulates elsewhere by shelling out to <c>xdg-open</c> /
    /// <c>open</c>, which is exactly the per-platform guessing the framework already does properly — and it also
    /// tells file from URL, which the shell verb cannot.</para></summary>
    public async Task<bool> OpenExternalUrlAsync(string url)
    {
        bool launched = false;
        try
        {
            if (Owner?.Launcher is not { } launcher)
            {
                _logger.LogError("Cannot open {Url}: there is no window to launch it from", url);
            }
            else if (File.Exists(url))
            {
                // A generated report: launched as a FILE, so the desktop opens it with the handler for its type
                // rather than having to re-parse a path as a URI (where a space or a '#' would break it).
                launched = await launcher.LaunchFileInfoAsync(new FileInfo(url));
            }
            else if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                launched = await launcher.LaunchUriAsync(uri);
            }
            else
            {
                _logger.LogError("Cannot open {Url}: it is neither an existing file nor an absolute URI", url);
            }
        }
        catch (Exception ex)
        {
            // Reported, not just logged: handing the document to the shell is where the "view report" workflow
            // ENDS, so swallowing the launch failure made a dead end look like a success — to the user and to any
            // automation client watching for a final outcome (UX review CORE-03).
            _logger.LogError(ex, "Could not open URL {Url}", url);
        }
        return launched;
    }

    private async Task<IStorageFolder?> GetFolderAsync(string? directory)
    {
        if (Owner is null || string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;
        return await Owner.StorageProvider.TryGetFolderFromPathAsync(directory);
    }

    /// <summary>The AutomationId every code-built confirm/message dialog window carries.</summary>
    public const string ConfirmDialogAutomationId = "ConfirmDialog";

    /// <summary>The AutomationId of the <paramref name="index"/>-th choice button on a confirm dialog, counted in
    /// the order the caller passed them; the LAST is the safe (negative) default.</summary>
    public static string ConfirmChoiceAutomationId(int index) => $"ConfirmChoice{index}";

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
        // Every dialog raised through here shares one shape, so it shares one id. The TITLE varies per call and is
        // Danish; the id is how an automation client recognises "a confirm dialog is up" at all.
        Avalonia.Automation.AutomationProperties.SetAutomationId(dialog, ConfirmDialogAutomationId);

        T defaultValue = buttons.Length > 0 ? buttons[^1].Value : default!;
        Button? safeButton = null;
        for (int index = 0; index < buttons.Length; index++)
        {
            (string label, T value) = buttons[index];
            var button = new Button { Content = label, MinWidth = 84 };
            // The labels are the caller's Danish strings ("Gem", "Kassér", "Annuller"), so the choices are
            // addressed by POSITION, which the caller controls and localization does not touch. The last is
            // always the safe/negative one — the same button Escape and the title-bar X resolve to.
            Avalonia.Automation.AutomationProperties.SetAutomationId(button, ConfirmChoiceAutomationId(index));
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

        // The INNERMOST window, like every other modal: a message box raised from inside a dialog belongs to that
        // dialog. This path does not go through WithOwnerAsync — it builds its window here and resolves through a
        // TaskCompletionSource rather than through ShowDialog's own task — so it has to ask for the owner itself.
        if (Owner is { } shell)
            _ = dialog.ShowDialog(Innermost(shell));
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
