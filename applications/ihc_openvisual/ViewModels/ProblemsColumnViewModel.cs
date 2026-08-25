using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ihc_openvisual.ViewModels;

/// <summary>Which column of the Problemer table a sort is keyed on. The order is the columns' screen order.</summary>
public enum ProblemsColumn
{
    /// <summary>Alvor — the severity tier.</summary>
    Severity,

    /// <summary>Kategori — the check family, by its Danish label.</summary>
    Category,

    /// <summary>Besked — the Danish sentence.</summary>
    Message,

    /// <summary>Element — the element name, or the raw locator where there is no element.</summary>
    Element,

    /// <summary>Kode — the finding's kebab-case code.</summary>
    Code,
}

/// <summary>
/// One sortable column header: its Danish title, the control that sorts by it, and the arrow saying which way it
/// is sorted right now.
///
/// <para>The header is a BUTTON rather than a click-handled label, and that is an accessibility decision rather
/// than a styling one: sorting has to be reachable without a mouse, and a button is the one thing that is
/// focusable, activatable with Enter/Space, and announced as something you can press. It carries its own command
/// so the template needs no binding up to an ancestor's DataContext.</para>
/// </summary>
public sealed partial class ProblemsColumnViewModel : ObservableObject
{
    internal ProblemsColumnViewModel(ProblemsColumn column, string title, Action<ProblemsColumn> sort)
    {
        Column = column;
        Title = title;
        SortCommand = new RelayCommand(() => sort(column));
    }

    /// <summary>Which column this header sorts by.</summary>
    public ProblemsColumn Column { get; }

    /// <summary>The Danish column title.</summary>
    public string Title { get; }

    /// <summary>
    /// The header's stable id, e.g. <c>problems.sort.element</c>. Lower-cased from the enum member so the
    /// vocabulary a driver types follows the column set automatically rather than being a second hand-kept list.
    /// </summary>
    public string AutomationId => "problems.sort." + Column.ToString().ToLowerInvariant();

    /// <summary>Sorts the panel by this column, reversing the direction if it is already the sorted one.</summary>
    public IRelayCommand SortCommand { get; }

    /// <summary>
    /// The direction arrow, or empty when this is not the sorted column — so exactly one header ever claims to
    /// say how the list is ordered.
    /// </summary>
    [ObservableProperty] private string _sortGlyph = string.Empty;

    /// <summary>
    /// Points this header at the sort the panel now holds. Pushed rather than pulled back through a pair of
    /// delegates: the panel already tells every header when the sort moves, so a header that also reached back
    /// for the answer was the same fact travelling in both directions.
    /// </summary>
    /// <param name="sortedColumn">The column the list is ordered by.</param>
    /// <param name="ascending">Which way that column is ordered.</param>
    internal void ShowSort(ProblemsColumn sortedColumn, bool ascending) =>
        SortGlyph = sortedColumn != Column ? string.Empty : ascending ? "▲" : "▼";

    public override string ToString() => Title;
}
