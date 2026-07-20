using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.Services;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace ihc_openvisual.ViewModels;

/// <summary>One editable user-defined text row (US-049): its element id token and its (bindable) text.</summary>
public sealed record UserTextItem(string Id, string Text);

/// <summary>
/// Backs the Data tables dialog (US-049): shows the read-only system tables and the editable user-defined texts,
/// with Add/Edit/Delete over the texts. Avalonia-free so it is headlessly testable; the window is a thin view over it.
/// The vendor deletes a text with no confirmation — per the story R-note this view-model guards Delete with an
/// app-level confirm.
/// </summary>
public partial class DataTablesViewModel : ViewModelBase
{
    private readonly ProjectWorkflow _session;
    private readonly IDialogService _dialogs;

    public ObservableCollection<DataTableView> SystemTables { get; } = new();
    public ObservableCollection<UserTextItem> UserTexts { get; } = new();

    [ObservableProperty] private UserTextItem? _selectedUserText;

    public DataTablesViewModel(ProjectWorkflow session, IDialogService dialogs)
    {
        _session = session;
        _dialogs = dialogs;
        Reload();
    }

    public void Reload()
    {
        DataTablesModel model = _session.GetDataTables();
        SystemTables.Clear();
        foreach (DataTableView table in model.SystemTables)
            SystemTables.Add(table);
        UserTexts.Clear();
        foreach (UserText text in model.UserTexts)
            UserTexts.Add(new UserTextItem(text.Id, text.Text));
    }

    [RelayCommand]
    private async Task AddText()
    {
        PropertiesResult? result = await _dialogs.EditPropertiesAsync("New user-defined text", string.Empty, string.Empty);
        if (result is null || string.IsNullOrWhiteSpace(result.Name) || _session.Current is not { } project)
            return;
        if ((await _session.ApplyAsync(_session.Commands.AddUserText(project, result.Name.Trim()))).Status == EditStatus.Committed)
            Reload();
    }

    [RelayCommand]
    private async Task EditText()
    {
        if (SelectedUserText is not { } selected || !ElementId.TryParse(selected.Id, out ElementId id)
            || _session.Current is not { } project)
            return;
        PropertiesResult? result = await _dialogs.EditPropertiesAsync("Edit user-defined text", selected.Text, string.Empty);
        if (result is null || string.IsNullOrWhiteSpace(result.Name))
            return;
        if ((await _session.ApplyAsync(_session.Commands.UpdateUserText(project, id, result.Name.Trim()))).Status == EditStatus.Committed)
            Reload();
    }

    [RelayCommand]
    private async Task DeleteText()
    {
        if (SelectedUserText is not { } selected || !ElementId.TryParse(selected.Id, out ElementId id)
            || _session.Current is not { } project)
            return;
        if (!await _dialogs.ConfirmAsync("Delete text", $"Delete the text '{selected.Text}'?"))
            return;
        if ((await _session.ApplyAsync(_session.Commands.DeleteUserText(project, id))).Status == EditStatus.Committed)
            Reload();
    }
}
