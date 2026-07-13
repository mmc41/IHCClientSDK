using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

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
    private readonly List<string> _items = new();

    public RecentProjectsStore(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    public static RecentProjectsStore CreateDefault() => new(DefaultFilePath());

    public static string DefaultFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IHC OpenVisual",
            "recent.json");

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

    private void Load()
    {
        if (!File.Exists(_filePath))
            return;
        try
        {
            PersistModel? model = JsonSerializer.Deserialize<PersistModel>(File.ReadAllText(_filePath));
            if (model?.Items is { } items)
                _items.AddRange(items.Take(MaxItems));
            LastDirectory = model?.LastDirectory;
        }
        catch (Exception)
        {
            // A corrupt recent-list must never block start-up; start with an empty list.
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var model = new PersistModel { Items = _items.ToList(), LastDirectory = LastDirectory };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(model));
        }
        catch (Exception)
        {
            // Persisting the recent list is best-effort; ignore IO failures.
        }
    }

    private sealed class PersistModel
    {
        public List<string> Items { get; set; } = new();
        public string? LastDirectory { get; set; }
    }
}
