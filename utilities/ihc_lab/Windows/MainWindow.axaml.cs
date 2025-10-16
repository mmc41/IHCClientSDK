using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ihc;

namespace ihc_lab;

public partial class MainWindow : Window
{
    private IhcDomain? ihcDomain;

    public MainWindow()
    {
        try
        {
            ihcDomain = new IhcDomain();
                    
            using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            InitializeComponent();
            DataContext = this;

            // Handle window closing event (when user clicks X button)
            Closing += OnWindowClosing;

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
        }
        catch (Exception ex)
        {
            SetError(nameof(MainWindow) + " constructor error", ex);
            RunButton.IsEnabled = false;
        }
    }

    public void RunButtonClickHandler(object sender, RoutedEventArgs e)
    {
        ClearError();

        try
        {
            using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(
                (nameof(sender), sender),
                (nameof(e), e)
            );

            // Get ServiceOperationMetadata from OperationsComboBox
            if (OperationsComboBox.SelectedItem is not ServiceOperationMetadata operationMetadata)
            {
                output.Text = "No operation selected.";
                return;
            }

            activity?.SetTag("ihcoperation", operationMetadata.Name);

            // Get parameter values from DynField controls
            var parameterValues = OperationSupport.GetParameterValues(ParametersPanel, operationMetadata.Parameters);

            String txt = operationMetadata.Name + " : " + String.Join(",", parameterValues.Select(p => p.ToString()));

            // Update text of TextBlock with name output
            output.Text = txt;
        } catch (Exception ex)
        {
           SetError(nameof(RunButtonClickHandler) +" error", ex);        
        }
    }

    private void OnServicesComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
         ClearError();
                
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
            SetError(nameof(OnServicesComboBoxSelectionChanged) + " error", ex);        
            RunButton.IsEnabled = false;
        }
    }

    private void OnOperationsComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ClearError();
        
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
                OperationSupport.SetUpParameterControls(ParametersPanel, operationMetadata);

                bool operationSupported = MetadataHelper.IsOperationSupported(operationMetadata);
                RunButton.IsEnabled = operationSupported;
                if (!operationSupported)
                {
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
        Close(); // Calls in turn OnWindowClosing which will dispose our IhcDomain.
    }

    public void ClearError()
    {
        ErrorContent.Text = "";
    }

    public void SetError(string text, Exception? ex = null)
    {
        string txt = text ?? "";
        if (ex != null)
            txt = txt + ": " + ex.Source + " : " + ex.Message;

        ErrorContent.Text = txt;
    }
}