using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    private readonly List<ApplicationItem> _applicationItems;

    public bool DialogResult { get; private set; }

    public record ApplicationItem(string Name, Application Value)
    {
        public override string ToString() => Name;
    }

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

        // Populate application items from enum
        _applicationItems = Enum.GetValues<Application>()
            .Select(app => new ApplicationItem(app.ToString(), app))
            .ToList();

        InitializeComponent();

        // Set ComboBox items source
        ApplicationComboBox.ItemsSource = _applicationItems;

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

            // Set application combo box to current value
            ApplicationComboBox.SelectedItem = _applicationItems.FirstOrDefault(item => item.Value ==  _ihcSettings.Application);

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
            Application application = ApplicationComboBox.SelectedItem is ApplicationItem selectedItem
                ? selectedItem.Value
                : Application.openapi;

            // Update IHC Settings properties directly (IhcSettings has mutable properties)
            _ihcSettings.Endpoint = EndpointTextBox.Text ?? string.Empty;
            _ihcSettings.UserName = UserNameTextBox.Text ?? string.Empty;
            _ihcSettings.Password = PasswordTextBox.Text ?? string.Empty;
            _ihcSettings.Application = application;
            _ihcSettings.LogSensitiveData = LogSensitiveDataCheckBox.IsChecked ?? false;
            _ihcSettings.AllowDangerousInternTestCalls = AllowDangerousTestCallsCheckBox.IsChecked ?? false;

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
