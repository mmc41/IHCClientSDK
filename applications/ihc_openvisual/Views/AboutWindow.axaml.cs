using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ihc;
using ihc_openvisual.Configuration;
using ihc_openvisual.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ihc_openvisual.Views;

public partial class AboutWindow : Window
{
    /// <summary>This type's entry point into the instrumentation core.</summary>
    private readonly OperationTelemetry _telemetry =
        new(AppTelemetryRegistry.Surface, nameof(AboutWindow));

    private readonly ILogger<AboutWindow> _logger;
    private readonly IDialogService? _dialogs;

    /// <summary>Standalone construction (the XAML previewer and the dialog smoke tests): the repository link
    /// reports that it could not open rather than launching, since there is no dialog port to launch through.</summary>
    public AboutWindow() : this(null)
    {
    }

    /// <param name="dialogs">The port that owns external launching for the whole app — passed by
    /// <see cref="AvaloniaDialogService.ShowAboutAsync"/> so this window does not re-implement it.</param>
    public AboutWindow(IDialogService? dialogs)
    {
        InitializeComponent();

        _logger = (Program.LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AboutWindow>();
        _dialogs = dialogs;

        AppDescription.Text = Constants.AppDescription;
        RepoLinkText.Text = Constants.SdkRepoLink;
        RepoAuthors.Text = Constants.Authors;
        AppVersionText.Text = $"App version: {Ihc.Bootstrap.TelemetryBootstrap.GetAppVersionStr()}";
        SdkVersionText.Text = $"SDK version: {Ihc.VersionInfo.GetSdkVersionStr()}";
    }

    // async void, deliberately: this is a view-layer event handler, and a Window handler runs off the message
    // loop where no global exception handler can see a fault (AP-06/WS-11). Contained by HandlerGuard rather than
    // by a try/catch written out here: a second copy of the floor is a copy that can drift from the one it
    // duplicates — and this one already had, since it returned nothing for a caller to react to and left no
    // durable row.
    private async void OnRepoLinkClick(object? sender, RoutedEventArgs e)
    {
        using OperationScope scope = _telemetry.Start(nameof(OnRepoLinkClick));
        if (await HandlerGuard.RunAsync(() => OpenRepoLinkAsync(scope), _logger, nameof(OnRepoLinkClick))
            is { } failure)
        {
            // A browser-launch failure must not terminate the app; the guard has recorded it and kept the dialog
            // open, and the span carries the outcome.
            scope.SetOutcome(Ihc.OperationOutcome.Failed(failure));
        }
    }

    private async Task OpenRepoLinkAsync(OperationScope scope)
    {
        // Through the dialog port, which is where "hand something to the desktop" lives for the whole app: it
        // already tells file from URL, launches through Avalonia's ILauncher rather than the Windows-only shell
        // verb, and reports whether the handler actually started. Launching here instead would be the one
        // external open outside that policy.
        if (_dialogs is { } dialogs && await dialogs.OpenExternalUrlAsync(Constants.SdkRepoLink))
        {
            return;
        }

        // NOTHING OPENED. This arm used to log and leave the span Unset, so a machine with no handler for the
        // URL produced a span indistinguishable from a successful launch — the outcome a support query would
        // most want to count. Coded rather than exception-shaped, because nothing threw: the platform declined,
        // and a support query counting launch failures wants both in the same bucket.
        //
        // Not worth a second dialog over a modal one either: the address itself is on screen right above the
        // button, so a reader still has a way forward. The port records the underlying reason.
        scope.SetOutcome(Ihc.OperationOutcome.FailedWith(RepoLinkNotOpenedOutcome));
        _logger.LogError("Could not open repository URL {Url}", Constants.SdkRepoLink);
    }

    /// <summary>
    /// What the span's error type reads when the OS opened no handler for the repository link.
    /// <para>
    /// A span label, NOT a catalogue code, and deliberately not declared as one. A host catalogue entry is a
    /// Danish sentence some site shows — <c>MessageSiteRegisterTests</c> gates exactly that — and this arm shows
    /// nothing: the address is on screen above the button, so a modal over a modal buys the reader nothing.
    /// Declaring it would add a row whose sentence no site can ever display.
    /// </para>
    /// </summary>
    internal const string RepoLinkNotOpenedOutcome = "app.openvisual.repo-link-not-opened";

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
