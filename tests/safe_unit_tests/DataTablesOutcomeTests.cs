using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace safe_unit_tests;

/// <summary>
/// T021 (US-049 consistency): a Data-tables edit whose command is <b>refused</b> must be surfaced to the installer,
/// not silently swallowed. <see cref="DataTablesViewModel"/> is Avalonia-free view-model logic, so it is exercised
/// here headlessly over a real (controller-free) <see cref="ProjectWorkflow"/> with a recording dialog service.
/// </summary>
public class DataTablesOutcomeTests
{
    // A compact IDialogService double: records the last message shown and answers the delete-confirm. Every other
    // member is an inert default — this test only drives the delete path.
    private sealed class RecordingDialogService : IDialogService
    {
        public string? LastMessage { get; private set; }
        public bool ConfirmResult { get; set; }
        public Task ShowMessageAsync(string title, string message) { LastMessage = message; return Task.CompletedTask; }
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(ConfirmResult);
        public Task<SaveChangesResult> ConfirmSaveChangesAsync(string documentName) => Task.FromResult(SaveChangesResult.Discard);
        public Task<string?> PickOpenProjectAsync(string? initialDirectory) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveProjectAsync(string? initialDirectory, string suggestedFileName) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFunctionBlockAsync(string suggestedFileName) => Task.FromResult<string?>(null);
        public Task ShowAboutAsync() => Task.CompletedTask;
        public Task ShowSettingsAsync(string settingsText) => Task.CompletedTask;
        public Task OpenExternalUrlAsync(string url) => Task.CompletedTask;
        public Task<PropertiesResult?> EditPropertiesAsync(string title, string name, string note, LibraryOrigin? origin = null, string affirmative = "OK") => Task.FromResult<PropertiesResult?>(null);
        public Task<VariablePropertiesResult?> EditVariablePropertiesAsync(VariablePropertiesInput input) => Task.FromResult<VariablePropertiesResult?>(null);
        public Task ShowReportPickerAsync(IReportPickerViewModel viewModel) => Task.CompletedTask;
        public Task<string?> PickSaveReportAsync(string suggestedFileName, string mimeType) => Task.FromResult<string?>(null);
        public Task<ProductPropertiesResult?> EditProductPropertiesAsync(ProductPropertiesInput input) => Task.FromResult<ProductPropertiesResult?>(null);
        public Task<SceneContainerResult?> EditSceneContainerAsync(SceneContainerInput input) => Task.FromResult<SceneContainerResult?>(null);
        public Task<PinPropertiesResult?> EditPinPropertiesAsync(PinPropertiesInput input, System.Func<PinPropertiesResult, System.Threading.Tasks.Task>? onApply = null) => Task.FromResult<PinPropertiesResult?>(null);
        public Task<ModemPropertiesResult?> EditModemPropertiesAsync(ModemPropertiesInput input) => Task.FromResult<ModemPropertiesResult?>(null);
        public Task<AdvancedDimmerResult?> EditAdvancedDimmerAsync(AdvancedDimmerInput input) => Task.FromResult<AdvancedDimmerResult?>(null);
        public Task<SceneValueResult?> EditSceneValueAsync(SceneValueInput input) => Task.FromResult<SceneValueResult?>(null);
        public Task<EnumDefinitionResult?> EditEnumDefinitionAsync(EnumDefinitionInput input) => Task.FromResult<EnumDefinitionResult?>(null);
        public Task<EnumTypeManagerResult?> ManageEnumTypesAsync(EnumTypeManagerInput input) => Task.FromResult<EnumTypeManagerResult?>(null);
        public Task<ProjectInfoData?> EditProjectInfoAsync(ProjectInfoData current) => Task.FromResult<ProjectInfoData?>(null);
        public Task ShowDataTablesAsync(IDataTablesDialogViewModel viewModel) => Task.CompletedTask;
        public Task ShowModuleMapAsync(DatalineModuleMap map) => Task.CompletedTask;
        public Task<string?> PickCatalogFileAsync() => Task.FromResult<string?>(null);
        public Task<string?> PickCatalogFolderAsync() => Task.FromResult<string?>(null);
    }

    [Test]
    public async Task DeleteText_RefusedEdit_IsSurfacedNotSwallowed()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ihc_ov_unit", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dialogs = new RecordingDialogService();
        var session = new ProjectWorkflow(
            new ProjectAppService(new IhcSettings()),
            new BackupService(Path.Combine(tempDir, "recovery")),
            new RecentProjectsStore(Path.Combine(tempDir, "recent.json")),
            dialogs,
            catalogDir: Path.Combine(tempDir, "catalog"));
        try
        {
            await session.NewAsync();

            // Add a real user text, then delete it out of band so the row the view-model still points at is stale.
            EditOutcome add = await session.ApplyAsync(session.Commands.AddUserText(session.Current!, "Doomed"));
            Assert.That(add.Status, Is.EqualTo(EditStatus.Committed), "precondition: the text is added");

            var dt = new DataTablesViewModel(session, dialogs);
            dt.SelectedUserText = dt.UserTexts.Single();
            Assert.That(ElementId.TryParse(dt.SelectedUserText!.Id, out ElementId staleId), Is.True);
            EditOutcome removed = await session.ApplyAsync(session.Commands.DeleteUserText(session.Current!, staleId));
            Assert.That(removed.Status, Is.EqualTo(EditStatus.Committed), "precondition: the text is deleted out of band");

            // The view-model's Delete now targets a row that no longer exists -> the command is refused.
            dialogs.ConfirmResult = true;
            await dt.DeleteTextCommand.ExecuteAsync(null);

            Assert.That(dialogs.LastMessage, Is.Not.Null,
                "a refused data-table edit is surfaced to the installer, not silently swallowed");
        }
        finally
        {
            session.Dispose();
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort temp cleanup */ }
        }
    }
}
