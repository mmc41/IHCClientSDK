using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Ihc;
using Ihc.Vis.Problems;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ihc_openvisual.Configuration;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;

namespace ihc_openvisual.Views;

/// <summary>
/// The details surface for one internal error: the Danish sentence the row already shows, the identity a support
/// case needs, and the captured technical detail underneath.
///
/// <para><b>Why a stack trace on this screen is not the D01 leak.</b> The gate bans an English engine diagnostic
/// becoming THE MESSAGE the installer is shown. Here the Danish sentence is rendered whole at the top, from the
/// record, exactly as the row renders it; the detail is a LABELLED diagnostic readout reached only by an explicit
/// gesture on a row that already says what happened in Danish. It is the same posture as the effective-settings
/// dialog, which already puts a long technical readout on screen. Only the captured payload is verbatim technical
/// text — every piece of chrome is Danish.</para>
///
/// <para><b>A plain <see cref="Window"/>, not a <c>ResultDialog</c> and not <c>ShowButtonsAsync</c>.</b> There is
/// no result to carry back, and the built-in dialog builder is fixed-width and non-resizable — a captured detail
/// would clip or push the window past the screen.</para>
///
/// <para><b>It reads the record, never an exception.</b> <see cref="InternalError.Detail"/> is a string captured
/// once at the raise site, so this presentation layer receives opaque text and there is nothing here for the
/// architecture scan's exception-text ban to catch.</para>
/// </summary>
public partial class InternalErrorWindow : Window
{
    /// <summary>Standalone construction, for the XAML previewer and the roster audit. Shows nothing.</summary>
    public InternalErrorWindow()
    {
        InitializeComponent();
    }

    public InternalErrorWindow(InternalError error) : this()
    {
        ArgumentNullException.ThrowIfNull(error);
        Show(error);
    }

    /// <summary>
    /// Binds one fault onto the window, through the view-model that also assembles what the copy button hands
    /// over — so the copied text and the shown text are read off ONE object and cannot drift.
    /// </summary>
    internal void Show(InternalError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Show(new InternalErrorViewModel(error, Ihc.Bootstrap.TelemetryBootstrap.GetAppVersionStr()));
    }

    /// <summary>Binds a prepared view-model. The door a headless test drives.</summary>
    internal void Show(InternalErrorViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;

        // Assigned rather than bound, because these three never change once shown: a binding would buy nothing
        // and would put the window's most important text — the Danish sentence — one indirection further from
        // the reader of this file. The copy button's label IS bound, because that one does change.
        Sentence.Text = viewModel.Sentence;
        Identity.Text = viewModel.Identity;
        DetailText.Text = viewModel.Detail;
    }

    /// <summary>
    /// The ONE Avalonia-shaped line in the copy path. Everything the button copies is assembled on the
    /// view-model; this asks for it, hands it to the platform, and tells the view-model which of the two things
    /// happened. <c>IClipboard</c> is an Avalonia type, and a view-model may not name one.
    /// </summary>
    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        using OperationScope scope = _telemetry.Start(nameof(OnCopyClick));

        // Through the guard, which is this app's floor for a handler with no view-model boundary in reach. A
        // window handler runs off the message loop, where neither Dispatcher.UnhandledException nor
        // AppDomain.UnhandledException can see a fault at all — so a bare try/catch here would be a fourth
        // hand-rolled floor rather than the one the containment gate can see.
        Exception? failure = await HandlerGuard.RunAsync(
            () => CopyAsync(scope), _logger, nameof(OnCopyClick));
        if (failure is not null)
        {
            // A clipboard that threw is the same outcome for the reader as one that was absent: nothing was
            // copied, and the button says so in place.
            scope.SetOutcome(OperationOutcome.Failed(failure));
            Refuse();
        }
    }

    private async Task CopyAsync(OperationScope scope)
    {
        if (DataContext is not InternalErrorViewModel viewModel)
        {
            return;
        }
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            // No clipboard at all — a restricted platform. Coded, and said ON the button: this window is modal,
            // so a second dialog over it would obscure the very text the user is trying to take away.
            scope.SetOutcome(OperationOutcome.Refused(HostProblemCodes.ClipboardUnavailable.Value));
            Refuse();
            return;
        }
        await clipboard.SetTextAsync(viewModel.Payload);
        viewModel.MarkCopied();
    }

    /// <summary>Nothing was copied. ONE place says so, so the absent-clipboard and threw-clipboard cases cannot
    /// come to disagree about what the button reads.</summary>
    private void Refuse()
    {
        if (DataContext is InternalErrorViewModel viewModel)
        {
            viewModel.MarkCopyUnavailable();
        }
    }

    private readonly ILogger<InternalErrorWindow> _logger =
        (Program.LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<InternalErrorWindow>();

    /// <summary>This type's entry point into the instrumentation core.</summary>
    private readonly OperationTelemetry _telemetry =
        new(AppTelemetryRegistry.Surface, nameof(InternalErrorWindow));

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
