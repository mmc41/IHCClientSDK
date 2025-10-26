using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ihc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IhcLab;

public partial class LoginDialog : Window
{
    private readonly IhcSettings _ihcSettings;
    private ILogger<LoginDialog> logger;

    public bool DialogResult { get; private set; }

    /// <summary>
    /// Parameterless constructor for design-time support.
    /// </summary>
    public LoginDialog() : this(new IhcSettings())
    {
    }

    public LoginDialog(IhcSettings ihcSettings)
    {
        if (ihcSettings == null)
        {
            throw new ArgumentNullException(nameof(ihcSettings));
        }

        _ihcSettings = ihcSettings;

        InitializeComponent();

        // Initialize logger
        this.logger = Program.loggerFactory != null
            ? Program.loggerFactory.CreateLogger<LoginDialog>()
            : NullLoggerFactory.Instance.CreateLogger<LoginDialog>();

        // Load existing values from IHC settings
        LoadConfiguration();

        // Update OK button state based on initial values
        UpdateOkButtonState();
    }

    private void LoadConfiguration()
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(
            nameof(LoginDialog) + "." + nameof(LoadConfiguration),
            ActivityKind.Internal);

        try
        {
            EndpointTextBox.Text = _ihcSettings.Endpoint ?? string.Empty;
            UserNameTextBox.Text = _ihcSettings.UserName ?? string.Empty;
            PasswordTextBox.Text = _ihcSettings.Password ?? string.Empty;

            // Set application combo box
            string application = _ihcSettings.Application ?? "openapi";
            switch (application.ToLower())
            {
                case "treeview":
                    ApplicationComboBox.SelectedIndex = 0;
                    break;
                case "openapi":
                    ApplicationComboBox.SelectedIndex = 1;
                    break;
                case "administrator":
                    ApplicationComboBox.SelectedIndex = 2;
                    break;
                default:
                    ApplicationComboBox.SelectedIndex = 0;
                    break;
            }

            LogSensitiveDataCheckBox.IsChecked = _ihcSettings.LogSensitiveData;
            AllowDangerousTestCallsCheckBox.IsChecked = _ihcSettings.AllowDangerousInternTestCalls;
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            logger.LogError(ex, "Error loading configuration");
        }
    }

    private bool ValidateInput()
    {
        ValidationMessageTextBlock.IsVisible = false;
        ValidationMessageTextBlock.Text = string.Empty;

        // Validate required fields
        if (string.IsNullOrWhiteSpace(EndpointTextBox.Text))
        {
            ValidationMessageTextBlock.Text = "Endpoint is required.";
            ValidationMessageTextBlock.IsVisible = true;
            return false;
        }

        if (string.IsNullOrWhiteSpace(UserNameTextBox.Text))
        {
            ValidationMessageTextBlock.Text = "UserName is required.";
            ValidationMessageTextBlock.IsVisible = true;
            return false;
        }

        if (string.IsNullOrWhiteSpace(PasswordTextBox.Text))
        {
            ValidationMessageTextBlock.Text = "Password is required.";
            ValidationMessageTextBlock.IsVisible = true;
            return false;
        }

        if (ApplicationComboBox.SelectedItem == null)
        {
            ValidationMessageTextBlock.Text = "Application is required.";
            ValidationMessageTextBlock.IsVisible = true;
            return false;
        }

        return true;
    }

    private void UpdateOkButtonState()
    {
        // Return early if controls are not initialized yet (during InitializeComponent)
        if (EndpointTextBox == null || UserNameTextBox == null ||
            PasswordTextBox == null || ApplicationComboBox == null || OkButton == null)
        {
            return;
        }

        // Enable OK button only if all required fields are non-empty
        bool isValid = !string.IsNullOrWhiteSpace(EndpointTextBox.Text) &&
                       !string.IsNullOrWhiteSpace(UserNameTextBox.Text) &&
                       !string.IsNullOrWhiteSpace(PasswordTextBox.Text) &&
                       ApplicationComboBox.SelectedItem != null;

        OkButton.IsEnabled = isValid;
    }

    private void OnRequiredFieldChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateOkButtonState();
    }

    private void SaveConfiguration()
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(
            nameof(LoginDialog) + "." + nameof(SaveConfiguration),
            ActivityKind.Internal);

        try
        {
            // Get application value from combo box
            string application = ((ComboBoxItem)ApplicationComboBox.SelectedItem!).Content?.ToString() ?? "treeview";

            // Update IHC Settings properties directly using reflection
            // Note: Since IhcSettings is a record with init-only properties, we need to use reflection
            typeof(IhcSettings).GetProperty(nameof(IhcSettings.Endpoint))?.SetValue(_ihcSettings, EndpointTextBox.Text ?? string.Empty);
            typeof(IhcSettings).GetProperty(nameof(IhcSettings.UserName))?.SetValue(_ihcSettings, UserNameTextBox.Text ?? string.Empty);
            typeof(IhcSettings).GetProperty(nameof(IhcSettings.Password))?.SetValue(_ihcSettings, PasswordTextBox.Text ?? string.Empty);
            typeof(IhcSettings).GetProperty(nameof(IhcSettings.Application))?.SetValue(_ihcSettings, application);
            typeof(IhcSettings).GetProperty(nameof(IhcSettings.LogSensitiveData))?.SetValue(_ihcSettings, LogSensitiveDataCheckBox.IsChecked ?? false);
            typeof(IhcSettings).GetProperty(nameof(IhcSettings.AllowDangerousInternTestCalls))?.SetValue(_ihcSettings, AllowDangerousTestCallsCheckBox.IsChecked ?? false);

            activity?.SetTag("configuration.updated", true);
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            logger.LogError(ex, "Error saving configuration");
            throw;
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(
            nameof(LoginDialog) + "." + nameof(OnOkClick),
            ActivityKind.Internal);

        try
        {
            if (ValidateInput())
            {
                SaveConfiguration();
                DialogResult = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            ValidationMessageTextBlock.Text = $"Error saving configuration: {ex.Message}";
            ValidationMessageTextBlock.IsVisible = true;
            logger.LogError(ex, "Error in OK button click handler");
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(
            nameof(LoginDialog) + "." + nameof(OnCancelClick),
            ActivityKind.Internal);

        DialogResult = false;
        Close();
    }
}
