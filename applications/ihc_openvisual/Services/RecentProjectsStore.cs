using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ihc_openvisual.Configuration;

namespace ihc_openvisual.Services;

/// <summary>
/// The most-recently-used project list shown on the <i>File</i> menu (US-004): at most four entries,
/// most-recent first, de-duplicated, persisted as JSON in the user's app-data directory. Also remembers the
/// directory of the last opened/saved project so file dialogs default there. Avalonia-free and testable.
/// </summary>
public sealed class RecentProjectsStore
{
    public const int MaxItems = 4;

    private readonly string _filePath;
    private readonly ILogger _logger;
    private readonly List<string> _items = new();

    public RecentProjectsStore(string filePath, ILoggerFactory? loggerFactory = null)
    {
        _filePath = filePath;
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<RecentProjectsStore>();
        Load();
    }

    public static RecentProjectsStore CreateDefault(ILoggerFactory? loggerFactory = null) =>
        new(DefaultFilePath(), loggerFactory);

    public static string DefaultFilePath() => Constants.AppDataPath("recent.json");

    public event EventHandler? Changed;

    public IReadOnlyList<string> Items => _items;

    public string? LastDirectory { get; private set; }

    public void Add(string path)
    {
        string full = Path.GetFullPath(path);
        _items.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
        _items.Insert(0, full);
        while (_items.Count > MaxItems)
            _items.RemoveAt(_items.Count - 1);

        LastDirectory = Path.GetDirectoryName(full);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // A corrupt recent-list must never block start-up; an empty list is the fallback.
    private void Load()
    {
        PersistModel? model = JsonPreferenceStore.TryLoad<PersistModel>(_filePath, _logger);
        if (model?.Items is { } items)
            _items.AddRange(items.Take(MaxItems));
        LastDirectory = model?.LastDirectory;
    }

    private void Save() =>
        JsonPreferenceStore.TrySave(
            _filePath,
            new PersistModel { Items = _items.ToList(), LastDirectory = LastDirectory },
            _logger);

    private sealed class PersistModel
    {
        public List<string> Items { get; set; } = new();
        public string? LastDirectory { get; set; }
    }
}
