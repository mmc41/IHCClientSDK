#nullable enable
using System;
using System.Collections.Generic;

using Ihc.Vis.Model;
using Ihc.Vis.Validation;

namespace Ihc.Vis
{
    /// <summary>Which of the three IHC documentation reports to generate (spec R3/R4).</summary>
    public enum ReportKind
    {
        /// <summary>"Funktionsdokumentation" — per locality → end-user-flagged product → terminals + linked FB-pin notes.</summary>
        Functions,

        /// <summary>"Installationsdokumentation" — module tables, per-locality component/wiring tables, cross-references.</summary>
        Installation,

        /// <summary>"Funktionsblok dokumentation" — per function block: description, variables with values, program tree.</summary>
        FunctionBlocks,
    }

    /// <summary>
    /// The report's information scope (spec R4): <see cref="Standard"/> is the vendor-parity content;
    /// <see cref="Full"/> is strictly additive — generation-meta line, Projekt identity block, inline
    /// <c>(ID _0x…)</c> chips, the "Fejl i dokumentation" appendix, and the kind-specific Full sections.
    /// </summary>
    public enum ReportMode
    {
        /// <summary>The vendor-parity information scope (the <c>std-*</c> oracles).</summary>
        Standard,

        /// <summary>Standard verbatim plus the additive Full-only content (the <c>full-*</c> oracles).</summary>
        Full,
    }

    /// <summary>
    /// Each report kind's Danish title — the <c>&lt;h1&gt;</c> / underlined heading the generated document
    /// carries, and the label a frontend shows for that kind. Published so a picker's menu entry and the
    /// document it produces cannot drift apart; the titles themselves are pinned by the report oracles.
    /// </summary>
    public static class ReportTitles
    {
        /// <summary>The title of <paramref name="kind"/>'s report.</summary>
        public static string For(ReportKind kind) => kind switch
        {
            ReportKind.Functions => "Funktionsdokumentation",
            ReportKind.Installation => "Installationsdokumentation",
            ReportKind.FunctionBlocks => "Funktionsblok dokumentation",
            _ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown report kind."),
        };
    }

    /// <summary>The report output formats <see cref="ProjectAppService.GenerateReport(Projects.Project, ReportKind, ReportMode, string, System.IO.Stream, IReportIconProvider?)"/> accepts (spec R1/R3). Any other mimetype is rejected with a clear error.</summary>
    public static class ReportMimeTypes
    {
        /// <summary>A self-contained HTML page (screen + print styling, inline icon sprite).</summary>
        public const string Html = "text/html";

        /// <summary>Plain text with the default 1–3 character unicode icon stand-ins.</summary>
        public const string PlainText = "text/plain";

        /// <summary>The file extension (without the dot) a report of <paramref name="mimeType"/> is written as — the
        /// one place the format↔extension mapping lives, so a caller naming a file and a caller configuring a save
        /// dialog cannot drift apart. Anything other than <see cref="PlainText"/> is HTML, matching what
        /// <c>GenerateReport</c> accepts.</summary>
        /// <remarks>
        /// The findings export is deliberately NOT a member of this mapping. It is not a report, <c>GenerateReport</c>
        /// rejects its mimetype, and an arm for it here would put a value this class's own contract refuses inside
        /// the type that publishes what that contract accepts. Its format is <see cref="FindingExportFormat"/>.
        /// </remarks>
        public static string FileExtensionFor(string mimeType) => mimeType == PlainText ? "txt" : "html";
    }

    /// <summary>
    /// The format <see cref="ProjectAppService.ExportFindings(Projects.Project, string, FindingExportOptions?)"/>
    /// writes: a flat, attribute-only XML document in the <c>.vis</c> encoding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two constants and no <c>…For(mimeType)</c> lookup, because there is nothing to look up: the findings export
    /// has exactly ONE format, so a caller never chooses one and never passes one. That is what makes this a
    /// declaration rather than a second copy of <see cref="ReportMimeTypes.FileExtensionFor"/> — the objection that
    /// once argued for folding <c>application/xml</c> into <see cref="ReportMimeTypes"/> was about duplicating a
    /// MAPPING, and there is no mapping here to duplicate.
    /// </para>
    /// <para>
    /// Published rather than left implicit so the bytes the SDK writes and the name a host suggests for them stay
    /// one fact — the same reason <see cref="ReportMimeTypes.FileExtensionFor"/> exists for reports.
    /// </para>
    /// </remarks>
    public static class FindingExportFormat
    {
        /// <summary>The document's media type, for a host labelling it or serving it.</summary>
        public const string MimeType = "application/xml";

