using System;
using System.Diagnostics;
using Avalonia.Platform.Storage;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ihc_openvisual.Views;

public partial class AboutWindow : Window
{
    private readonly ILogger<AboutWindow> _logger;

    public AboutWindow()
    {
        InitializeComponent();

        _logger = (Program.LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AboutWindow>();

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
            // Avalonia's own launcher, not Process.Start(UseShellExecute: true): the shell verb is a Windows
            // concept, and this window opens the same link on all three desktops.
            await Launcher.LaunchUriAsync(new Uri(Constants.SdkRepoLink));
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
