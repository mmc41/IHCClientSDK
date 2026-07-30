using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;

namespace ihc_openvisual.Views;

/// <summary>
/// The Reports view (US-040 / T021): the single project-documentation window that replaces the six direct report
/// commands. It binds to the app's <c>ReportsViewModel</c> (through the <see cref="IReportsDialogViewModel"/> seam),
/// offering the on-screen / printer-friendly toggle, an actual HTML view (<see cref="NativeWebView"/>) of the
/// rendered combined document, and <i>Open in browser</i>. The report content is produced entirely by the
/// view-model / SDK; this view is responsible only for pushing it into the web view on every change, since
/// <see cref="NativeWebView"/> has no bindable HTML-content property.
/// </summary>
public partial class ReportsWindow : Window
{
    private static readonly Uri BaseUri = new("about:blank");

    private INotifyPropertyChanged? _viewModel;

    public ReportsWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => HookViewModel();
    }

    public static async Task ShowAsync(Window owner, IReportsDialogViewModel viewModel)
    {
        var window = new ReportsWindow { DataContext = viewModel };
        await window.ShowDialog(owner);
    }

    private void HookViewModel()
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = DataContext as INotifyPropertyChanged;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RenderHtml();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(ReportsViewModel.Html))
            RenderHtml();
    }

    private void RenderHtml()
    {
        if (DataContext is ReportsViewModel viewModel)
            ReportView.NavigateToString(viewModel.Html, BaseUri);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
