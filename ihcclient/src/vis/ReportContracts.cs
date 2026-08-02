#nullable enable
using System.Collections.Generic;

namespace Ihc.Vis
{
    /// <summary>Which of the three IHC documentation reports to generate (spec R3/R4).</summary>
    public enum ReportKind
    {
        /// <summary>"Funktionsdokumentation" — per locality → end-user-flagged product → terminals + linked FB-pin notes.</summary>
        Functions,

        /// <summary>"Installationsdokumentation" — module tables, per-locality component/wiring tables, cross-references.</summary>
        Installation,

        /// <summary>"Functionsblok dokumentation" — per function block: description, variables with values, program tree.</summary>
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

    /// <summary>The report output formats <see cref="ProjectAppService.GenerateReport(Projects.Project, ReportKind, ReportMode, string, System.IO.Stream, IReportIconProvider?)"/> accepts (spec R1/R3). Any other mimetype is rejected with a clear error.</summary>
    public static class ReportMimeTypes
    {
        /// <summary>A self-contained HTML page (screen + print styling, inline icon sprite).</summary>
        public const string Html = "text/html";

        /// <summary>Plain text with the default 1–3 character unicode icon stand-ins.</summary>
        public const string PlainText = "text/plain";
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
}
