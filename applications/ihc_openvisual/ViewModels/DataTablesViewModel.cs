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
public partial class DataTablesViewModel : ViewModelBase, ihc_openvisual.Services.IDataTablesDialogViewModel
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
        await ApplyAndReloadAsync(_session.Commands.AddUserText(project, result.Name.Trim()));
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
        await ApplyAndReloadAsync(_session.Commands.UpdateUserText(project, id, result.Name.Trim()));
    }

    [RelayCommand]
    private async Task DeleteText()
    {
        if (SelectedUserText is not { } selected || !ElementId.TryParse(selected.Id, out ElementId id)
            || _session.Current is not { } project)
            return;
        if (!await _dialogs.ConfirmAsync("Delete text", $"Delete the text '{selected.Text}'?"))
            return;
        await ApplyAndReloadAsync(_session.Commands.DeleteUserText(project, id));
    }

    // Applies a user-text command and surfaces its outcome (T021): a committed edit reloads the tables; a refused or
    // failed edit is reported to the installer. A dialog view-model has no status bar, so the old inline
    // Status==Committed checks silently swallowed a refusal (e.g. the selected row was deleted from under the dialog);
    // this mirrors the main view-model's rule of never dropping a non-committed outcome on the floor. NoChange needs
    // no report -- nothing changed and nothing failed.
    private async Task ApplyAndReloadAsync(ProjectCommand command)
    {
        EditOutcome outcome = await _session.ApplyAsync(command);
        switch (outcome.Status)
        {
            case EditStatus.Committed:
                Reload();
                break;
            case EditStatus.Refused:
                await _dialogs.ShowMessageAsync("Cannot edit", outcome.Reason ?? "The edit was refused.");
                break;
            case EditStatus.Failed:
                await _dialogs.ShowMessageAsync("Edit failed", outcome.Reason ?? "The edit failed.");
                break;
        }
    }
}
