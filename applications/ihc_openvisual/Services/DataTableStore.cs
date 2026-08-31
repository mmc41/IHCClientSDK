using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ihc_openvisual.Configuration;

namespace ihc_openvisual.Services;

/// <summary>One of the eighteen documentation data tables: its Danish caption and the stable key its texts are
/// persisted under.</summary>
/// <remarks>The key is the vendor's own backing-file stem, so a future import of an IHC Visual installation's
/// <c>Data\*.txt</c> needs no second mapping.</remarks>
public sealed record DataTableDefinition(string Name, string Key);

/// <summary>
/// The installer's own data tables — the reusable texts offered wherever a documentation field is filled in
/// (customer and installer contact details, wire colours, cable types, notes, …).
///
/// <para>
/// <b>These are APPLICATION state, not project state</b>: they are shared across every project the installer opens
/// and are never written into a <c>.vis</c>. The table set and its keys follow IHC Visual's own eighteen-row
/// manifest (<c>…\IHC Visual\Data\userEditableTables.txttables</c>, rows of <c>|caption|backing-file|</c>), so a
/// future import of an installation's <c>Data\*.txt</c> needs no second mapping. Persisted as JSON in the user's
/// app-data directory beside <see cref="InstallerIdentityStore"/>.
/// </para>
/// <para>
/// <b>There is no editor for these tables</b> — maintaining them by hand is a declared product exclusion. They fill
/// up the way the vendor's own do: a value typed into a documentation field joins that field's table, so the next
/// project offers it. Today the only writer and only reader is <see cref="ProjectInfoSuggestions"/>, behind the
/// project-info dialog's contact drop-downs.
/// </para>
/// <para>
/// <b>The product dialog's suggestion lists do NOT come from here, and are not planned to</b> (corrected 2026-08-12,
/// T032 — this used to say the remaining documentation fields would feed these keys). A
/// <c>ComboSuggest</c> field on the product dialog is offered the OPEN PROJECT's own distinct values for the
/// attribute it binds (D07), computed per open by <c>ProductDialogComposer</c>. That is a deliberate divergence from
/// the vendor, which reads a machine-local <c>Data\*.txt</c>: a project-sourced list travels WITH the project, so
/// two installers opening the same file are offered the same suggestions, where the vendor's differ per PC.
/// <c>Kabeltyper</c>, <c>Ledningsfarver</c> and the other field-backed rows therefore stay empty here by design.
/// They are kept rather than dropped because the manifest is the vendor's eighteen rows verbatim, which is what
/// lets a future import of an installation's <c>Data\</c> folder need no second mapping.
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
    private readonly ILogger _logger;
    private Dictionary<string, ImmutableArray<string>> _texts;

    public DataTableStore(string filePath, ILoggerFactory? loggerFactory = null)
    {
        _filePath = filePath;
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DataTableStore>();
        _texts = Load();
    }

    public static DataTableStore CreateDefault(ILoggerFactory? loggerFactory = null) =>
        new(DefaultFilePath(), loggerFactory);

    public static string DefaultFilePath() => Constants.AppDataPath("datatables.json");

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

    // A corrupt setting must not stop the app: an unreadable file reads as no tables at all.
    private Dictionary<string, ImmutableArray<string>> Load()
    {
        Dictionary<string, string[]>? raw =
            JsonPreferenceStore.TryLoad<Dictionary<string, string[]>>(_filePath, _logger);
        return raw is null
            ? new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
            : raw.ToDictionary(p => p.Key, p => p.Value.ToImmutableArray(), StringComparer.Ordinal);
    }

    private void Save() =>
        JsonPreferenceStore.TrySave(
            _filePath, _texts.ToDictionary(p => p.Key, p => p.Value.ToArray()), _logger);

}
