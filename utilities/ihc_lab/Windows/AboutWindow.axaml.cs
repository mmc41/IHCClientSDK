using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ihc;

namespace IhcLab;

public partial class AboutWindow : Window
{
    private ILogger<AboutWindow> logger;
        
    public AboutWindow()
    {
        InitializeComponent();

        // Avalonia uses logger so we might as well use it for important things like errors and warnings.
        this.logger = Program.loggerFactory != null ? Program.loggerFactory.CreateLogger<AboutWindow>() : NullLoggerFactory.Instance.CreateLogger<AboutWindow>();
               
        RepoLink.Text = Constants.SDK_REPO_LINK;
        RepoAuthors.Text = Constants.SDK_AUTHORS;
        AppDescription.Text = Constants.APP_DESCRIPTION;

        // Display version information
        AppVersionText.Text = $"App Version: {IhcLab.VersionInfo.GetAppVersionStr()}";
        SdkVersionText.Text = $"SDK Version: {Ihc.VersionInfo.GetSdkVersion()}";
    }

    private void OnRepoLinkClick(object? sender, PointerPressedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(AboutWindow)+"."+nameof(OnRepoLinkClick), ActivityKind.Internal);

        // Open the GitHub URL in the default browser
        var url = Constants.SDK_REPO_LINK;
        try
        {
            // Use ProcessStartInfo to open URL in default browser
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            logger.Log(LogLevel.Error, message: "Could not open link " + url, exception: ex);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(AboutWindow)+"."+nameof(OnCloseClick), ActivityKind.Internal);
        Close();
    }
}
