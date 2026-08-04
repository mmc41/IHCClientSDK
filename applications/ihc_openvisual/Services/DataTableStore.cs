using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ihc_openvisual.Services;

/// <summary>One of the eighteen documentation data tables: its Danish caption and the stable key its texts are
/// persisted under.</summary>
/// <remarks>The key is the vendor's own backing-file stem, so a future import of an IHC Visual installation's
/// <c>Data\*.txt</c> needs no second mapping.</remarks>
public sealed record DataTableDefinition(string Name, string Key);

/// <summary>
/// The installer's own data tables — the reusable texts IHC Visual offers wherever a documentation field is filled
/// in (customer and installer contact details, wire colours, cable types, notes, …).
///
/// <para>
/// <b>These are APPLICATION state, not project state.</b> Measured on the vendor 2026-08-04: the values its
/// <i>Rediger data tabeller</i> dialog listed under <c>Kunder</c> appear nowhere in the open project's <c>.vis</c> —
/// several of them were entered while entirely different projects were open. IHC Visual declares the table set in
/// <c>…\IHC Visual\Data\userEditableTables.txttables</c>, an eighteen-row manifest of
/// <c>|caption|backing-file|</c>, and that manifest's order is the order its dialog lists them in. This store keeps
/// the same eighteen tables, keyed by the same file stems, persisted as JSON in the user's app-data directory
/// beside <see cref="InstallerIdentityStore"/>.
/// </para>
/// <para>
/// What OpenVisual had instead was a different feature wearing the same name: the left pane listed the open
/// project's <c>enum_definition</c>s (function-block types such as <i>Persienne tilstand</i>, which the vendor's
/// dialog does not show at all) and the right pane the values of an enum named <c>User-defined texts</c> — a name
/// that occurs in no <c>.vis</c> in the corpus, so that pane could never be anything but empty.
/// </para>
/// </summary>
public sealed class DataTableStore
{
    /// <summary>The eighteen tables, in the vendor manifest's order — which is the order the dialog lists them.</summary>
    public static readonly ImmutableArray<DataTableDefinition> Definitions =
    [
        new("Kunder", "customer"),
        new("Firma", "company"),
        new("Mobil telefonnumre", "mobilphone"),
        new("Telefon numre", "phone"),
        new("email adresser", "email"),
        new("Vejnavne", "street"),
        new("By", "city"),
        new("Post numre", "zip"),
        new("Land", "country"),
        new("Ledningsfarver", "noteCableColour"),
        new("Kabelnummer", "noteCableNumber"),
        new("Kabeltyper", "noteCableType"),
        new("Produkt position", "noteProductPosition"),
        new("Note tekster", "noteNote"),
        new("Lysgrupper", "notePowerGroup"),
        new("Projekt typer", "projecttype"),
        new("Datalinie modul lokationer", "noteModuleDatalineLocation"),
        new("Produkt identifikationskoder", "documentationTag"),
    ];

    private readonly string _filePath;
    private Dictionary<string, ImmutableArray<string>> _texts;

    public DataTableStore(string filePath)
    {
        _filePath = filePath;
        _texts = Load();
    }

    public static DataTableStore CreateDefault() => new(DefaultFilePath());

    public static string DefaultFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IHC OpenVisual",
            "datatables.json");

    /// <summary>The texts the installer has added to one table, in the order they were added. An unknown key —
    /// or a table never added to — reads as empty rather than throwing: every table exists, most are empty.</summary>
    public ImmutableArray<string> TextsFor(string key) =>
        _texts.TryGetValue(key, out ImmutableArray<string> texts) ? texts : ImmutableArray<string>.Empty;

    /// <summary>Replaces every table's texts at once and persists — the dialog's OK, which commits a whole
    /// working copy. Annuller simply never calls this.</summary>
    public void Commit(IReadOnlyDictionary<string, IReadOnlyList<string>> tables)
    {
        _texts = tables
            .Where(pair => pair.Value.Count > 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToImmutableArray(), StringComparer.Ordinal);
        Save();
    }

    private Dictionary<string, ImmutableArray<string>> Load()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);
        try
        {
            Dictionary<string, string[]>? raw =
                JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(_filePath));
            return raw is null
                ? new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
                : raw.ToDictionary(p => p.Key, p => p.Value.ToImmutableArray(), StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);   // as InstallerIdentityStore: a corrupt setting must not stop the app
        }
    }

    private void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath,
                JsonSerializer.Serialize(_texts.ToDictionary(p => p.Key, p => p.Value.ToArray()), JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing a preference is not worth failing the edit that triggered the write.
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
