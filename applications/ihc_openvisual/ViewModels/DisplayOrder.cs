using System;
using System.Globalization;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// How this app orders text the INSTALLER reads — menu entries, folder names, list rows.
/// <para>
/// Danish collation, because the application is Danish-only and da-DK sorts Æ/Ø/Å after Z, where an ordinal
/// comparer sorts them by code point: Æ (U+00C6) lands after every ASCII letter but Å (U+00C5) sorts before Æ,
/// and lower-case æøå land in a third place entirely — so an ordinal list is wrong in a way a Danish reader
/// notices immediately. Ordinal-ignore-case happens to agree for every name in the built-in catalog (none begins
/// with Æ/Ø/Å), which is exactly why this needs stating: the defect would first appear on an installer's own
/// imported <c>.def</c>/<c>.ifb</c>, not in any shipped fixture.
/// </para>
/// <para>
/// For DISPLAY ordering only. Dictionary keys, element ids, catalog identifiers and file paths stay ordinal —
/// culture-sensitive comparison of an identifier is a correctness bug, not a niceness one.
/// </para>
/// </summary>
public static class DisplayOrder
{
    /// <summary>The Danish, case-insensitive comparer for user-visible ordering.</summary>
    public static readonly StringComparer Danish =
        StringComparer.Create(CultureInfo.GetCultureInfo("da-DK"), ignoreCase: true);
}
