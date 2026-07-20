using System;
using System.Diagnostics;
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
        AppVersionText.Text = $"App Version: {Ihc.Bootstrap.AppTelemetryBootstrap.GetAppVersionStr()}";
        SdkVersionText.Text = $"SDK Version: {Ihc.VersionInfo.GetSdkVersionStr()}";
    }

    private void OnRepoLinkClick(object? sender, RoutedEventArgs e)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(AboutWindow)}.{nameof(OnRepoLinkClick)}", ActivityKind.Internal);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = Constants.SdkRepoLink, UseShellExecute = true });
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
