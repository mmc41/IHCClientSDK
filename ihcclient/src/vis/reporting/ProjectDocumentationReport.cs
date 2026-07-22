#nullable enable
using System.Collections.Immutable;

namespace Ihc.Vis
{
    // The COMBINED render-ready project-documentation report model (D14/T020). It composes the three legacy report
    // models (installation / end-user / function-block) in the fixed documentation order and adds the
    // switch-supporting data the app needs to render and TOGGLE without recomputing anything: per-section and
    // per-element internal ids + inclusion flags, a raw-blank-beside-display value type, and a unified locality
    // representation. Placed in the root Ihc.Vis namespace beside the other render-ready records (NOT in
    // Ihc.Vis.Reporting) so the app can consume it without tripping the Gui_DoesNotDependOn_Reporting arch rule. The
    // builder is ReportBuilder.BuildProjectDocumentation, surfaced by ProjectAppService.GenerateProjectDocumentationReport.

    /// <summary>Which of the three fixed documentation sections a report part belongs to (D14). The combined report
    /// orders its <see cref="ProjectDocumentationReport.Sections"/> in exactly this enum order.</summary>
    public enum ReportSectionKind
    {
        Installation,
        EndUser,
        FunctionBlock,
    }

    /// <summary>A value cell carrying BOTH the <see cref="Raw"/> value (blank preserved) and the <see cref="Display"/>
    /// value (a blank resolved to the report placeholder), so the app can switch between showing blanks and the
    /// placeholder without recomputing (D14).</summary>
    public sealed record ReportValue(string Raw, string Display)
    {
        /// <summary>Whether the underlying value was blank — the raw text has no non-whitespace content.</summary>
        public bool IsBlank => string.IsNullOrWhiteSpace(Raw);
    }

    /// <summary>A unified locality across every section (D14): its internal <c>group/@id</c> token, its name, and its
    /// default inclusion. The app toggles a locality once and every section honours it.</summary>
    public sealed record ReportLocality(string Id, string Name, bool IncludedByDefault);

    /// <summary>One switchable section in the fixed documentation order (D14): its stable id, its
    /// <see cref="ReportSectionKind"/>, its heading, and its default inclusion. The app renders sections in list
    /// order and toggles a section by id.</summary>
    public sealed record ReportSectionEntry(string Id, ReportSectionKind Kind, string Heading, bool IncludedByDefault);

    /// <summary>One switchable product or function block across the report (D14): its internal element id, display
    /// name, the section it belongs to, and its default inclusion — the hook the per-product inclusion switch binds to.</summary>
    public sealed record ReportElementRef(string Id, string Name, ReportSectionKind Section, bool IncludedByDefault);

    /// <summary>The Projekt identity block (US-039, T023): the project's description, number and programmer, each as a
    /// raw/display <see cref="ReportValue"/>. Rendered as the report's Projekt section; its <see cref="Programmer"/> is
    /// also the heading's programmer, so heading and section always agree.</summary>
    public sealed record ReportProjektInfo(ReportValue Description, ReportValue Number, ReportValue Programmer);

    /// <summary>The technical detail of one LINKED product terminal (US-040/US-073, T024): the product and terminal it
    /// belongs to, the <see cref="LinkDisplay"/> path to what drives it (<c>-&gt; &lt;FB input&gt; -&gt; &lt;function
    /// block&gt; -&gt; &lt;its locality&gt;</c>), and the <see cref="FunctionNote"/> behaviour note resolved from that
    /// driving function-block input. Both were absent from the technical output.</summary>
    public sealed record ReportTerminalDetail(string Product, string Terminal, string LinkDisplay, ReportValue FunctionNote);

    /// <summary>One row of the consolidated Kabler (cabling) table (US-073, T025): one ADDRESSED terminal across
    /// inputs and outputs, in the vendor column order — wire colour, data-line address, its module + module location,
    /// light group, id-code, locality, position, product and the Indgang/Udgang direction. Unaddressed terminals are
    /// excluded from the table entirely.</summary>
    public sealed record ReportKablerRow(
        string Ledningsfarve, string Adresse, string Modul, string ModulLokation, string Lysgruppe,
        string IdKode, string Lokalitet, string Placering, string Produkt, string IndUdgang);

    /// <summary>One documentation-completeness issue ("Fejl i dokumentation", US-072, T027): a single missing/blank
    /// item on a wired product or terminal, located by <see cref="Locality"/> → <see cref="Product"/> →
    /// <see cref="Terminal"/> (the terminal is empty for a product-level issue). Only elements WITH an issue produce
    /// rows; a fully-documented element yields none, and an empty list renders as "none found".</summary>
    public sealed record ReportCompletenessRow(string Locality, string Product, string Terminal, string Problem);

    /// <summary>A function-block pin in the deep FB report (US-041, T028): its name and behaviour note.</summary>
    public sealed record ReportFbPin(string Name, string Note);

    /// <summary>A function-block setting / internal variable in the deep FB report (US-041, T028): its name and its
    /// value (rendered as <c>name = value</c>).</summary>
    public sealed record ReportFbVariable(string Name, string Value);

    /// <summary>The deep per-block layout of the function-block logic report (US-041, T028): the block's description,
    /// its input/output pins with notes, its settings and internal variables as name=value, and a flattened program
    /// <see cref="Outline"/> (events → commands, sub-programs with conditions and commands, scene invocations). An
    /// unprogrammed block has <see cref="IsEmpty"/> set and renders as "Tom blok".</summary>
    public sealed record ReportFbBlock(
        string Name, string Description,
        ImmutableArray<ReportFbPin> Inputs, ImmutableArray<ReportFbPin> Outputs,
        ImmutableArray<ReportFbVariable> Settings, ImmutableArray<ReportFbVariable> InternalVariables,
        ImmutableArray<string> Outline, bool IsEmpty);

    /// <summary>
    /// The combined, render-ready project-documentation report (D14/T020): the three legacy sub-reports composed in
    /// the fixed section order (installation → end-user → function-block), plus the switch-supporting data the app
    /// renders and toggles without recomputing — the ordered <see cref="Sections"/> (ids + inclusion flags), the
    /// per-element <see cref="Elements"/> (internal ids + inclusion flags), the unified <see cref="Localities"/>, the
    /// masthead <see cref="ProjectName"/> as a raw/display <see cref="ReportValue"/>, the <see cref="Projekt"/>
    /// identity block (description/number/programmer, US-039/T023), and the report <see cref="GeneratedAt"/>
    /// generation timestamp (T022, from an injected clock). The app applies switches and computes nothing; the three
    /// legacy models still build for now.
    /// </summary>
    public sealed record ProjectDocumentationReport(
        ReportValue ProjectName,
        ReportProjektInfo Projekt,
        string GeneratedAt,
        ImmutableArray<ReportSectionEntry> Sections,
        ImmutableArray<ReportLocality> Localities,
        ImmutableArray<ReportElementRef> Elements,
        ImmutableArray<ReportTerminalDetail> TerminalDetails,
        ImmutableArray<ReportKablerRow> Kabler,
        ModuleAddressMap ModuleMap,
        ImmutableArray<ReportCompletenessRow> Completeness,
        ImmutableArray<ReportFbBlock> FunctionBlocks,
        InstallationReport Installation,
        EndUserReport EndUser,
        FunctionBlockReport FunctionBlock);
}
