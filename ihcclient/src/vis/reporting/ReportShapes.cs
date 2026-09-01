using System.Collections.Immutable;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// Mode membership of a shape (spec R4/D1): <see cref="Common"/> shapes appear in Standard AND Full;
    /// <see cref="FullOnly"/> shapes only in Full. Read by <see cref="ReportModeFilter"/> alone — the writers
    /// never see the mode, and never infer it: every layout choice they make keys off an explicit layout
    /// property (<see cref="SectionBreakStyle"/>, <see cref="FbBlockShape.Standalone"/>, …), never off which
    /// content a mode happened to strip.
    /// </summary>
    internal enum ReportMembership
    {
        /// <summary>Rendered in both Standard and Full mode.</summary>
        Common,

        /// <summary>Rendered in Full mode only (Full = Standard verbatim + additive).</summary>
        FullOnly,
    }

    /// <summary>
    /// One layout shape of the closed rendering vocabulary (spec D1): the builder projects a
    /// <see cref="Ihc.Vis.Projects.Project"/> into an ordered list of shapes, and one generic writer per
    /// output format renders them. All format rules (markup, CSS, column math, blank-line grammar) live in
    /// the writers; all content and mode tagging lives in the shapes. The vocabulary grows only when an
    /// oracle-witnessed layout needs a new shape.
    /// </summary>
    internal abstract record ReportShape(ReportMembership Membership);

    /// <summary>
    /// An indented tree block (the functions report body; the FB report's icon trees): rendered rows in
    /// document order, depth carried per row (HTML nests lists, text indents two spaces per level).
    /// </summary>
    internal sealed record TreeShape(ImmutableArray<ReportTreeRow> Rows)
        : ReportShape(ReportMembership.Common);

    /// <summary>
    /// One row of a <see cref="TreeShape"/> (or of a <see cref="FbBlockShape"/>'s icon tree).
    /// <see cref="Depth"/> is 0-based.
    /// <para><see cref="Membership"/> carries mode at ROW level, so a mode can add rows and not merely
    /// strip fields — it defaults to <see cref="ReportMembership.Common"/>, so a builder says nothing
    /// unless it means to. <see cref="ReportModeFilter"/> is still the only reader; the writers never see
    /// it. Marking a row <see cref="ReportMembership.FullOnly"/> drops its whole SUBTREE in Standard, which
    /// is a contract and not an optimisation: rows are depth-encoded and the HTML writer's forest builder
    /// consumes only rows at exactly the depth it is reading, so a surviving child of a dropped parent
    /// would silently truncate the rest of the block.</para>
    /// </summary>
    internal abstract record ReportTreeRow(int Depth)
    {
        public ReportMembership Membership { get; init; } = ReportMembership.Common;
    }

    /// <summary>
    /// A row whose name is emphasized (HTML <c>span.name</c>): localities and products. <see cref="Detail"/>
    /// is trailing plain text after the name (a product's placering); null when absent.
    /// <see cref="IdToken"/> is the element's <c>_0x</c> id, rendered as the Full-only <c>(ID …)</c> chip
    /// between name and detail — stripped in Standard mode.
    /// </summary>
    internal sealed record NamedTreeRow(int Depth, string Name, string? Detail, string? IdToken)
        : ReportTreeRow(Depth);

    /// <summary>A plain text row (a terminal), with the Full-only id chip appended when present.</summary>
    internal sealed record PlainTreeRow(int Depth, string Text, string? IdToken)
        : ReportTreeRow(Depth);

    /// <summary>
    /// A note row (HTML <c>li.note</c>) under a terminal: the far FB pin's note text (spec A5). An empty
    /// note still emits its row (U2). <see cref="LocalitySuffix"/> carries the linked FB's locality name
    /// when it differs from the product's by name equality (B9) — a Full-only field rendered as
    /// <c> (name)</c> and stripped in Standard mode.
    /// </summary>
    internal sealed record NoteTreeRow(int Depth, string Text, string? LocalitySuffix)
        : ReportTreeRow(Depth);

    /// <summary>
    /// The Full-mode report-meta line under the title (R4): generation timestamp (from the facade's
    /// <c>TimeProvider</c>, already formatted) and the project's programmer. The writers own the joining
    /// format ("Fuld rapport — Genereret: … — Programmør: …" — the HTML variant uses the
    /// <c>&amp;mdash;</c> entity, the text variant the literal dash).
    /// </summary>
    internal sealed record MetaLineShape(string GeneratedAt, string Programmer)
        : ReportShape(ReportMembership.FullOnly);

    /// <summary>One key/value row of a <see cref="KeyValueBlockShape"/>; values are display-ready (A1: the
    /// builder supplies <c>--</c> for blank masthead values). <see cref="IdToken"/> is the Full-only id
    /// chip appended to the value at the element's definition site — stripped in Standard mode.</summary>
    internal readonly record struct KeyValueRow(string Key, string Value, string? IdToken = null);

    /// <summary>One table cell: the display text plus the Full-only id chip when this cell is the row
    /// subject's definition site (module type, terminal name, S0 product) — stripped in Standard mode.</summary>
    internal readonly record struct ReportCell(string Text, string? IdToken = null)
    {
        public static implicit operator ReportCell(string text) => new(text);
    }

    /// <summary>The HTML rendering variant of a <see cref="KeyValueBlockShape"/> (text renders both the same
    /// way): <see cref="Meta"/> = title-row table (the Projekt block), <see cref="People"/> = side
    /// party-heading table (the installation Installatør/Kunde mastheads).</summary>
    internal enum KeyValueStyle
    {
        Meta,
        People,
    }

    /// <summary>
    /// A titled key/value block (the Full-mode Projekt identity block; the installation mastheads):
    /// HTML renders a table per <see cref="Style"/>, text a heading with an aligned two-column grid.
    /// </summary>
    internal sealed record KeyValueBlockShape(string Heading, ImmutableArray<KeyValueRow> Rows, KeyValueStyle Style, ReportMembership Membership)
        : ReportShape(Membership);

    /// <summary>
    /// One per-locality component block of the installation report (A8): the family-specific field rows
    /// plus, for dataline products only, the descendant-scoped unsorted terminal sub-table (A9). Text
    /// renders the fields as an indented grid with the sub-table nested one level deeper; HTML renders one
    /// <c>table.locality</c> holding both.
    /// </summary>
    internal sealed record ComponentBlockShape(ImmutableArray<KeyValueRow> Fields, TableShape? Terminals)
        : ReportShape(ReportMembership.Common);

    /// <summary>The HTML indentation of a <see cref="SectionBreakShape"/> (text renders both the same way):
    /// <see cref="Flush"/> = at column 0 (the shared appendix form), <see cref="Indented"/> = carrying the
    /// installation body's indent.</summary>
    internal enum SectionBreakStyle
    {
        Flush,
        Indented,
    }

    /// <summary>
    /// A section break before a titled section (the "Fejl i dokumentation" appendix; the installation and
    /// FB report sections): HTML renders <c>&lt;hr class="divider"&gt;</c> + <c>&lt;h2&gt;</c> at the
    /// <see cref="Style"/>'s indent, text a full-width dash rule + the heading line.
    /// </summary>
    internal sealed record SectionBreakShape(string Heading, SectionBreakStyle Style, ReportMembership Membership)
        : ReportShape(Membership);

    /// <summary>
    /// The HTML layout family of a <see cref="TableShape"/> (text renders every family the same way). Each
    /// value selects a fixed writer-owned markup form: <see cref="Plain"/> = bare table, no thead (the
    /// findings appendix and the locality terminal grid); <see cref="Module"/> = thead with a title row
    /// (the module tables); <see cref="Datalines"/> = scroll wrapper + <c>table.datalines</c> with the
    /// fixed column-percentage colgroup and 5+6 split rows; <see cref="Special"/>/<see cref="S0"/> =
    /// scroll wrapper, no colgroup, 6+4 / 5+2 split rows.
    /// </summary>
    internal enum TableStyle
    {
        Plain,
        Module,
        Datalines,
        Special,
        S0,
    }

    /// <summary>
    /// A flat table: optional <see cref="Heading"/>, column headers, and display-ready cell rows (blank
    /// cells stay blank in the flat cross-reference tables; the per-locality tables carry <c>--</c> — both
    /// per A1, decided by the builder). Text renders the heading as its own line directly above the table
    /// and computes content-driven column widths with dash underlines; HTML renders the fixed markup form
    /// selected by <see cref="Style"/>.
    /// </summary>
    internal sealed record TableShape(string? Heading, ImmutableArray<string> Columns, ImmutableArray<ImmutableArray<ReportCell>> Rows, TableStyle Style, ReportMembership Membership)
        : ReportShape(Membership);

    /// <summary>
    /// An icon-carrying tree row of the function-block report (R7): depth, the semantic icon key
    /// (<see cref="ReportIconKeys"/>), the name text, the optional <c>= value</c> (A10/A11), the optional
    /// note column, and the Full-only id chip.
    /// </summary>
    internal sealed record IconTreeRow(int Depth, string IconKey, string Name, string? Value, string? Note, string? IdToken)
        : ReportTreeRow(Depth);

    /// <summary>One rendered line of a function block's description (the block <c>@note</c> after the B7
    /// line rules): <see cref="IsNote"/> marks the trailing small-print paragraph's lines.</summary>
    internal readonly record struct FbParagraph(string Text, bool IsNote);

    /// <summary>
    /// One function block of the FB report: the B7 heading (the block's <c>@name</c>), the Full-only id
    /// chip, the Full-only identity grid (Lokalitet/Type/Version/Låst — stripped to empty in Standard),
    /// the description paragraphs, and the icon-tree rows. The fixed "Anvendelse" kicker label above the
    /// paragraphs is writer boilerplate, emitted only when paragraphs exist. <see cref="Standalone"/> picks
    /// between the two witnessed layouts: set, the section renders as an ordinary blank-separated block;
    /// clear, it joins the single-line section run. <see cref="ReportModeFilter"/> clears it along with the
    /// identity grid, so the writers pick a layout without ever inferring the mode.
    /// </summary>
    internal sealed record FbBlockShape(string Heading, string? IdToken, ImmutableArray<KeyValueRow> Identity, ImmutableArray<FbParagraph> Paragraphs, ImmutableArray<ReportTreeRow> Rows, bool Standalone)
        : ReportShape(ReportMembership.Common);

    /// <summary>
    /// One complete report as shapes (spec D1): the fixed house banner and the <see cref="Title"/> heading
    /// are writer boilerplate; everything below the title is <see cref="Shapes"/> in render order.
    /// <see cref="TitleHugsFirstShape"/> is the one document-level LAYOUT property: set, the first shape
    /// renders directly under the title with no blank separator (the FB report's witnessed layout).
    /// </summary>
    internal sealed record ReportShapeDocument(string Title, ImmutableArray<ReportShape> Shapes, bool TitleHugsFirstShape = false);
}
