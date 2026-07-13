#nullable enable
using System.Collections.Immutable;

namespace Ihc.Vis.Reporting
{
    // The render-ready backing model for the IHC project documentation reports. Every value here is already
    // display-final: blanks are resolved to "--" (per-product/module tables) or the empty string (flat
    // cross-reference tables), data-line addresses are decoded, the end-user omission filter is applied and
    // note propagation is resolved. A GUI (IHC OpenVisual, US-040) transforms these records 1-to-1 into HTML
    // with no further business logic. The field/order/omission contract is REPORT-P3 output-spec.md; the
    // builder is <see cref="ReportBuilder"/>, surfaced by ProjectAppService.Generate*Report.

    /// <summary>
    /// One masthead party block (installer or customer): the only three project-info fields any report renders
    /// (<c>@name/@address/@phone</c>), each already resolved to its display value ("--" when blank).
    /// </summary>
    public sealed record ReportPartyInfo(string Navn, string Adresse, string Telefon);

    /// <summary>The product family a per-product installation detail table was built from (drives its label set).</summary>
    public enum ReportProductKind
    {
        Dataline,
        Airlink,
        Rs485LedDimmer,
        Rs485Modem,
        Rs485SmsModem,
    }

    /// <summary>A label→value row of a per-product installation detail table (value already blank→"--").</summary>
    public sealed record ReportLabelValue(string Label, string Value);

    /// <summary>
    /// A row of a <c>product_dataline</c> terminal sub-table. <see cref="Address"/> is the render-ready
    /// <c>Indgang</c>/<c>Udgang</c> direction word plus the decoded data-line address ("?" when unassigned);
    /// <see cref="Wire"/> is the terminal cable colour (blank→"--").
    /// </summary>
    public sealed record ReportTerminalRow(string Terminal, string Address, string Wire);

    /// <summary>
    /// One per-product installation detail table: the family-specific label→value <see cref="Rows"/> and, for
    /// <see cref="ReportProductKind.Dataline"/> only, the terminal sub-table (<see cref="Terminals"/>, empty
    /// for every other family).
    /// </summary>
    public sealed record ProductDetailTable(
        ReportProductKind Kind,
        ImmutableArray<ReportLabelValue> Rows,
        ImmutableArray<ReportTerminalRow> Terminals);

    /// <summary>A row of the Datalinie input/output-module tables (§1/§2), values blank→"--".</summary>
    public sealed record ModuleRow(string Dataline, string ModuleType, string Locality, string Description);

    /// <summary>
    /// A row of the flat <c>Datalinie indgange</c>/<c>udgange</c> cross-reference tables (§5/§6). All eleven
    /// columns are raw values (blank→empty string, never "--"); <see cref="Address"/> is the decoded data-line
    /// address ("?" when unassigned). Product-derived columns are "?" only when the terminal has no product ancestor.
    /// </summary>
    public sealed record DatalineCrossReferenceRow(
        string Address, string Product, string Terminal, string Note, string Locality,
        string Position, string IdCode, string CableType, string CableNumber,
        string PowerGroup, string WireColour);

    /// <summary>A row of the flat <c>Specielle Produkter</c> table (§7, RS485 modems); raw values, blank→empty.</summary>
    public sealed record SpecialProductRow(
        string Product, string Terminal, string Note, string Locality, string Position,
        string IdCode, string WireColour0V, string WireColour24V,
        string WireColourRs485Minus, string WireColourRs485Plus);

    /// <summary>A row of the flat <c>S0 Device</c> table (§8); raw values, blank→empty.</summary>
    public sealed record S0DeviceRow(
        string Product, string Note, string Locality, string Position, string IdCode,
        string CableColourS0Minus, string CableColourS0Plus);

    /// <summary>
    /// The complete installation ("Installationsdokumentation") report model, sections in the fixed output
    /// order of output-spec §1.3. Every product/IO/module is present (the installation report never omits
    /// undocumented products — omission is end-user-report-only); rows keep Installation-pane document order
    /// except the flat/module tables, which sort by decoded address / <c>@dataline</c>.
    /// </summary>
    public sealed record InstallationReport(
        string Heading,
        ReportPartyInfo Installer,
        ReportPartyInfo Customer,
        ImmutableArray<ModuleRow> InputModules,
        ImmutableArray<ModuleRow> OutputModules,
        ImmutableArray<ProductDetailTable> ProductDetails,
        ImmutableArray<ProductDetailTable> ModemDetails,
        ImmutableArray<DatalineCrossReferenceRow> DatalineInputs,
        ImmutableArray<DatalineCrossReferenceRow> DatalineOutputs,
        ImmutableArray<SpecialProductRow> SpecialProducts,
        ImmutableArray<S0DeviceRow> S0Devices);

    /// <summary>
    /// One note sub-line under an end-user terminal: the <see cref="Text"/> resolved through the link to the
    /// driving function-block input's <c>@note</c>. <see cref="FbLocality"/> is the SCREEN-ONLY differing-FB
    /// locality suffix (the group of the driving block, rendered as "(…)" on screen only when it is not a
    /// prefix of the product's locality); empty when no suffix applies — the print transform ignores it.
    /// </summary>
    public sealed record EndUserNote(string Text, string FbLocality);

    /// <summary>An end-user terminal bullet and its note sub-lines (one per link; empty when the terminal is unlinked).</summary>
    public sealed record EndUserTerminal(string Name, ImmutableArray<EndUserNote> Notes);

    /// <summary>
    /// An end-user product block: its <see cref="Name"/>, the <see cref="Position"/> (empty when blank — the
    /// transform appends it only when non-empty), the <see cref="ProductIdentifier"/> (the product image key),
    /// and its terminals (inputs then outputs for wired products, inputs for airlink).
    /// </summary>
    public sealed record EndUserProduct(
        string Name, string Position, string ProductIdentifier,
        ImmutableArray<EndUserTerminal> Terminals);

    /// <summary>
    /// An end-user locality section: its <see cref="Name"/> and the end-user products it contains (only those
    /// flagged <c>enduser_report='yes'</c> — localities themselves are never omitted, so empty ones still
    /// render). <see cref="AnchorId"/> is the SCREEN anchor target (the <c>group/@id</c> token used by the
    /// screen table-of-contents and section anchor); the print transform anchors on <see cref="Name"/> instead.
    /// </summary>
    public sealed record EndUserLocality(
        string Name, string AnchorId, ImmutableArray<EndUserProduct> Products);

    /// <summary>
    /// The complete end-user ("Funktionsdokumentation") report model. <see cref="Localities"/> is every
    /// locality in Installation-pane order; the SCREEN-ONLY table-of-contents is the transform's projection of
    /// this same list (name + <see cref="EndUserLocality.AnchorId"/>), dropped by the print transform.
    /// </summary>
    public sealed record EndUserReport(
        string Heading,
        ImmutableArray<EndUserLocality> Localities);

    /// <summary>
    /// Placeholder for the deferred function-block ("Functionsblok dokumentation") report (US-041). Its
    /// per-field layout has not been transcribed from the vendor oracle yet, so this type is intentionally
    /// unpopulated; do not add fields until the layout is specified.
    /// </summary>
    public sealed record FunctionBlockReport();
}
