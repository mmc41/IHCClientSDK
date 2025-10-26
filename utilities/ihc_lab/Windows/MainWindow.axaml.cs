using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Ihc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IhcLab;

public partial class MainWindow : Window
{
    private IhcDomain? ihcDomain;
    private IClipboard? clipboard;

    private ILogger<MainWindow> logger;

    public MainWindow()
    {
        // Use OpenTel activities as the primary way to keep track of operations. This mix well with the SDK activities.
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + ".Ctr", ActivityKind.Internal);

        // Avalonia uses logger so we might as well use it for important things like errors and warnings.
        this.logger = Program.loggerFactory != null ? Program.loggerFactory.CreateLogger<MainWindow>() : NullLoggerFactory.Instance.CreateLogger<MainWindow>();

        try
        {
            InitializeComponent();
            DataContext = this;

            clipboard = this.Clipboard;

            if (clipboard == null)
            {
                CopyOutputMenuItem.IsEnabled = false;
                CopyErrorMenuItem.IsEnabled = false;
            }

            // Handle window closing event (when user clicks X button)
            Closing += OnWindowClosing;
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError(nameof(MainWindow) + " constructor error", ex);
            RunButton.IsEnabled = false;
        }
    }

    /// <summary>
    /// Async initialization of MainWindow that sets up IhcDomain and login if needed. Returns this for chaining.
    /// </summary>
    /// <returns>this</returns>
    public async Task<MainWindow> Start()
    {
        // Use OpenTel activities as the primary way to keep track of operations. This mix well with the SDK activities.
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + "." + nameof(Start), ActivityKind.Internal);

        try
        {
            ihcDomain = new IhcDomain();

            if (!ihcDomain.IhcSettings.IsValid())
            {
                var longinWindow = new LoginDialog(ihcDomain!.IhcSettings);
                await longinWindow.ShowDialog(this);
            }
            LoginUpdated();
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError(nameof(Start) + " error", ex);
        }

        return this;
    }

    public async void SetupMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + "." + nameof(SetupMenuItemClick), ActivityKind.Internal);
        try
        {
            var longinWindow = new LoginDialog(ihcDomain!.IhcSettings);
            await longinWindow.ShowDialog(this);
            LoginUpdated();
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError("LoginDialog error", ex);
        }
    }
      
    private void LoginUpdated()
    {
        ihcDomain?.UpdateSetup();
        this.Title = "IHC Lab : Endpoint set to " + ihcDomain?.IhcSettings.Endpoint ?? "(no endpoint set)";

        // Initialize ServicesComboBox with all IHC services
        // Create a wrapper to provide display names for services
        var serviceItems = ihcDomain!=null ? ihcDomain.AllIhcServices
            .Select(service => new ServiceItem(service))
            .ToList() : new List<ServiceItem>();

        ServicesComboBox.ItemsSource = serviceItems;
        ServicesComboBox.DisplayMemberBinding = new Avalonia.Data.Binding("DisplayName");

        // Select the first service by default
        if (serviceItems.Count > 0)
        {
            ServicesComboBox.SelectedIndex = 0;
        }
        
        // Do this last so Warning is not cleared unless we are testing.
        if (string.IsNullOrEmpty(Program.config?.telemetryConfig?.Host) && (Program.config?.ihcSettings?.Endpoint?.StartsWith(SpecialEndpoints.MockedPrefix) == false))
        {
            OpenTelemetryMenuItem.IsEnabled = false;
            SetWarning("OpenTelemtry not configured. It is recommended (but not requireds) to setup telemetry to view logs/traces. See guide in README for details.");
        }
    }

    public async void RunButtonClickHandler(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+nameof(RunButtonClickHandler), ActivityKind.Internal);

        ClearErrorAndWarning();
        ClearOutput();

        this.Cursor = new Cursor(StandardCursorType.Wait);

        try
        {
            activity?.SetParameters(
                (nameof(sender), sender),
                (nameof(e), e)
            );

            if (ServicesComboBox.SelectedItem is not ServiceItem serviceItem)
            {
                throw new Exception("No service selected.");
            }


            // Get ServiceOperationMetadata from OperationsComboBox
            if (OperationsComboBox.SelectedItem is not ServiceOperationMetadata operationMetadata)
            {
                throw new Exception("No operation selected.");
            }

            activity?.SetTag("ihcoperation", operationMetadata.Name);

            // Get parameter values from DynField controls
            var parameterValues = OperationSupport.GetParameterValues(ParametersPanel, operationMetadata.Parameters);

            string txt = await OperationSupport.DynCall(serviceItem.Service, operationMetadata, parameterValues);

            // Update result text view
            await SetOutput(txt, operationMetadata.ReturnType );
        } catch (Exception ex)
        {
           activity?.SetError(ex);
           SetError(nameof(RunButtonClickHandler) + " error", ex);
        } finally
        {
            this.Cursor = Cursor.Default;
        }
    }

    private void OnServicesComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
         using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+nameof(OnServicesComboBoxSelectionChanged), ActivityKind.Internal);

        ClearErrorAndWarning();
        ClearOutput();         
                
         try {
            if (ServicesComboBox.SelectedItem is ServiceItem serviceItem)
            {
                var operations = ServiceMetadata.GetOperations(serviceItem.Service);
                OperationsComboBox.ItemsSource = operations;
                OperationsComboBox.DisplayMemberBinding = new Avalonia.Data.Binding("Name");

                // Restore the previously selected operation index for this service
                if (operations.Count > 0)
                {
                    // Ensure the index is valid for the current operations list
                    int indexToSelect = serviceItem.InitialOperationSelectedIndex;
                    if (indexToSelect >= operations.Count)
                    {
                        indexToSelect = 0;
                    }
                    OperationsComboBox.SelectedIndex = indexToSelect;
                }
            }
            else
            {
                OperationsComboBox.ItemsSource = null;
            }
        } catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError(nameof(OnServicesComboBoxSelectionChanged) + " error", ex);        
            RunButton.IsEnabled = false;
        }
    }

    private void OnOperationsComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+ nameof(OnOperationsComboBoxSelectionChanged), ActivityKind.Internal);

        ClearErrorAndWarning();
        ClearOutput();
        
        try
        {
            // Save the selected operation index to the current service item
            if (ServicesComboBox.SelectedItem is ServiceItem serviceItem && OperationsComboBox.SelectedIndex >= 0)
            {
                serviceItem.InitialOperationSelectedIndex = OperationsComboBox.SelectedIndex;
            }

            // Update the operation description text
            if (OperationsComboBox.SelectedItem is ServiceOperationMetadata operationMetadata)
            {
                OperationDescription.Text = operationMetadata.Description;

                bool operationSupported = MetadataHelper.IsOperationSupported(operationMetadata);
                RunButton.IsEnabled = operationSupported;

                if (operationSupported)
                {
                   OperationSupport.SetUpParameterControls(ParametersPanel, operationMetadata);
                } else
                {
                   OperationSupport.ClearControls(ParametersPanel);
                   SetError("Support for executing this particular operation has not yet been implemented");
                }
            }
            else
            {
                OperationDescription.Text = string.Empty;
                RunButton.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError(nameof(OnOperationsComboBoxSelectionChanged) + " error", ex);
            RunButton.IsEnabled = false;
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Clean up IHC domain when window is closing
        ihcDomain?.Dispose();
    }

    public void ExitMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + "." + nameof(ExitMenuItemClick), ActivityKind.Internal);

        Close(); // Calls in turn OnWindowClosing which will dispose our IhcDomain.
    }

    public async void AboutMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+nameof(AboutMenuItemClick), ActivityKind.Internal);
        try
        {
            var aboutWindow = new AboutWindow();
            await aboutWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError("AboutMenu error", ex);
        }
    }

    public async void ShowSettingsMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+nameof(ShowSettingsMenuItemClick), ActivityKind.Internal);
        try
        {
            ClearErrorAndWarning();
            ClearOutput();

            // Convert IConfigurationSection to dictionary to properly serialize values
            var loggingConfigDict = Program.config?.loggingConfig?.GetChildren()
                .ToDictionary(
                    section => section.Key,
                    section => section.GetChildren().Any()
                        ? section.GetChildren().ToDictionary(child => child.Key, child => child.Value)
                        : (object?)section.Value
                );

            var settings = new
            {
                IhcSettings = Program.config?.ihcSettings,
                TelemetryConfiguration = Program.config?.telemetryConfig,
                LoggingConfig = loggingConfigDict
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(settings, options);
            await SetOutput(json, settings.GetType());
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError("Show settings error", ex);
        }
    }

    public void OpenTelemetryMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+nameof(OpenTelemetryMenuItemClick), ActivityKind.Internal);
        try
        {
            string? telemetryUrl = Program.config?.telemetryConfig?.Host;
            if (string.IsNullOrEmpty(telemetryUrl))
            {
                throw new NotSupportedException("Telemetry host not set");
            }

            var psi = new ProcessStartInfo
            {
                FileName = telemetryUrl,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError("Open telemetry in browser error", ex);
        }
    }

    public async void CopyOutputMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+nameof(CopyOutputMenuItemClick), ActivityKind.Internal);

        try
        {
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(Output.Text ?? string.Empty);
            } else throw new NotSupportedException("No clipboard available");
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError("Output to clipboard error", ex);
        }
    }

    public async void CopyErrorMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + "." + nameof(CopyErrorMenuItemClick), ActivityKind.Internal);

        try
        {
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(ErrorWarningContent.Text ?? string.Empty);
            }
            else throw new NotSupportedException("No clipboard available");
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError("Error to clipboard error", ex);
        }
    }
    
     public void ClearOutput()
    {
        Output.Text = "";
        OutputHeading.IsVisible = false;
    }

    public async Task SetOutput(string text, Type type)
    {
        Output.Text = text;
        OutputHeading.Text = $"Output (Size={text.Length}, Type={type.Name}):";
        OutputHeading.IsVisible = true;

        // For large content: disable wrapping and enable horizontal scroll
        // For small content: enable wrapping and disable horizontal scroll
        bool isLargeContent = text.Length > 10000;

        Output.TextWrapping = isLargeContent
            ? Avalonia.Media.TextWrapping.NoWrap
            : Avalonia.Media.TextWrapping.Wrap;

        // Get the parent ScrollViewer and update its scroll mode
        if (Output.Parent is ScrollViewer scrollViewer)
        {
            scrollViewer.HorizontalScrollBarVisibility = isLargeContent
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Disabled;
        }

        // Wait for UI to complete layout and rendering
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
    }

    public void ClearErrorAndWarning()
    {
        ErrorWarningContent.Text = "";
        ErrorHeading.IsVisible = false;
        WarningHeading.IsVisible = false;
    }

    public void SetError(string text, Exception? ex = null)
    {
        string txt = text ?? "";
        if (ex != null)
            txt = txt + ": " + ex.Source + " : " + ex.Message;

        ErrorWarningContent.Text = txt;
        ErrorHeading.IsVisible = true;

        logger.Log(LogLevel.Error, message: text, exception: ex);
    }
    
    public void SetWarning(string text, Exception? ex = null)
    {
        string txt = text ?? ""; 
        if (ex != null)
            txt = txt + ": " + ex.Source + " : " + ex.Message;

        ErrorWarningContent.Text = txt;
        WarningHeading.IsVisible = true;

        logger.Log(LogLevel.Warning, message: text, exception: ex);
    }
}