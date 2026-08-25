using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Ihc.Vis.Validation;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// One severity tier as the Problemer panel's chrome shows it: its Danish word, its glyph, how many findings the
/// result holds in it, and whether its rows are currently listed.
///
/// <para>A tier is a ROW OF DATA rather than three properties per tier on the panel, for the same reason
/// <see cref="ProblemsColumnViewModel"/> is: the alternative had the word, the asset path, the count and the
/// toggle written out once per tier in the view-model and AGAIN in the markup, so the label a filter button shows
/// and the label its rows show were two independent copies that nothing compared.</para>
///
/// <para><b>A filter hides ROWS and nothing else.</b> <see cref="Count"/> is of the whole bound result, never of
/// the visible rows — switching a tier off must never look like its findings were fixed. That is also why there
/// is no way to reach a severity through this type other than as a filtering and grouping key.</para>
/// </summary>
public sealed partial class ProblemsTierViewModel : ObservableObject
{
    private readonly Action _filterChanged;

    internal ProblemsTierViewModel(ValidationSeverity severity, string helpText, Action filterChanged)
    {
        Severity = severity;
        HelpText = helpText;
        _filterChanged = filterChanged;
    }

    /// <summary>The tier this row is about.</summary>
    public ValidationSeverity Severity { get; }

    /// <summary>The tier's Danish word — the same one its rows carry, read from the one place that names it.</summary>
    public string Label => ProblemsPanelViewModel.SeverityLabel(Severity);

    /// <summary>The tier's icon asset — again the same one its rows carry.</summary>
    public string Icon => ProblemsPanelViewModel.SeverityIcon(Severity);

    /// <summary>What the toggle announces it does.</summary>
    public string HelpText { get; }

    /// <summary>
    /// The toggle's stable id, e.g. <c>problems.filter.error</c>. Lower-cased from the tier so the vocabulary a
    /// driver types follows the severity set automatically rather than being a second hand-kept list.
    /// </summary>
    public string AutomationId => "problems.filter." + Severity.ToString().ToLowerInvariant();

    /// <summary>The count's own id, e.g. <c>problems.count.error</c>, so a driver can read the number back.</summary>
    public string CountAutomationId => "problems.count." + Severity.ToString().ToLowerInvariant();

    /// <summary>How many findings the BOUND RESULT has in this tier — not how many are currently listed.</summary>
    [ObservableProperty] private int _count;

    /// <summary>
    /// Whether this tier's rows are listed. Session-only, and true by default: a finding nobody can see is a
    /// finding nobody acts on, so the panel starts by showing everything and lets a user narrow it.
    /// </summary>
    [ObservableProperty] private bool _isShown = true;

    partial void OnIsShownChanged(bool value) => _filterChanged();

    public override string ToString() => Label;
}
