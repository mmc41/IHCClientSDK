using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Configuration;
using ihc_openvisual.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ihc_openvisual.Views;

public partial class AboutWindow : Window
{
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
        AppVersionText.Text = $"App version: {Ihc.Bootstrap.AppTelemetryBootstrap.GetAppVersionStr()}";
        SdkVersionText.Text = $"SDK version: {Ihc.VersionInfo.GetSdkVersionStr()}";
    }

    // async void, deliberately: this is a view-layer event handler, and its whole body is inside the try/catch —
    // a Window handler runs off the message loop where no global exception handler can see a fault (AP-06/WS-11).
    private async void OnRepoLinkClick(object? sender, RoutedEventArgs e)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(AboutWindow)}.{nameof(OnRepoLinkClick)}", ActivityKind.Internal);
        try
        {
            // Through the dialog port, which is where "hand something to the desktop" lives for the whole app: it
            // already tells file from URL, launches through Avalonia's ILauncher rather than the Windows-only shell
            // verb, and reports whether the handler actually started. Launching here instead would be the one
            // external open outside that policy.
            if (_dialogs is not { } dialogs || !await dialogs.OpenExternalUrlAsync(Constants.SdkRepoLink))
            {
                // Not fatal and not worth a second dialog over a modal one: the address itself is on screen right
                // above the button, so a reader still has a way forward. The port records the underlying reason.
                _logger.LogError("Could not open repository URL {Url}", Constants.SdkRepoLink);
            }
        }
        catch (Exception ex)
        {
            // A browser-launch failure must not terminate the app; record it and keep the dialog open.
            if (activity is not null)
                Ihc.ActivityExtensions.SetError(activity, ex);
            _logger.LogError(ex, "Could not open repository URL {Url}", Constants.SdkRepoLink);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
