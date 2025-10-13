using System;
using System.Linq;
using System.Reflection.Metadata;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ihc;

namespace ihc_lab;

public partial class MainWindow : Window
{
    private IhcDomain ihcDomain;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        ihcDomain = IhcDomain.GetOrCreateIhcDomain();

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

    public void RunButtonClickHandler(object sender, RoutedEventArgs e)
    {
        // Get ServiceOperationMetadata from OperationsComboBox
        if (OperationsComboBox.SelectedItem is not ServiceOperationMetadata operationMetadata)
        {
            output.Text = "No operation selected.";
            return;
        }

        // Get parameter values from DynField controls
        var parameterValues = GetParameterValues();

        String txt = operationMetadata.Name + " : " + String.Join(",", parameterValues.Select(p => p.ToString()));

        // Update text of TextBlock with name output
        output.Text = txt;
    }

    private void OnServicesComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
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
    }

    private void OnOperationsComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
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
            SetUpParamterControls(operationMetadata);
        }
        else
        {
            OperationDescription.Text = string.Empty;
        }

    }

    private void AddFieldControls(Panel parent, FieldMetaData field, string prefix)
    {
        // Add legend (TextBlock label) for this parameter
        var legend = new TextBlock
        {
            Text = field.Name + ":",
            Margin = new Thickness(0, 10, 5, 2),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        };

        if (field.SubTypes.Length==0)
        {
            parent.Children.Add(legend);
                    
            // Map .NET type names to DynField control types
            string dynFieldType = TypeHelper.MapTypeToControlType(field.Type);

            // Create a DynField control for this parameter
            var dynField = new DynField
            {
                TypeForControl = dynFieldType,
                Margin = new Thickness(0, 0, 20, 15),
                Name = prefix+field.Name
            };

            // Add the control to the ParametersPanel
            parent.Children.Add(dynField);
        } else { // Complex types like records or arrays.
            // Wrap these controls in a dynamically created StackPanel with horizontal orientation
            var stackPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            parent.Children.Add(stackPanel);

            stackPanel.Children.Add(legend);
            foreach (var subField in field.SubTypes)
            {
                AddFieldControls(stackPanel, subField, field.Name + ".");
            }
        } 
    }

    private void SetUpParamterControls(ServiceOperationMetadata operationMetadata)
    {
        // Clear any existing child controls under panel with Name ParametersPanel
        ParametersPanel.Children.Clear();

        foreach (FieldMetaData parameter in operationMetadata.Parameters)
        {
            AddFieldControls(ParametersPanel, parameter, "");
        }
    }

    private object[] GetParameterValues()
    {
        // Retrieve parameter values as array from DynField children of ParametersPanel
        var dynFields = ParametersPanel.Children.OfType<DynField>().ToArray();
        var values = new object[dynFields.Length];

        for (int i = 0; i < dynFields.Length; i++)
        {
            values[i] = dynFields[i].Value ?? string.Empty;
        }

        return values;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Clean up IHC domain when window is closing
        IhcDomain.DisposeIhcDomain();
    }

    public void ExitMenuItemClick(object sender, RoutedEventArgs e)
    {
        Close(); // Calls in turn OnWindowClosing which will dispose our IhcDomain.
    }
}