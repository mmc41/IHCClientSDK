using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
using Ihc.Vis;

namespace ihc_openvisual.Views;

/// <summary>
/// The enumerator types-and-values editor (US-030, uxparity2 W10/F12), shaped on the reference application's
/// <i>Bibliotek ▸ Rediger Enumerator typer</i> as measured 2026-08-04.
/// <para>
/// TWO panes: <i>Enumerator type</i> and <i>Enumerator værdier - &lt;type&gt;</i>, each with Ny / Slet / Omdøb, and a
/// single OK that closes. Every button applies IMMEDIATELY through <see cref="EnumTypeManagerInput.Apply"/> and the
/// panes are then re-read from the document — which is why there is no Cancel to offer.
/// </para>
/// <para>
/// The button gating is the vendor's, and it is not cosmetic: on a <c>[read only]</c> built-in type it greys
/// type-Slet, type-Omdøb <b>and all three value buttons</b>, leaving only type-Ny live. Enumerator selection alone
/// gates the rest, so a pane with nothing selected offers nothing but Ny.
/// </para>
/// </summary>
public partial class EnumTypeManagerWindow : Window
{
    private EnumTypeManagerInput _input = null!;
    private IReadOnlyList<EnumTypeView> _types = [];

    public EnumTypeManagerWindow()
    {
        InitializeComponent();
    }

    public static async Task ShowAsync(Window owner, EnumTypeManagerInput input)
    {
        var window = new EnumTypeManagerWindow { Title = input.Title, _input = input };
        window.Reload(selectType: null);
        await window.ShowDialog(owner);
    }

    /// <summary>The selected type, or null when the list is empty / nothing is picked.</summary>
    private EnumTypeView? SelectedType =>
        TypesList.SelectedIndex >= 0 && TypesList.SelectedIndex < _types.Count ? _types[TypesList.SelectedIndex] : null;

    /// <summary>Re-reads the project and rebuilds both panes, preferring to land the caret on
    /// <paramref name="selectType"/> (the type just created/renamed) so an edit does not throw the selection away.</summary>
    private void Reload(string? selectType)
    {
        _types = _input.Types();
        // The list shows DisplayName — the vendor's "[read only]" marker lives there, never in the stored name.
        TypesList.ItemsSource = _types.Select(t => t.DisplayName).ToList();
        int index = selectType is null ? -1 : _types.Select((t, i) => (t, i)).FirstOrDefault(p => p.t.Name == selectType).i;
        TypesList.SelectedIndex = _types.Count == 0 ? -1
            : index >= 0 && _types[index].Name == selectType ? index
            : 0;
        ReloadValues();
    }

    private void ReloadValues()
    {
        EnumTypeView? type = SelectedType;
        ValuesGroup.Header = type is null ? "Enumerator værdier" : $"Enumerator værdier - {type.DisplayName}";
        ValuesList.ItemsSource = type?.Values.ToList() ?? [];
        ValuesList.SelectedIndex = type is { Values.Length: > 0 } ? 0 : -1;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        EnumTypeView? type = SelectedType;
        bool editable = type is { IsReadOnly: false };

        NewTypeButton.IsEnabled = true;                     // always live, as in the vendor
        DeleteTypeButton.IsEnabled = editable;
        RenameTypeButton.IsEnabled = editable;

        NewValueButton.IsEnabled = editable;
        bool valuePicked = editable && ValuesList.SelectedIndex >= 0;
        DeleteValueButton.IsEnabled = valuePicked;
        RenameValueButton.IsEnabled = valuePicked;
    }

    private void OnTypeSelectionChanged(object? sender, SelectionChangedEventArgs e) => ReloadValues();

    private void OnValueSelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateButtons();

    private async void OnNewType(object? sender, RoutedEventArgs e)
    {
        if (await Prompt("Opret ny enumerator type", "Navn") is { } name)
        {
            await ApplyAndReload(new EnumTypeManagerOperation.NewType(name), selectType: name);
        }
    }

    private async void OnRenameType(object? sender, RoutedEventArgs e)
    {
        if (SelectedType is not { } type)
            return;
        if (await Prompt("Omdøb Enumerator type", type.Name) is { } name)
        {
            await ApplyAndReload(new EnumTypeManagerOperation.RenameType(type.Name, name), selectType: name);
        }
    }

    private async void OnDeleteType(object? sender, RoutedEventArgs e)
    {
        if (SelectedType is { } type)
        {
            await ApplyAndReload(new EnumTypeManagerOperation.DeleteType(type.Name), selectType: null);
        }
    }

    private async void OnNewValue(object? sender, RoutedEventArgs e)
    {
        if (SelectedType is not { } type)
            return;
        if (await Prompt("Opret ny enumerator værdi", "Navn") is { } name)
        {
            await ApplyAndReload(new EnumTypeManagerOperation.NewValue(type.Name, name), selectType: type.Name);
        }
    }

    private async void OnRenameValue(object? sender, RoutedEventArgs e)
    {
        if (SelectedType is not { } type || ValuesList.SelectedIndex is var index && index < 0)
            return;
        if (await Prompt("Omdøb Enumerator værdi", type.Values[index]) is { } name)
        {
            await ApplyAndReload(new EnumTypeManagerOperation.RenameValue(type.Name, index, name), selectType: type.Name);
        }
    }

    private async void OnDeleteValue(object? sender, RoutedEventArgs e)
    {
        if (SelectedType is { } type && ValuesList.SelectedIndex >= 0)
        {
            await ApplyAndReload(
                new EnumTypeManagerOperation.DeleteValue(type.Name, ValuesList.SelectedIndex), selectType: type.Name);
        }
    }

    private Task<string?> Prompt(string title, string initial) =>
        NamePromptWindow.ShowAsync(this, new NamePromptInput(title, initial, _input.Blank));

    // Reload REGARDLESS of the verdict: a refusal leaves the document untouched, but re-reading it is what proves
    // the panes still describe the project rather than the edit we hoped for. The view-model surfaces the reason.
    private async Task ApplyAndReload(EnumTypeManagerOperation operation, string? selectType)
    {
        await _input.Apply(operation);
        Reload(selectType);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
