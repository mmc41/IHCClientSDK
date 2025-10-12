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
    }

    private void OnServicesComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ServicesComboBox.SelectedItem is ServiceItem serviceItem)
        {
            var operations = serviceItem.Service.GetOperations();
            OperationsComboBox.ItemsSource = operations;
            OperationsComboBox.DisplayMemberBinding = new Avalonia.Data.Binding("Name");
        }
        else
        {
            OperationsComboBox.ItemsSource = null;
        }
    }

    public void ExitMenuItemClick(object sender, RoutedEventArgs e)
    {
        IhcDomain.DisposeIhcDomain();
        Close();
    }
}