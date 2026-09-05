using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

    /// <summary>This service's entry point into the instrumentation core.</summary>
    private readonly Ihc.OperationTelemetry _telemetry =
        new(AppTelemetryRegistry.Surface, nameof(AvaloniaDialogService));

    /// <summary>
    /// One modal's span, covering the whole wait — from raising the dialog to the person answering it.
    ///
    /// <para>Why a span for something that is only a person thinking: without it, that thinking is billed to the
    /// operation the dialog interrupted, and nothing says so. Measured on a live save, the failure dialog turned
    /// 24 ms of file I/O into a 13.6 s <c>SaveToAsync</c>, and a picker turned a save-as into 20 s — numbers a
    /// reader of the trace, or of the save-duration histogram, cannot tell from slow work. A child span makes the
    /// remainder honest by subtraction and costs one span per modal.</para>
    ///
    /// <para><see cref="CallerMemberNameAttribute"/> rather than a literal per site: the member IS the dialog's
    /// name, so no call site can pass a name that has drifted from the method it names, and a dialog added later
    /// is named correctly without anyone remembering to name it. A private helper that fronts more than one
    /// public door leaves the parameter to ITS caller, so the span reports the door the installer used.</para>
    /// <para>
    /// EVERY door comes through here — including the ones that read as ordinary <c>async</c> methods. They used
    /// to open the scope themselves with a bare <c>using</c>, which is the same funnel written out by hand and
    /// drifted the way a copy does: the hand-written half never classified a dialog that FAULTED, so a picker
    /// that broke recorded <c>ok</c>. One funnel is what keeps the two halves from disagreeing again.
    /// </para>
    /// <para>
    /// It HANDS BACK the task rather than awaiting it, which keeps the one property an <c>async</c> rewrite
    /// would quietly destroy: a modal that cannot even be BUILT — no platform, no window — throws
    /// SYNCHRONOUSLY, before its caller's next statement runs.
    /// </para>
    /// <para>
    /// That is not a nicety. <c>ShowProblemAsync</c> counts a problem only AFTER asking for its presentation,
    /// precisely so a dialog that failed to appear is not counted as one an installer saw; turning the throw
    /// into a faulted task made the count fire anyway, and <c>APresentationThatFailsIsNotCounted</c> caught it.
    /// So <paramref name="show"/> is invoked inside a plain try — not after an await — and the scope is closed
    /// on that path too, because a started span nobody stops would stay <see cref="Activity.Current"/> and
    /// adopt every later span on the thread as its child.
    /// </para>
    /// </summary>
    /// <param name="enrich">
    /// Runs against the scope BEFORE the dialog is raised, for a door that knows something about the wait worth
    /// carrying on its span. Last in the list so <see cref="CallerMemberNameAttribute"/> still fills
    /// <paramref name="dialog"/> for every caller that does not need it.
    /// </param>
    private Task<T> TimedAsync<T>(Func<Task<T>> show, [CallerMemberName] string dialog = "",
        Action<Ihc.OperationScope>? enrich = null)
    {
        // The caller's ambient activity, captured to be PUT BACK below. Starting a span makes it current, and
        // this method is a PLAIN one — there is no async kickoff to restore the execution context on the way
        // out, so what it leaves current is what the caller's continuation keeps. Measured with a probe over
        // the three shapes (telemetry_points §12.3): the change escapes from a plain method and does not from
        // an `async` one. Left unrestored, every span a gesture opened AFTER the dialog — the apply behind a
        // properties dialog, the delete behind its confirm — became a child of a modal span that had already
        // stopped.
        System.Diagnostics.Activity? ambient = System.Diagnostics.Activity.Current;
        Ihc.OperationScope modal = _telemetry.Start(dialog);
        enrich?.Invoke(modal);
        Task<T> pending;
        try
        {
            pending = show();
        }
        catch (Exception ex)
        {
            // No restore needed on this arm: disposing the scope stops the span, and stopping it makes its own
            // parent current again.
            modal.SetOutcome(Ihc.OperationOutcome.Failed(ex));
            modal.Dispose();
            throw;
        }
        // AFTER `show()`, so the dialog's own work is raised while the modal span is current and nests under
        // it; before the return, so nothing the CALLER does afterwards can.
        System.Diagnostics.Activity.Current = ambient;
        // CA2025 sees a disposable handed to a task this method does not await. That is the design: the scope is
        // disposed by AwaitedAsync when the dialog is ANSWERED, which is the wait this span exists to measure,
        // and disposing it here instead would time the raising of the dialog rather than the waiting for it.
#pragma warning disable CA2025
        return AwaitedAsync(pending, modal);
#pragma warning restore CA2025
    }

    /// <inheritdoc cref="TimedAsync{T}"/>
    /// <remarks>
    /// CA1859 would have this declare the <c>Task&lt;bool&gt;</c> it actually returns. Declined, and suppressed
    /// rather than silenced repo-wide: that <c>bool</c> is scaffolding — the value <see cref="CompletedAsync"/>
    /// invents so one generic can serve both shapes — and publishing it would push a meaningless result up
    /// through every void dialog door that calls this.
    /// </remarks>