        /// <summary>The file extension (without the dot) a findings export is written as.</summary>
        public const string FileExtension = "xml";
    }

    /// <summary>
    /// The caller-supplied icon mapping for report generation (spec R11). Element type → semantic icon key
    /// stays SDK logic; this contract only maps key → glyph for a given output format. Both formats consult
    /// the provider; a provider that only customizes HTML simply returns null for <c>text/plain</c>.
    /// </summary>
    /// <remarks>
    /// <b>Trust and escaping boundary:</b> fragments returned by a provider are trusted RAW markup for the
    /// requested format and are emitted verbatim (a provider is caller code running in-process). Everything
    /// else the writers emit — project data, labels, finding messages, and the DEFAULT unicode stand-ins —
    /// always goes through the writer's escaping for the target format.
    /// </remarks>
    public interface IReportIconProvider
    {
        /// <summary>
        /// The per-instance icon fragment for (<paramref name="mimeType"/>, <paramref name="iconKey"/>), or
        /// null/empty to fall back to the default unicode stand-in for that key — generation never fails over
        /// icons. OpenVisual's HTML provider returns
        /// <c>&lt;svg class="icon icon-&lt;key&gt;" aria-hidden="true"&gt;&lt;use href="#icon-&lt;key&gt;"/&gt;&lt;/svg&gt;</c>.
        /// </summary>
        string? GetFragment(string mimeType, string iconKey);

        /// <summary>
        /// The optional once-per-document definitions block for the keys used by the document (for HTML: the
        /// inline <c>&lt;symbol&gt;</c> sprite placed directly after <c>&lt;body&gt;</c>). The block is one
        /// opaque trusted fragment — content, ordering and de-duplication are the provider's responsibility.
        /// Return null/empty for no block (the default provider contributes none; text output never has one).
        /// </summary>
        string? GetDefinitionsBlock(string mimeType, IReadOnlyCollection<string> iconKeys);
    }

    /// <summary>
    /// What a findings export says about itself that the findings cannot say for themselves: where the list came
    /// from, what sequence it is in, and which tiers it was allowed to contain.
    /// <para>
    /// Every member is something the SDK genuinely does not know. A <see cref="Projects.Project"/> is a pure in-memory
    /// model with no path, no filename and no provenance, so <see cref="SourceName"/> has to be supplied; the
    /// writer never re-sorts, so <see cref="Order"/> is the caller's own label for the sequence it handed over;
    /// and which severities a list was filtered to is a fact about the CALLER's filter, not about the findings
    /// that survived it — an export with no Info rows and an export that excluded the Info tier look identical
    /// from the inside.
    /// </para>
    /// <para>
    /// It lives in the root contract namespace rather than beside the writer: <c>Ihc.Vis.Reporting</c> is the
    /// report PIPELINE and is internal by architecture rule, while what a caller passes in is contract.
    /// <c>ReportMimeTypes</c> and <c>ReportKind</c> are here for the same reason.
    /// </para>
    /// </summary>
    public sealed record FindingExportOptions
    {
        /// <summary>All three tiers, in enum order — what an unfiltered export includes.</summary>
        public static EquatableArray<ValidationSeverity> AllSeverities { get; } =
            EquatableArray.CreateRange(Enum.GetValues<ValidationSeverity>());

        /// <summary>
        /// What the file names itself: the open document's name for a host, the corpus case name for the oracle.
        /// Null becomes an empty attribute rather than a missing one, so the root's shape never varies.
        /// </summary>
        public string? SourceName { get; init; }

        /// <summary>
        /// What sequence this is, recorded verbatim as <c>@order</c>. Free text, because the writer emits what it
        /// is handed and only the caller knows why that is the order: <c>production</c> for the engine's own
        /// deterministic key, <c>host:severity desc</c> for a column the user clicked.
        /// </summary>
        public string Order { get; init; } = "production";

        /// <summary>
        /// Which tiers the caller included. Recorded on EVERY export, including the empty case, because absence
        /// of a tier's findings and exclusion of the tier are different facts and only this one distinguishes
        /// them.
        /// </summary>
        public EquatableArray<ValidationSeverity> Severities { get; init; } = AllSeverities;

        /// <summary>An unfiltered production export of an unnamed source.</summary>
        public static FindingExportOptions Default { get; } = new();
    }
}
