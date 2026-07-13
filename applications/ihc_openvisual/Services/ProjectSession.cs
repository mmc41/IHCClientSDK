using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ihc_openvisual.Configuration;
using Ihc.Vis;
using Ihc.Vis.Projects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ihc_openvisual.Services;

/// <summary>
/// The single open-document session for the window: owns the one <see cref="Project"/>, its file path, the
/// dirty flag and the change counter, and orchestrates the whole project lifecycle (new/open/save/save-as/
/// close/quit) on top of the stateless SDK <see cref="ProjectAppService"/>. Enforces the single-project
/// constraint, drives the save-prompt through <see cref="IDialogService"/>, and runs the crash-recovery
/// auto-backup (10-minute timer + every 10th change). Deliberately Avalonia-free so it is testable headlessly.
/// </summary>
public sealed class ProjectSession : IDisposable
{
    private readonly ProjectAppService _service;
    private readonly BackupService _backup;
    private readonly RecentProjectsStore _recent;
    private readonly IDialogService _dialogs;
    private readonly ILogger<ProjectSession> _logger;
    private readonly TimeSpan _autoBackupInterval;
    private readonly int _changeBackupThreshold;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _backupLock = new(1, 1);
    private Timer? _timer;

    public ProjectSession(
        ProjectAppService service,
        BackupService backup,
        RecentProjectsStore recent,
        IDialogService dialogs,
        ILoggerFactory? loggerFactory = null,
        TimeSpan? autoBackupInterval = null,
        int changeBackupThreshold = 10)
    {
        _service = service;
        _backup = backup;
        _recent = recent;
        _dialogs = dialogs;
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ProjectSession>();
        _autoBackupInterval = autoBackupInterval ?? TimeSpan.FromMinutes(10);
        _changeBackupThreshold = changeBackupThreshold < 1 ? 10 : changeBackupThreshold;
    }

    public Project? Current { get; private set; }

    public string? FilePath { get; private set; }

    public bool IsDirty { get; private set; }

    public int ChangeCount { get; private set; }

    /// <summary>The document name shown in the title bar: <c>Untitled</c> before the first save, else the file name.</summary>
    public string DocumentName => FilePath is null ? Constants.UntitledDocument : Path.GetFileName(FilePath);

    /// <summary>Raised whenever the current project, file path or dirty flag changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Start-up entry point: offer to recover a crash backup if one exists (US-005), otherwise open a
    /// fresh empty project (US-002); then begin the auto-backup timer.</summary>
    public async Task StartAsync()
    {
        if (_backup.HasRecovery())
        {
            RecoveryInfo? info = _backup.ReadMarker();
            string when = info is { } i ? $" from {i.SavedAtUtc.ToLocalTime():g}" : string.Empty;
            bool recover = await _dialogs.ConfirmAsync(
                "Recover project",
                $"IHC OpenVisual did not close normally last time. Recover unsaved work{when}?");
            if (recover)
            {
                Project recovered = await _service.Load(_backup.RecoveryProjectPath);
                SetProject(recovered, info?.OriginPath, dirty: true);
                ResetChangeCount();
                StartTimer();
                return;
            }
            _backup.Delete();
        }

        NewInternal();
        StartTimer();
    }

    /// <summary>File → New (US-002): prompt to save the open project, then open the standard empty project.</summary>
    public async Task<bool> NewAsync()
    {
        if (!await ConfirmSaveIfDirtyAsync())
            return false;
        NewInternal();
        _backup.Delete();
        ResetChangeCount();
        return true;
    }

    /// <summary>File → Open (US-004): prompt to save, then load the chosen file as the single active project.</summary>
    public async Task<bool> OpenAsync(string path)
    {
        if (!await ConfirmSaveIfDirtyAsync())
            return false;
        try
        {
            Project loaded = await _service.Load(path);
            SetProject(loaded, path, dirty: false);
            _recent.Add(path);
            _backup.Delete();
            ResetChangeCount();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open project {Path}", path);
            await _dialogs.ShowMessageAsync("Open failed", $"Could not open '{path}':\n{ex.Message}");
            return false;
        }
    }

    public async Task<bool> OpenWithPickerAsync()
    {
        string? path = await _dialogs.PickOpenProjectAsync(_recent.LastDirectory);
        return path is not null && await OpenAsync(path);
    }

