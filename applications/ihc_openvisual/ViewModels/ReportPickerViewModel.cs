using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.Services;
using Ihc.Vis;

namespace ihc_openvisual.ViewModels;

/// <summary>One report-type choice in the picker's dropdown; displays as its Danish report title.</summary>
public sealed record ReportKindOption(ReportKind Kind, string Title)
{
    public override string ToString() => Title;
}

/// <summary>
/// The shared report picker (R12/D4): report-type dropdown (pre-selected per the invoking menu entry),
/// Standard/Fuld mode choice, and the [Vis i browser] action delegating to the workflow's
/// facade-generate → temp file → browser flow. Avalonia-free and project-free: it holds only the chosen
/// kind/mode and the action delegate.
/// </summary>
public sealed partial class ReportPickerViewModel : ObservableObject, IReportPickerViewModel
{
    private readonly Func<ReportKind, ReportMode, Task> _viewInBrowser;
    private readonly Func<ReportKind, ReportMode, Task> _saveAs;

    public ReportPickerViewModel(ReportKind preselected,
        Func<ReportKind, ReportMode, Task> viewInBrowser, Func<ReportKind, ReportMode, Task> saveAs)
    {
        _viewInBrowser = viewInBrowser ?? throw new ArgumentNullException(nameof(viewInBrowser));
        _saveAs = saveAs ?? throw new ArgumentNullException(nameof(saveAs));
        _selectedKind = Kinds.Single(option => option.Kind == preselected);
    }

    /// <summary>The three reports, in the fixed menu order, labelled with the SDK's own report titles so a
    /// dropdown entry always reads exactly like the heading of the document it generates.</summary>
    public IReadOnlyList<ReportKindOption> Kinds { get; } = new[]
    {
        ReportKind.Functions, ReportKind.Installation, ReportKind.FunctionBlocks,
    }.Select(kind => new ReportKindOption(kind, ReportTitles.For(kind))).ToArray();

    [ObservableProperty] private ReportKindOption _selectedKind;

    [ObservableProperty] private bool _isFullMode;

    /// <summary>The Standard radio's binding target — the inverse of <see cref="IsFullMode"/>.</summary>
    public bool IsStandardMode
    {
        get => !IsFullMode;
        set => IsFullMode = !value;
    }

    partial void OnIsFullModeChanged(bool value) => OnPropertyChanged(nameof(IsStandardMode));

    /// <summary>[Vis i browser]: generate the picked kind × mode as HTML and open it (US-063 flow).</summary>
    [RelayCommand]
    private Task ViewInBrowser() =>
        _viewInBrowser(SelectedKind.Kind, IsFullMode ? ReportMode.Full : ReportMode.Standard);

    /// <summary>[Gem som…]: pick a target file (.html/.txt) and generate the picked kind × mode to it.</summary>
    [RelayCommand]
    private Task SaveAs() =>
        _saveAs(SelectedKind.Kind, IsFullMode ? ReportMode.Full : ReportMode.Standard);
}