#pragma warning disable CA1859
    private Task TimedAsync(Func<Task> show, [CallerMemberName] string dialog = "",
        Action<Ihc.OperationScope>? enrich = null) =>
        // Through the generic so the try/catch above is written once. The inner call is an ARGUMENT, evaluated
        // where the generic invokes the lambda, so a synchronous throw still lands in that one catch.
        TimedAsync(() => CompletedAsync(show()), dialog, enrich);
#pragma warning restore CA1859

    /// <summary>
    /// Closes <paramref name="modal"/> when the dialog is answered — which is what times the wait — and says
    /// what the answer WAS.
    /// </summary>
    /// <remarks>
    /// The catch is the other half of <see cref="TimedAsync{T}"/>'s: a dialog can fail before it is built, which
    /// that one records, and it can fail after — a window that could not be shown, a storage provider that
    /// threw. Without this arm the <c>using</c> disposes with the default outcome, so a modal that BROKE is
    /// recorded as one the installer answered, which is the single thing the outcome machinery exists to
    /// prevent. Rethrown unchanged; recording is additive.
    /// </remarks>
    private static async Task<T> AwaitedAsync<T>(Task<T> pending, Ihc.OperationScope modal)
    {
        using (modal)
        {
            try
            {
                return await pending;
            }
            catch (Exception ex)
            {
                modal.SetOutcome(Ihc.OperationOutcome.Failed(ex));
                throw;
            }
        }
    }

    /// <summary>A void dialog as a valued one, so <see cref="TimedAsync{T}"/> serves both shapes.</summary>
    private static async Task<bool> CompletedAsync(Task pending)
    {
        await pending;
        return true;
    }

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
    private Task<T?> WithOwnerAsync<T>(Func<Window, Task<T?>> show, [CallerMemberName] string dialog = "")
        where T : class =>
        TimedAsync(() => Owner is { } owner ? show(Innermost(owner)) : Task.FromResult<T?>(null), dialog);

    /// <inheritdoc cref="WithOwnerAsync{T}"/>
    private Task WithOwnerAsync(Func<Window, Task> show, [CallerMemberName] string dialog = "") =>
        TimedAsync(() => Owner is { } owner ? show(Innermost(owner)) : Task.CompletedTask, dialog);

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

    public Task<SaveChangesResult> ConfirmSaveChangesAsync(string documentName) =>
        TimedAsync(() => ShowButtonsAsync(
            SaveChangesTitle,
            SaveChangesMessage(documentName),
            (SaveChangesSaveLabel, SaveChangesResult.Save),
            (SaveChangesDiscardLabel, SaveChangesResult.Discard),
            (SaveChangesCancelLabel, SaveChangesResult.Cancel)));

    public Task<bool> ConfirmAsync(string title, string message) =>
        TimedAsync(() => ShowButtonsAsync(title, message, ("Ja", true), ("Nej", false)));

    public Task ShowMessageAsync(string title, string message) =>
        TimedAsync(() => ShowButtonsAsync(title, message, ("OK", true)));

    // The coded doors render through the shell's ONE presentation path and then reuse the same box: identity is
    // decided there, never per dialog, so a problem shown here and one shown in a future findings pane read alike.
    //
    // They are also where problems are COUNTED, and that placement is the point. RaisedProblemDisplay is the
    // tidier-looking home, but call sites reach a dialog without going through it - so counting there would
    // under-report by exactly the paths nobody remembered. These three overloads are what a problem must pass
    // through to become something an installer actually sees.
    public Task ShowProblemAsync(string title, Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        return PresentAsync(problem.Code, title, ProblemPresenter.Text(problem));
    }

    public Task ShowProblemAsync(string title, ProblemChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        // The CAUSE, not the operation: the operation names what was being attempted, the cause names what
        // was wrong, and the second is the one worth counting.
        return PresentAsync(chain.Cause.Code, title, ProblemPresenter.Text(chain));
    }

    public Task ShowProblemAsync(string title, ProblemAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        // The HEAD, once - not once per item. An aggregate is ONE thing shown to the user, and counting its
        // items would make a single dialog about a many-findings validation look like many dialogs.
        return PresentAsync(aggregate.Head.Code, title, ProblemPresenter.Text(aggregate));
    }

    /// <summary>
    /// Presents one problem and counts it — in that order, which is the whole content of this method.
    /// </summary>
    /// <remarks>
    /// <para>The counter's own summary says it records a problem <b>actually presented</b>, and the three
    /// overloads used to count BEFORE calling the presentation they were counting. So a dialog that could not be
    /// built was counted anyway, and the metric meant "presentation attempted" while its documentation claimed
    /// otherwise. One of the two had to move, and the comment was the drift: "attempted" is not a number anyone
    /// wants, because the interesting question is what installers actually hit.</para>
    /// <para><b>Counted once the presentation has STARTED, not once it is dismissed.</b> The task below completes
    /// when a person closes the dialog, and awaiting it would fold think-time into the metric and lose the count
    /// entirely for a process that exits with the modal up — the two costs <c>OperationScope</c> documents. A
    /// problem shown is counted whether or not it is dismissed; a problem that never reached the screen is not
    /// counted at all.</para>
    /// </remarks>
    /// <remarks>
    /// <para>It raises the box DIRECTLY rather than through <see cref="ShowMessageAsync"/>, and the difference is
    /// the span. A coded problem routed through the message door produced a span named
    /// <c>ShowMessageAsync</c> — indistinguishable from an informational box, which is the worst possible answer
    /// for the case that motivated modal spans at all: the audit measured a failure dialog holding a 24 ms save
    /// open for 13.6 s. Named for the coded door and carrying the code, the span says WHICH problem the
    /// installer was reading. The rendering is unchanged; this is the same call the message door makes.</para>
    /// </remarks>
    private Task PresentAsync(Ihc.Vis.Problems.ProblemCode code, string title, string text) =>
        TimedAsync(
            () =>
            {
                // The order the remarks above turn on: raise first, count second, so a box that could not be
                // built throws out of here without being counted. Inside the lambda, so that throw lands in
                // TimedAsync's own catch and closes the span.
                //
                // Typed Task, not Task<bool>: the button's value is scaffolding this door has no use for, and
                // the wider type is what selects the void funnel rather than the generic one.
                Task shown = ShowButtonsAsync(title, text, ("OK", true));
                CountProblem(code);
                return shown;
            },
            nameof(ShowProblemAsync),
            modal => modal.Activity?.SetTag(AppTelemetryRegistry.Attributes.ProblemCode, code.Value));

    /// <summary>Records one problem actually presented, keyed by its code and the family that code belongs to.</summary>
    private static void CountProblem(Ihc.Vis.Problems.ProblemCode code) =>
        AppTelemetryRegistry.ProblemRaised.Add(1,
            new KeyValuePair<string, object?>(AppTelemetryRegistry.Attributes.ProblemCode, code.Value),
            new KeyValuePair<string, object?>(AppTelemetryRegistry.Attributes.ProblemFamily, code.Family.ToString()));

    public Task<string?> PickOpenProjectAsync(string? initialDirectory) => TimedAsync<string?>(async () =>
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
    });

    public Task<string?> PickSaveProjectAsync(string? initialDirectory, string suggestedFileName) =>
        TimedAsync<string?>(async () =>
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
        });

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
    private Task<string?> PickSaveAsync(SaveFileDescription description, string suggestedFileName,
        [CallerMemberName] string dialog = "") =>
        TimedAsync<string?>(async () =>
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
        }, dialog);

    public Task<string?> PickCatalogFileAsync() => TimedAsync<string?>(async () =>
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
    });

    public Task<string?> PickCatalogFolderAsync() => TimedAsync<string?>(async () =>
    {
        if (Owner is null)
            return null;
        IReadOnlyList<IStorageFolder> folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Importer katalogmappe",
            AllowMultiple = false
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    });

    public Task ShowAboutAsync() => TimedAsync(async () =>
    {
        if (Owner is null)
            return;
        // Passing this: the About window's repository link opens through OpenExternalUrlAsync below rather than
        // launching for itself, so there is ONE external-open policy in the app.
        await new AboutWindow(this).ShowDialog(Owner);
    });

    public Task ShowInternalErrorAsync(Ihc.Vis.Problems.InternalError error)
    {
        // OUTSIDE the funnel: a null argument is the caller's bug, not a modal that failed, and reporting it as
        // one would put a span and an error.type on an operation that never started (CA1062).
        ArgumentNullException.ThrowIfNull(error);
        return TimedAsync(async () =>
        {
            if (Owner is null)
                return;
            await new InternalErrorWindow(error).ShowDialog(Owner);
        });
    }

    public Task ShowSettingsAsync(string settingsText) =>
        TimedAsync(() => ShowButtonsAsync(
            "Effektive indstillinger", settingsText, selectable: true, ("Luk", true)));

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
            TaskSupervisor.Fire(
                dialog.ShowDialog(Innermost(shell)),
                $"{nameof(AvaloniaDialogService)}.{nameof(ShowButtonsAsync)}");
        else
            dialog.Show();

        return tcs.Task;
    }

    /// <summary>Makes a code-built dialog keyboard-operable (A-9/A-10): <paramref name="focusOnOpen"/> (the safe,
    /// default control) takes focus when the dialog opens, and Escape closes it — the dialog's <c>Closed</c> handler
    /// then resolves it to its safe default. Public so a headless test can verify the wiring on a real window.</summary>
    public static void WireKeyboardDismissal(Window dialog, Control? focusOnOpen)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        dialog.Opened += (_, _) => focusOnOpen?.Focus();
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
                dialog.Close();
        };
    }
}
