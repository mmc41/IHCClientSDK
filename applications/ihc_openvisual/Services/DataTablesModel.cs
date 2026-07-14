using System.Collections.Immutable;

namespace ihc_openvisual.Services;

/// <summary>A read-only system data table (US-049): its name and its reference rows. These are the built-in
/// (<c>typeid</c>-bearing) enum definitions — shown for reference, never edited.</summary>
public sealed record DataTableView(string Name, ImmutableArray<string> Rows);

/// <summary>One editable user-defined text (US-049): its element id token (for edit/delete) and its text.</summary>
public sealed record UserText(string Id, string Text);

/// <summary>The data-tables dialog's content (US-049): the read-only system tables and the editable
/// user-defined texts.</summary>
public sealed record DataTablesModel(ImmutableArray<DataTableView> SystemTables, ImmutableArray<UserText> UserTexts);
