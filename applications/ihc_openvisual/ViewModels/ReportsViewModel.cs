using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.Services;
using ihc_openvisual.Services.Reporting;
using Ihc.Vis;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// The Reports view's view-model (US-040 / D14 / T021): owns the report session-state — the SDK's combined
/// project-documentation model for the open project plus the on-screen / printer variant — and renders it as ONE
/// navigable HTML document. It replaces the six direct report commands. It renders and toggles; it computes nothing
/// (all report business logic is in the SDK's <see cref="ProjectDocumentationReport"/> model). Implements the
/// <see cref="IReportsDialogViewModel"/> Services seam so the dialog service stays uncoupled from this concrete type.
/// </summary>
public sealed partial class ReportsViewModel : ObservableObject, IReportsDialogViewModel
{
    private readonly ProjectDocumentationReport _report;
    private readonly Func<string, string, Task<string?>> _writeHtml;
    private readonly Func<string, Task> _openUrl;

    public ReportsViewModel(ProjectDocumentationReport report,
        Func<string, string, Task<string?>> writeHtml, Func<string, Task> openUrl)
    {
        _report = report;
        _writeHtml = writeHtml;
        _openUrl = openUrl;
        // Content sections and value-carrying detail options default ON; internal ids default OFF. The Reports view
        // owns these toggles for the session (US-071).
        ShowInstallation = ShowEndUser = ShowFunctionBlocks = true;
        ShowWireColours = ShowLinkDisplay = ShowFunctionDocs = ShowEmptyFields = true;
    }

    /// <summary>Whether the printer-friendly variant is selected — it drops the on-screen navigation (overview,
    /// section anchors, back-to-top). False = the on-screen navigable variant.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Html))]
    public partial bool IsPrint { get; set; }

    // US-071 content-section switches — an OFF section emits nothing.
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Html))] public partial bool ShowInstallation { get; set; }
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Html))] public partial bool ShowEndUser { get; set; }
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Html))] public partial bool ShowFunctionBlocks { get; set; }

    // US-071 detail options — applied within the sections that are on.
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Html))] public partial bool ShowInternalIds { get; set; }
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Html))] public partial bool ShowWireColours { get; set; }
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Html))] public partial bool ShowLinkDisplay { get; set; }
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Html))] public partial bool ShowFunctionDocs { get; set; }
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Html))] public partial bool ShowEmptyFields { get; set; }

    private ReportOptions Options => new(ShowInstallation, ShowEndUser, ShowFunctionBlocks,
        ShowInternalIds, ShowWireColours, ShowLinkDisplay, ShowFunctionDocs, ShowEmptyFields);

    /// <summary>Seeds every switch from a named purpose preset (US-040/T030); the user can still adjust individual
    /// toggles afterwards. Each assignment re-renders <see cref="Html"/>.</summary>
    [RelayCommand]
    private void ApplyPreset(ReportPreset preset)
    {
        ReportOptions o = ReportHtmlRenderer.ForPreset(preset);
        ShowInstallation = o.ShowInstallation;
        ShowEndUser = o.ShowEndUser;
        ShowFunctionBlocks = o.ShowFunctionBlocks;
        ShowInternalIds = o.ShowInternalIds;
        ShowWireColours = o.ShowWireColours;
        ShowLinkDisplay = o.ShowLinkDisplay;
        ShowFunctionDocs = o.ShowFunctionDocs;
        ShowEmptyFields = o.ShowEmptyFields;
    }

    /// <summary>The rendered combined HTML for the current variant + switches — one navigable document on screen
    /// (overview / section-jump / back-to-top), tailored by the section/detail toggles (US-071).</summary>
    public string Html => ReportHtmlRenderer.RenderProjectDocumentation(_report, IsPrint, Options);

    /// <summary>Writes the current HTML to a temp file and opens it in the standard browser (US-040).</summary>
    [RelayCommand]
    private async Task OpenInBrowser()
    {
        string stem = IsPrint ? "projektdokumentation-print" : "projektdokumentation";
        if (await _writeHtml(stem, Html) is { } path)
        {
            await _openUrl(new Uri(path).AbsoluteUri);
        }
    }
}
