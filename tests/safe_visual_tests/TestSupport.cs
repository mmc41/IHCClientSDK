using System;
using System.IO;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;

namespace safe_visual_tests;

/// <summary>A scriptable <see cref="IDialogService"/> for headless tests: canned answers for the save prompt,
/// recovery confirm, and file pickers, plus call counters — no real UI.</summary>
public sealed class FakeDialogService : IDialogService
{
    public SaveChangesResult SaveChangesResult { get; set; } = SaveChangesResult.Discard;
    public bool ConfirmResult { get; set; }
    public string? OpenPath { get; set; }
    public string? SavePath { get; set; }
    public int ConfirmSaveCalls { get; private set; }
    public string? LastMessage { get; private set; }

    public Task<SaveChangesResult> ConfirmSaveChangesAsync(string documentName)
    {
        ConfirmSaveCalls++;
        return Task.FromResult(SaveChangesResult);
    }

    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(ConfirmResult);
    public Task ShowMessageAsync(string title, string message) { LastMessage = message; return Task.CompletedTask; }
    public Task<string?> PickOpenProjectAsync(string? initialDirectory) => Task.FromResult(OpenPath);
    public Task<string?> PickSaveProjectAsync(string? initialDirectory, string suggestedFileName) => Task.FromResult(SavePath);
    public Task ShowAboutAsync() => Task.CompletedTask;
    public Task ShowSettingsAsync(string settingsText) => Task.CompletedTask;
    public Task OpenExternalUrlAsync(string url) => Task.CompletedTask;
}

/// <summary>Builds file-only <see cref="ProjectSession"/>/<see cref="MainWindowViewModel"/> instances over a
/// throwaway temp directory, with a fake dialog service and no controller — the whole shell is exercised without
/// a network, controller or IHC install.</summary>
public sealed class ShellHarness : IDisposable
{
    public string TempDir { get; }
    public FakeDialogService Dialogs { get; } = new();
    public BackupService Backup { get; }
    public RecentProjectsStore Recent { get; }
    public ProjectSession Session { get; }

    private readonly bool _ownsDir;

    private ShellHarness(string dir, bool ownsDir, int changeThreshold)
    {
        TempDir = dir;
        _ownsDir = ownsDir;
        Directory.CreateDirectory(TempDir);
        Backup = new BackupService(Path.Combine(TempDir, "recovery"));
        Recent = new RecentProjectsStore(Path.Combine(TempDir, "recent.json"));
        var service = new ProjectAppService(new IhcSettings());
        // A one-hour timer never fires during a test; backup triggers are driven explicitly via MarkChangedAsync.
        Session = new ProjectSession(service, Backup, Recent, Dialogs, null, TimeSpan.FromHours(1), changeThreshold);
    }

    public static ShellHarness Create(int changeThreshold = 10) =>
        new(Path.Combine(Path.GetTempPath(), "ihc_ov_tests", Guid.NewGuid().ToString("N")), ownsDir: true, changeThreshold);

    /// <summary>A second session over an existing directory — simulates restarting the app after a crash so the
    /// recovery backup left in <paramref name="dir"/> is discovered.</summary>
    public static ShellHarness Restart(string dir, int changeThreshold = 10) =>
        new(dir, ownsDir: false, changeThreshold);

    public string TempPath(string fileName) => Path.Combine(TempDir, fileName);

    public MainWindowViewModel CreateViewModel() =>
        new(Session, Dialogs, Recent, new NullThemeService());

    public void Dispose()
    {
        Session.Dispose();
        if (_ownsDir)
        {
            try { Directory.Delete(TempDir, recursive: true); } catch { /* best-effort temp cleanup */ }
        }
    }
}
