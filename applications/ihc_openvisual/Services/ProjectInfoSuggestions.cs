using System;
using System.Collections.Generic;
using System.Linq;

namespace ihc_openvisual.Services;

/// <summary>
/// The values the project-info dialog offers per contact field, drawn from the installer's data tables (US-049).
///
/// <para>
/// This is what makes IHC Visual's contact fields drop-downs rather than plain boxes: measured on the vendor
/// 2026-08-04, all sixteen are editable combos, and each one's list is a data table — <c>Firma</c> behind the
/// installer's <i>Navn</i>, <c>Kunder</c> behind the customer's, and one shared table behind each of the other
/// seven fields (the vendor offered the SAME street/phone/zip/city/country/email/mobile list on both sides).
/// </para>
/// </summary>
public sealed record ProjectInfoSuggestions(
    IReadOnlyList<string> InstallerNames,
    IReadOnlyList<string> CustomerNames,
    IReadOnlyList<string> Streets,
    IReadOnlyList<string> Phones,
    IReadOnlyList<string> Zips,
    IReadOnlyList<string> Mobiles,
    IReadOnlyList<string> Cities,
    IReadOnlyList<string> Emails,
    IReadOnlyList<string> Countries)
{
    public static ProjectInfoSuggestions Empty { get; } =
        new([], [], [], [], [], [], [], [], []);

    public static ProjectInfoSuggestions From(DataTableStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        return new(
            store.TextsFor("company"),
            store.TextsFor("customer"),
            store.TextsFor("street"),
            store.TextsFor("phone"),
            store.TextsFor("zip"),
            store.TextsFor("mobilphone"),
            store.TextsFor("city"),
            store.TextsFor("email"),
            store.TextsFor("country"));
    }

    /// <summary>The table key each contact field feeds, so a value typed into the dialog joins the same list it
    /// would have been offered from next time. This is how the vendor's tables fill up: every one of its
    /// <c>Kunder</c> entries was typed into a project-info dialog, not into the data-tables editor.</summary>
    internal static readonly (string Key, System.Func<Ihc.Vis.ContactInfo, string?> Field)[] CustomerFields =
    [
        ("customer", c => c.Name), ("street", c => c.Address), ("phone", c => c.Phone), ("zip", c => c.Zip),
        ("mobilphone", c => c.Mobile), ("city", c => c.City), ("email", c => c.Email), ("country", c => c.Country),
    ];

    internal static readonly (string Key, System.Func<Ihc.Vis.ContactInfo, string?> Field)[] InstallerFields =
    [
        ("company", c => c.Name), ("street", c => c.Address), ("phone", c => c.Phone), ("zip", c => c.Zip),
        ("mobilphone", c => c.Mobile), ("city", c => c.City), ("email", c => c.Email), ("country", c => c.Country),
    ];

    /// <summary>Folds the contact values an installer just committed into the data tables, appending only what is
    /// new and non-blank. Returns the tables to commit.</summary>
    public static Dictionary<string, IReadOnlyList<string>> Absorb(
        DataTableStore store, Ihc.Vis.ProjectInfoData info)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(info);

        var tables = DataTableStore.Definitions.ToDictionary(
            d => d.Key, d => (IReadOnlyList<string>)store.TextsFor(d.Key).ToList(), System.StringComparer.Ordinal);

        foreach ((string key, System.Func<Ihc.Vis.ContactInfo, string?> field) in CustomerFields)
            Append(tables, key, field(info.Customer));
        foreach ((string key, System.Func<Ihc.Vis.ContactInfo, string?> field) in InstallerFields)
            Append(tables, key, field(info.Installer));
        Append(tables, "projecttype", info.Type);
        return tables;
    }

    private static void Append(Dictionary<string, IReadOnlyList<string>> tables, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !tables.TryGetValue(key, out IReadOnlyList<string>? rows))
            return;
        string trimmed = value.Trim();
        if (rows.Contains(trimmed))
            return;
        tables[key] = rows.Append(trimmed).ToList();
    }
}
