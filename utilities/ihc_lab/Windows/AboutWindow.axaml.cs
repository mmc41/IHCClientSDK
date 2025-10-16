using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace IhcLab;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        RepoLink.Text = Constants.SDK_REPO_LINK;
        RepoAuthors.Text = Constants.SDK_AUTHORS;
        AppDescription.Text = Constants.APP_DESCRIPTION;
    }

    private void OnRepoLinkClick(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            // Open the GitHub URL in the default browser
            var url = Constants.SDK_REPO_LINK;

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
            // If opening browser fails, silently ignore
            // (alternatively, could show a message box with the URL)
            Debug.WriteLine($"Failed to open URL: {ex.Message}");
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
