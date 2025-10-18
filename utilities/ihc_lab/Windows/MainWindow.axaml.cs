using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Ihc;

namespace IhcLab;

public partial class MainWindow : Window
{
    private IhcDomain? ihcDomain;
    private IClipboard? clipboard;

    public MainWindow()
    {
       using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"Ctr", ActivityKind.Internal);
                    
       try
       {
            InitializeComponent();
            DataContext = this;

            ihcDomain = new IhcDomain();
            clipboard = this.Clipboard;

            if (clipboard == null)
            {
                CopyOutputMenuItem.IsEnabled = false;
                CopyErrorMenuItem.IsEnabled = false;
            }

            // Handle window closing event (when user clicks X button)
            Closing += OnWindowClosing;

            this.Title = "IHC Lab : Connected to " + ihcDomain.IhcSettings.Endpoint ?? "(no endpoint set)";

            // Initialize ServicesComboBox with all IHC services
            // Create a wrapper to provide display names for services
            var serviceItems = ihcDomain.AllIhcServices
                .Select(service => new ServiceItem(service))
                .ToList();

            ServicesComboBox.ItemsSource = serviceItems;
            ServicesComboBox.DisplayMemberBinding = new Avalonia.Data.Binding("DisplayName");

            // Select the first service by default
            if (serviceItems.Count > 0)
            {
                ServicesComboBox.SelectedIndex = 0;
            }

            // Do this last so Warning is not cleared.
            if (string.IsNullOrEmpty(Program.config?.telemetryConfig?.Host))
            {
                OpenTelemetryMenuItem.IsEnabled = false;
                SetWarning("OpenTelemtry not configured. It is recommended (but not requireds) to setup telemetry to view logs/traces. See guide in README for details.");
            }
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError(nameof(MainWindow) + " constructor error", ex);
            RunButton.IsEnabled = false;
        }
    }

    public async void RunButtonClickHandler(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(RunButtonClickHandler), ActivityKind.Internal);

        ClearErrorAndWarning();
        ClearOutput();

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
            SetOutput(txt);
        } catch (Exception ex)
        {
           activity?.SetError(ex);
           SetError(nameof(RunButtonClickHandler) + " error", ex);
        }
    }

    private void OnServicesComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
         using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(OnServicesComboBoxSelectionChanged), ActivityKind.Internal);

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
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(OnOperationsComboBoxSelectionChanged), ActivityKind.Internal);

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
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(ExitMenuItemClick), ActivityKind.Internal);

        Close(); // Calls in turn OnWindowClosing which will dispose our IhcDomain.
    }

    public async void AboutMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(AboutMenuItemClick), ActivityKind.Internal);
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

    public void OpenTelemetryMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(OpenTelemetryMenuItemClick), ActivityKind.Internal);
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
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(CopyOutputMenuItemClick), ActivityKind.Internal);

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
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(CopyErrorMenuItemClick), ActivityKind.Internal);

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

    public void SetOutput(string text)
    {
        Output.Text = text;
        OutputHeading.IsVisible = true;
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
    }
    
    public void SetWarning(string text, Exception? ex = null)
    {
        string txt = text ?? ""; 
        if (ex != null)
            txt = txt + ": " + ex.Source + " : " + ex.Message;

        ErrorWarningContent.Text = txt;
        WarningHeading.IsVisible = true;
    }
}