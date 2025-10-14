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
        var parameterValues = GetParameterValues(operationMetadata.Parameters);

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
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
        };
        ToolTip.SetTip(legend, $"Type: {field.Type.Name}: {field.Description}");

        if (field.SubTypes.Length==0)
        {
            parent.Children.Add(legend);

            // Create a DynField control for this parameter
            var dynField = new DynField
            {
                TypeForControl = field.Type.Name,
                Margin = new Thickness(0, 0, 20, 15),
                Name = prefix + field.Name,
                Tag = field
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

    private object[] GetParameterValues(FieldMetaData[] parameters)
    {
        var values = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            values[i] = GetFieldValue(ParametersPanel, parameter, string.Empty) ?? throw new InvalidOperationException($"Failed to get value for parameter {parameter.Name}");
        }

        return values;
    }

    private object? GetFieldValue(Panel parent, FieldMetaData field, string prefix)
    {
        string fullName = prefix + field.Name;

        // For simple types (primitives and string), find the DynField control and get its value
        if (field.IsSimple)
        {
            var dynField = FindDynFieldByName(parent, fullName);
            if (dynField != null)
            {
                return dynField.Value ?? GetDefaultValue(field.Type);
            }
            return GetDefaultValue(field.Type);
        }

        // For arrays, handle specially
        if (field.IsArray)
        {
            // For now, return empty array of the element type
            // TODO: Implement array handling with dynamic UI elements
            var elementType = field.Type.GetElementType();
            if (elementType != null)
            {
                return Array.CreateInstance(elementType, 0);
            }
            return Array.Empty<object>();
        }

        // For complex types (records/classes with subtypes)
        if (field.SubTypes.Length > 0)
        {
            // Create an instance of the type
            var instance = Activator.CreateInstance(field.Type);
            if (instance == null)
            {
                return GetDefaultValue(field.Type);
            }

            // Set each property from the subtypes
            foreach (var subField in field.SubTypes)
            {
                var subValue = GetFieldValue(parent, subField, fullName + ".");
                var property = field.Type.GetProperty(subField.Name);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(instance, subValue);
                }
            }

            return instance;
        }

        // Default: return default value for the type
        return GetDefaultValue(field.Type);
    }

    private DynField? FindDynFieldByName(Panel parent, string name)
    {
        // Recursively search for DynField with the given name
        foreach (var child in parent.Children)
        {
            if (child is DynField dynField && dynField.Name == name)
            {
                return dynField;
            }

            if (child is Panel childPanel)
            {
                var found = FindDynFieldByName(childPanel, name);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private object? GetDefaultValue(Type type)
    {
        if (type == typeof(string))
            return string.Empty;
        if (type.IsValueType)
            return Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Failed to create instance of {type.Name}");
        return null;
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