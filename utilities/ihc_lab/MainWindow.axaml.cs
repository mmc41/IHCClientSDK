using System.Linq;
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
        System.Console.WriteLine("Run button clicked!");

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
    }

    public void ExitMenuItemClick(object sender, RoutedEventArgs e)
    {
        IhcDomain.DisposeIhcDomain();
        Close();
    }
}