    /// <summary>File → Save (US-003): re-save to the existing file, or fall through to Save As when unnamed.</summary>
    public async Task<bool> SaveAsync()
    {
        if (Current is null)
            return false;
        return FilePath is null ? await SaveAsAsync() : await SaveToAsync(FilePath);
    }

    /// <summary>File → Save As (US-003): pick a file name and write the project there.</summary>
    public async Task<bool> SaveAsAsync()
    {
        if (Current is null)
            return false;
        string suggested = FilePath is not null ? Path.GetFileName(FilePath) : "Untitled.vis";
        string? path = await _dialogs.PickSaveProjectAsync(_recent.LastDirectory, suggested);
        return path is not null && await SaveToAsync(path);
    }

    /// <summary>File → Close (US-005): prompt to save, discard the crash backup, and return to a fresh empty project.</summary>
    public async Task<bool> CloseAsync()
    {
        if (!await ConfirmSaveIfDirtyAsync())
            return false;
        _backup.Delete();
        NewInternal();
        ResetChangeCount();
        return true;
    }

    /// <summary>Quit gate (US-064): prompt to save; on a clean, acknowledged exit discard the crash backup.
    /// Returns false to cancel the quit.</summary>
    public async Task<bool> CanQuitAsync()
    {
        if (!await ConfirmSaveIfDirtyAsync())
            return false;
        _backup.Delete();
        return true;
    }

    /// <summary>Records one committed edit (the hook editors use in E2+): marks the project dirty and triggers a
    /// crash backup on every Nth change. Fire-and-forget for UI callers; tests await <see cref="MarkChangedAsync"/>.</summary>
    public void MarkChanged() => _ = MarkChangedAsync();

    internal async Task MarkChangedAsync()
    {
        bool backup;
        lock (_gate)
        {
            IsDirty = true;
            ChangeCount++;
            backup = ChangeCount % _changeBackupThreshold == 0;
        }
        RaiseChanged();
        if (backup)
            await AutoBackupAsync();
    }

    /// <summary>Writes the current project to the recovery location. Invoked by the timer and the change counter;
    /// exposed internally so tests can drive it deterministically without waiting on the timer.</summary>
    internal async Task AutoBackupAsync()
    {
        // Serialize the timer path and the change-threshold path so two backups never write the recovery
        // file concurrently (the atomic File.Replace/File.Move would otherwise race and throw).
        await _backupLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Project? snapshot;
            string? origin;
            lock (_gate)
            {
                snapshot = Current;
                origin = FilePath;
            }
            if (snapshot is null)
                return;
            _backup.EnsureDirectory();
            await _service.Save(snapshot, _backup.RecoveryProjectPath);
            _backup.WriteMarker(origin, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-backup failed");
        }
        finally
        {
            _backupLock.Release();
        }
    }

    private async Task<bool> SaveToAsync(string path)
    {
        try
        {
            await _service.Save(Current!, path);
            lock (_gate)
            {
                FilePath = path;
                IsDirty = false;
            }
            _recent.Add(path);
            // The work is now safely persisted, so the crash backup is stale and the change counter starts
            // over — matching the New/Open/Close transitions.
            _backup.Delete();
            ResetChangeCount();
            RaiseChanged();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save project {Path}", path);
            await _dialogs.ShowMessageAsync("Save failed", $"Could not save '{path}':\n{ex.Message}");
            return false;
        }
    }

    private async Task<bool> ConfirmSaveIfDirtyAsync()
    {
        if (!IsDirty)
            return true;
        SaveChangesResult result = await _dialogs.ConfirmSaveChangesAsync(DocumentName);
        return result switch
        {
            SaveChangesResult.Save => await SaveAsync(),
            SaveChangesResult.Discard => true,
            _ => false
        };
    }

    private void NewInternal()
    {
        Project project = _service.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
        SetProject(project, null, dirty: false);
    }

    private void SetProject(Project project, string? path, bool dirty)
    {
        lock (_gate)
        {
            Current = project;
            FilePath = path;
            IsDirty = dirty;
        }
        RaiseChanged();
    }

    private void ResetChangeCount()
    {
        lock (_gate)
        {
            ChangeCount = 0;
        }
    }

    private void StartTimer()
    {
        _timer ??= new Timer(_ => _ = AutoBackupAsync(), null, _autoBackupInterval, _autoBackupInterval);
    }

    private void RaiseChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _timer?.Dispose();
        _backupLock.Dispose();
    }
}
