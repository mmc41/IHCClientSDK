using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ihc_openvisual.Services;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// One editable field of the generic product dialog. Holds only what a control needs to render and what the
/// write-back needs to commit — the descriptor already decided everything else.
/// </summary>
public sealed partial class ProductDialogFieldViewModel : ObservableObject
{
    private readonly DialogDescriptorField _field;

    public ProductDialogFieldViewModel(DialogDescriptorField field)
    {
        _field = field;
        _value = field.Value ?? string.Empty;
        // The wrapper already normalizes default to empty, so the old SuggestionsOrEmpty accessor is gone.
        // Materialized back to ImmutableArray here because Suggestions is XAML-bound (ComboBox ItemsSource)
        // and this keeps the binding's runtime type exactly what it was.
        Suggestions = field.Suggestions.AsImmutableArray();
    }

    /// <summary>The Danish label, with a repeat's key already substituted.</summary>
    public string Caption => _field.Caption;

    /// <summary><c>dlg.&lt;group&gt;.&lt;field&gt;</c> — stamped on the control so assistive technology and the
    /// UI drivers can address it without knowing the family.</summary>
    public string AutomationId => _field.AutomationId;

    public bool IsReadOnly => _field.ReadOnly;

    /// <summary>Typing suggestions for a <see cref="DialogControlKind.ComboSuggest"/> field; empty otherwise.</summary>
    public ImmutableArray<string> Suggestions { get; }

    public int? Minimum => _field.Minimum;

    public int? Maximum => _field.Maximum;

    /// <summary>How many of the group's columns this field occupies (clamped when the rows are packed).</summary>
    public int ColumnSpan => _field.ColumnSpan;

    /// <summary>Which editor realizes this field. The view's template selector switches on it; nothing else does,
    /// which is why there is no per-kind boolean here for a binding to drift away from.</summary>
    public DialogControlKind Control => _field.Control;

    /// <summary>
    /// Whether the shared caption block above the editor is drawn.
    /// <para>False for a checkbox and true for everything else: a tick box carries its label to its right, as
    /// its own content, and drawing the caption as well would show the sentence twice. The one place a control
    /// kind changes the SURROUNDING markup rather than the editor, so it is stated here rather than inferred in
    /// XAML from the kind.</para>
    /// </summary>
    public bool HasSeparateCaption => Control != DialogControlKind.Checkbox;

    /// <summary>
    /// The checkbox view of <see cref="Value"/>: the file spells this flag <c>yes</c>/<c>no</c> and the
    /// descriptor carries that spelling through untouched, so the translation to a tick lives at the very edge,
    /// in the one control that needs it. Setting it goes back through <see cref="Value"/>, which is what
    /// <see cref="PendingEdit"/> reads — a tick therefore commits as <c>yes</c>, never as <c>True</c>.
    /// </summary>
    public bool IsChecked
    {
        get => string.Equals(Value, "yes", StringComparison.Ordinal);
        set => Value = value ? "yes" : "no";
    }

    /// <summary>The SDK rule this field's value must satisfy, or null when anything goes.</summary>
    public DialogValueRule? Rule => _field.Rule;

    /// <summary>Whether the current value satisfies <see cref="Rule"/>. The rule itself lives in the SDK and is
    /// pinned there — the GUI consults it, and never restates what "valid" means.</summary>
    public bool IsSatisfied => Rule?.IsSatisfiedBy(Value) ?? true;

    /// <summary>The refusal to show when this field is the offending one. It NAMES the field, because a dialog
    /// with thirty boxes on screen leaves the installer hunting otherwise (US-013).</summary>
    public string RefusalSentence => $"{Caption}: {Rule?.Refusal}";

    // IsChecked is a view OVER Value, so it has to be told when Value moves — otherwise a tick box bound to it
    // keeps showing the old state after anything else writes the field.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChecked))]
    private string _value;

    /// <summary>The edit this field would commit, or null when its value is unchanged.</summary>
    public ProductDialogEdit? PendingEdit =>
        IsReadOnly || string.Equals(Value, _field.Value ?? string.Empty, StringComparison.Ordinal)
            ? null
            : new ProductDialogEdit(_field.Target, _field.Attribute, Value);
}

/// <summary>
/// One rendered row of a group: the fields on it, and how many cells the row divides into.
/// <para><see cref="Cells"/> is the group's column count for an ordinary row and 1 for a row holding a
/// single full-width field, so the renderer needs no span arithmetic — each row is its own small uniform
/// grid. A short final row keeps the group's cell count, so its last field does not stretch to fill the
/// gap; the vendor leaves that space empty too.</para>
/// </summary>
public sealed class ProductDialogRow(IReadOnlyList<ProductDialogFieldViewModel> fields, int cells)
{
    public IReadOnlyList<ProductDialogFieldViewModel> Fields { get; } = fields;

    public int Cells { get; } = cells;
}

/// <summary>
/// One of the two terminal grids of US-012 — <i>Indgange</i> or <i>Udgange</i>.
///
/// <para>The two sides differ only in their captions, their ids, their rows and which selection the
/// <i>Konfigurer</i> button reads, so they are DATA and render from one template. Authored as a copy-paste
/// pair they were nineteen lines differing in nine tokens, and the pair is what forced the button to recover
/// its list by walking the visual tree for a hard-coded control name — a name cannot be bound (Avalonia:
/// "once the element is added to a logical tree, its name cannot be changed"), so one shared template cannot
/// give the two lists distinct names, and the handler reads this object instead.</para>
///
/// <para>The ids are spelled out rather than derived from the caption: they are the app's automation surface,
/// and a driver's grep for <c>dlg.terminaler.konfigurerIndgang</c> has to find it.</para>
/// </summary>
public sealed partial class ProductDialogTerminalGridViewModel : ObservableObject
{
    private ProductDialogTerminalGridViewModel(
        string name, string automationId, string buttonCaption, string buttonAutomationId,
        IEnumerable<ProductTerminal> rows)
    {
        Name = name;
        AutomationId = automationId;
        ButtonCaption = buttonCaption;
        ButtonAutomationId = buttonAutomationId;
        Rows = new ObservableCollection<ProductTerminal>(rows);
        // Pre-selected on open, which is what lets the Konfigurer route treat an EMPTY selection as "the
        // installer cleared it" and address nothing, rather than falling back to the first row.
        SelectedRow = Rows.FirstOrDefault();
    }

    public static ProductDialogTerminalGridViewModel ForInputs(IEnumerable<ProductTerminal> rows) =>
        new("Indgange", "dlg.terminaler.indgange",
            "Konfigurer indgang", "dlg.terminaler.konfigurerIndgang", rows) { IsOutputSide = false };

    public static ProductDialogTerminalGridViewModel ForOutputs(IEnumerable<ProductTerminal> rows) =>
        new("Udgange", "dlg.terminaler.udgange",
            "Konfigurer udgang", "dlg.terminaler.konfigurerUdgang", rows) { IsOutputSide = true };

    /// <summary>The side's name, and the list's accessible name — <c>Indgange</c> or <c>Udgange</c>.</summary>
    public string Name { get; }

    /// <summary>The vendor's caption, which spells out that a row is clicked to address it.</summary>
    public string Caption => $"{Name} <klik for at konfigurere>";

    public string AutomationId { get; }

    public string ButtonCaption { get; }

    public string ButtonAutomationId { get; }

    public ObservableCollection<ProductTerminal> Rows { get; }

    /// <summary>Whether this side has any terminals. An EMPTY grid is still drawn — with its Configure button
    /// disabled — because an absent section reads as an unfinished dialog (US-012 MUST).</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>The row <i>Konfigurer</i> addresses. Bound two-way, so it is the same selection the installer
    /// sees; null means they cleared it.</summary>
    [ObservableProperty]
    public partial ProductTerminal? SelectedRow { get; set; }

    /// <summary>
    /// Replaces this side's rows with the freshly computed ones, keeping the selection on the same TERMINAL.
    /// </summary>
    /// <param name="all">Every terminal of the product; this grid takes the ones for its own direction.</param>
    internal void Replace(IEnumerable<ProductTerminal> all)
    {
        string? selected = SelectedRow?.PinId;
        Rows.Clear();
        foreach (ProductTerminal row in all.Where(r => r.IsOutput == IsOutputSide))
        {
            Rows.Add(row);
        }
        SelectedRow = Rows.FirstOrDefault(r => r.PinId == selected) ?? Rows.FirstOrDefault();
    }

    /// <summary>Which direction this grid shows — decided once, at construction, by which factory built it.</summary>
    private bool IsOutputSide { get; init; }
}

/// <summary>
/// The sensors' <i>Indstillinger</i> grid (T070) — a SIBLING of
/// <see cref="ProductDialogTerminalGridViewModel"/>, not a third face of the group that hosts it.
///
/// <para>A sibling because the two grids answer the same question in the same way: a list of element-backed rows,
/// one of which is selected, replaced wholesale when the visit re-projects. Hanging the rows and the selection off
/// the group instead would have put a list's state on a thing that is mostly a run of fields, and left the
/// selection with nowhere to be restored to across a refresh.</para>
///
/// <para>What it does NOT share is a Konfigurer button: the vendor's settings row is opened by clicking the row
/// itself, so this grid has rows and a selection and nothing else.</para>
/// </summary>
public sealed partial class ProductDialogSettingsGridViewModel : ObservableObject
{
    internal ProductDialogSettingsGridViewModel(IEnumerable<ProductSetting> rows) =>
        Rows = new ObservableCollection<ProductSetting>(rows);

    /// <summary>The list's accessible name.</summary>
    public string Name => "Indstillinger";

    /// <summary>The vendor's caption, which spells out that a row is clicked to configure it.</summary>
    public string Caption => $"{Name} <klik for at konfigurere>";

    public string AutomationId => "dlg.indstillinger.liste";

    public ObservableCollection<ProductSetting> Rows { get; }

    /// <summary>
    /// The selected setting. Bound two-way, so it is the same row the installer sees.
    /// <para>Nothing is selected on open, unlike the terminal grids: those pre-select because their Konfigurer
    /// button needs a standing target even before the installer has pointed at anything, and this grid has no
    /// button — the row IS the gesture. A highlighted row on open would claim a choice nobody made.</para>
    /// </summary>
    [ObservableProperty]
    public partial ProductSetting? SelectedRow { get; set; }

    /// <summary>
    /// Replaces the rows with the freshly computed ones, keeping the selection on the same setting ELEMENT — the
    /// same rule the terminal grid follows, and for the same reason: a row's position is not its identity.
    /// </summary>
    internal void Replace(IEnumerable<ProductSetting> settings)
    {
        ElementId? selected = SelectedRow?.Id;
        Rows.Clear();
        foreach (ProductSetting row in settings)
        {
            Rows.Add(row);
        }
        // Null when the selection's element is gone, rather than sliding to a neighbour: a refresh must not
        // silently re-point a selection at a different setting.
        SelectedRow = selected is { } id ? Rows.FirstOrDefault(r => r.Id == id) : null;
    }
}

/// <summary>One captioned (or uncaptioned) group of the generic dialog.</summary>
public sealed class ProductDialogGroupViewModel
{
    public ProductDialogGroupViewModel(
        DialogDescriptorGroup group,
        IReadOnlyList<ProductTerminal> terminals,
        IReadOnlyList<ProductSetting> settings)
    {
        Caption = group.Caption;
        Id = group.Id;
        IsCollapsible = group.Collapsible;
        Columns = Math.Max(1, group.Columns);
        Fields = new ObservableCollection<ProductDialogFieldViewModel>(
            group.Fields.Select(f => new ProductDialogFieldViewModel(f)));
        Widgets = group.Widgets;

        if (HasTerminalGrids)
        {
            TerminalGrids =
            [
                ProductDialogTerminalGridViewModel.ForInputs(terminals.Where(t => !t.IsOutput)),
                ProductDialogTerminalGridViewModel.ForOutputs(terminals.Where(t => t.IsOutput)),
            ];
        }
        if (HasSettingsGrid)
        {
            SettingsGrid = new ProductDialogSettingsGridViewModel(settings);
        }

        DisplayFields = group.ColumnMajor ? Transposed(Fields, Columns) : Fields;
        Rows = PackIntoRows(DisplayFields, Columns);
    }

    /// <summary>
    /// Packs fields into rows of at most <paramref name="columns"/> cells, giving a field that spans the
    /// full width a row to itself.
    /// <para>A span is CLAMPED to the group's width: a shared fragment declaring 2 is inert in a
    /// one-column group, which is how the modem's identity block stays one field per row while using the
    /// same <c>Note</c> as the wired dialog.</para>
    /// </summary>
    private static List<ProductDialogRow> PackIntoRows(
        IReadOnlyList<ProductDialogFieldViewModel> fields, int columns)
    {
        var rows = new List<ProductDialogRow>();
        var current = new List<ProductDialogFieldViewModel>();

        void Flush()
        {
            if (current.Count > 0)
            {
                rows.Add(new ProductDialogRow([.. current], columns));
                current.Clear();
            }
        }

        foreach (ProductDialogFieldViewModel field in fields)
        {
            if (Math.Min(field.ColumnSpan, columns) >= columns && columns > 1)
            {
                Flush();
                rows.Add(new ProductDialogRow([field], 1));
                continue;
            }
            current.Add(field);
            if (current.Count == columns)
            {
                Flush();
            }
        }
        Flush();
        return rows;
    }

    /// <summary>
    /// Re-sequences <paramref name="fields"/> so a ROW-major grid of <paramref name="columns"/> columns
    /// draws them reading down each column.
    /// <para>With <c>r = ceil(n / columns)</c> rows, the cell at (row <c>i</c>, column <c>j</c>) must show
    /// the item at <c>j*r + i</c>. A ragged last column leaves indices past the end, which are simply
    /// skipped — the resulting short tail is what the vendor shows too, since it fills complete columns
    /// first.</para>
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
        Justification = "The parameter states what this helper does with the collection - reads it. " +
                        "Naming ObservableCollection would force every caller to own one and let this " +
                        "method mutate a bound collection, to elide one dispatch over a dialog's fields.")]
    private static IReadOnlyList<ProductDialogFieldViewModel> Transposed(
        IReadOnlyList<ProductDialogFieldViewModel> fields, int columns)
    {
        if (columns <= 1 || fields.Count == 0)
        {
            return fields;
        }
        int rows = (fields.Count + columns - 1) / columns;
        var ordered = new List<ProductDialogFieldViewModel>(fields.Count);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int index = (column * rows) + row;
                if (index < fields.Count)
                {
                    ordered.Add(fields[index]);
                }
            }
        }
        return ordered;
    }

    // ── The two hand-written composites (D12) ───────────────────────────────────────────────────────────────
    // The DESCRIPTOR says whether a slot applies; it carries no rows, because terminal rows are element data and
    // not dialog metadata. The rows arrive alongside it, from the caller that already reads the product.

    /// <summary>Whether the group is drawn behind a disclosure — the descriptor's own hint.</summary>
    public bool IsCollapsible { get; }

    /// <summary>
    /// Whether the plain captioned box is what draws this group. A collapsible group carries its caption on the
    /// disclosure instead, so drawing both would show the caption twice — once on a box and once on the header.
    /// </summary>
    public bool ShowsPlainBox => HasCaption && !IsCollapsible;

    /// <summary>The disclosure's automation id, so a driver can expand and collapse the group by name.</summary>
    public string DisclosureAutomationId => $"dlg.{Id}.udvid";

    /// <summary>Whether this group hosts the input/output terminal grids (US-012).</summary>
    public bool HasTerminalGrids => Widgets.Contains(DialogWidgetKind.TerminalGrids);

    /// <summary>
    /// The two terminal grids when this group hosts them, and NOTHING otherwise.
    /// <para>The widget markup lives inside the per-group item template, so binding its visibility to a
    /// boolean would BUILD it for every group and hide all but one: several list boxes and buttons
    /// carrying the same automation ids, of which only one is reachable. Driving it from a collection that
    /// is empty for the other groups means they construct nothing at all. Same lesson as the field editors
    /// in T028 — hiding is not the same as not building.</para>
    /// <para>Always BOTH sides when hosted, never only the populated one: a product with no outputs shows an
    /// empty <i>Udgange</i> grid with its button disabled (US-012 MUST).</para>
    /// </summary>
    public IReadOnlyList<ProductDialogTerminalGridViewModel> TerminalGrids { get; } = [];

    /// <summary>Whether this group hosts the sensors' <i>Indstillinger</i> grid (T070).</summary>
    public bool HasSettingsGrid => Widgets.Contains(DialogWidgetKind.SettingsGrid);

    /// <summary>The 0-or-1 section for the settings grid, for the same reason as the other two.</summary>
    public IReadOnlyList<ProductDialogSettingsGridViewModel> SettingsSection =>
        SettingsGrid is { } grid ? [grid] : [];

    /// <summary>The settings grid this group hosts, or null where the descriptor declared none.</summary>
    public ProductDialogSettingsGridViewModel? SettingsGrid { get; }

    /// <summary>The group box's title, or null for an uncaptioned run of fields (no box is drawn).</summary>
    public string? Caption { get; }

    /// <summary>The preset's group id — the stem of the automation ids inside it.</summary>
    public string Id { get; }

    /// <summary>True when this group draws a captioned box.</summary>
    public bool HasCaption => Caption is not null;

    /// <summary>How many columns the fields flow into — the descriptor's hint, honoured as a uniform grid.</summary>
    public int Columns { get; }

    public ObservableCollection<ProductDialogFieldViewModel> Fields { get; }

    /// <summary>
    /// The fields in the order the renderer must draw them, which is not always the order they are
    /// declared in.
    /// <para>The grid fills row by row. To make it READ down each column — which is how the vendor's
    /// telephone grid is laid out, 1–10 then 11–20 then 21–30 — the sequence handed to it has to be
    /// transposed: 1, 11, 21, 2, 12, 22, … Identical to <see cref="Fields"/> for a row-major group and
    /// for any single-column group.</para>
    /// <para>Deliberately a second sequence rather than a re-ordering of <see cref="Fields"/>: slot
    /// <i>n</i> must stay at index <i>n-1</i>, because the write-back and the validation tests address
    /// slots by position. Reordering in place would silently redefine what "slot 17" means.</para>
    /// </summary>
    public IReadOnlyList<ProductDialogFieldViewModel> DisplayFields { get; }

    /// <summary>
    /// <see cref="DisplayFields"/> packed into rows, which is what the renderer actually lays out.
    /// <para>Rows rather than one flat uniform grid, because a field may take the WHOLE row — the vendor
    /// gives <c>Note</c> the full width, and everything after it pairs up beneath. A uniform grid cannot
    /// express that: it would put the next field beside Note and shift every later field one cell.</para>
    /// </summary>
    public IReadOnlyList<ProductDialogRow> Rows { get; }

    /// <summary>Hand-written composite widgets that belong in this group (terminal grids, advanced dimmer).</summary>
    /// <remarks>Consumed through the wrapper's own read surface — only the three <c>Has…</c> predicates above
    /// touch it, never a binding, so there is nothing to materialize it for.</remarks>
    public EquatableArray<DialogWidgetKind> Widgets { get; }
}

/// <summary>
/// The generic product-dialog view-model: a composed <see cref="ProductDialogDescriptor"/> turned into bindable
/// rows, and the pending edits turned back into the write-back's triples.
/// <para>It knows nothing about product families. Everything family-specific was decided by the composer, which is
/// what lets ONE window serve all five families and the open-world fallback.</para>
/// </summary>
public sealed partial class ProductDialogViewModel : ObservableObject
{
    /// <param name="descriptor">The composed dialog — every field, caption, value, rule and write target.</param>
    /// <param name="terminals">The product's addressing rows, for the groups whose descriptor declares the
    /// terminal-grid widget. Empty for every family that declares none, which is all but the wired one.</param>
    /// <param name="settings">The product's configurable settings, for the group whose descriptor declares
    /// the settings-grid widget. Empty for every product that declares none, which is all but six.</param>
    public ProductDialogViewModel(
        ProductDialogDescriptor descriptor,
        IReadOnlyList<ProductTerminal>? terminals = null,
        IReadOnlyList<ProductSetting>? settings = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Title = descriptor.Title;
        Groups = new ObservableCollection<ProductDialogGroupViewModel>(
            descriptor.Groups.Select(g => new ProductDialogGroupViewModel(g, terminals ?? [], settings ?? [])));

        // A refusal describes a state; editing any field ends that state, so the message comes down. Without this
        // the installer fixes the number and is still looking at a complaint about it.
        foreach (ProductDialogFieldViewModel field in AllFields)
            field.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProductDialogFieldViewModel.Value))
                    Refusal = null;
            };
    }

    public string Title { get; }

    public ObservableCollection<ProductDialogGroupViewModel> Groups { get; }

    /// <summary>
    /// Re-projects the grids from freshly computed rows, keeping each grid's selection on the SAME terminal.
    /// </summary>
    /// <remarks>
    /// The rows are replaced, not edited: they are values, and the caller has already worked out what they
    /// should be. Selection is restored by ELEMENT rather than by index — by pin in the terminal grids, by
    /// setting id in the Indstillinger grid — because a row's position is not its identity: a re-projection that
    /// reordered anything would otherwise move the installer's selection to a different row without touching it.
    /// </remarks>
    public void Refresh(IReadOnlyList<ProductTerminal> terminals, IReadOnlyList<ProductSetting> settings)
    {
        foreach (ProductDialogGroupViewModel group in Groups)
        {
            foreach (ProductDialogTerminalGridViewModel grid in group.TerminalGrids)
            {
                grid.Replace(terminals);
            }
            group.SettingsGrid?.Replace(settings);
        }
    }

    /// <summary>Every field, flattened — what the tests and the commit walk.</summary>
    public IEnumerable<ProductDialogFieldViewModel> AllFields => Groups.SelectMany(g => g.Fields);

    /// <summary>
    /// The edits to commit: only the fields the installer actually changed.
    /// <para>Changed-fields-only is not an optimisation. Submitting every field would rewrite attributes the
    /// dialog never showed the installer as editable, and would turn an untouched OK into a commit — where the
    /// engine's <c>NoChange</c> is what tells the insert flow that nothing needs undoing.</para>
    /// </summary>
    public ImmutableArray<ProductDialogEdit> PendingEdits =>
        [.. AllFields.Select(f => f.PendingEdit).Where(e => e is not null).Select(e => e!.Value)];

    /// <summary>The refusal currently on screen, or null when nothing is wrong.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRefusal))]
    private string? _refusal;

    public bool HasRefusal => Refusal is not null;

    /// <summary>
    /// Whether OK may commit. A field whose value breaks its SDK rule is refused OUT LOUD — the message names the
    /// field and the dialog stays open so the value can be fixed — rather than being silently dropped or written
    /// through into the project.
    /// <para>The rule and its wording both come from the SDK (<see cref="DialogValueRule"/>); this decides only
    /// WHEN to consult it and WHICH field to name, so there is no second definition of "valid" in the GUI.</para>
    /// </summary>
    public bool TryCommit()
    {
        ProductDialogFieldViewModel? offender = AllFields.FirstOrDefault(field => !field.IsSatisfied);
        Refusal = offender?.RefusalSentence;
        return offender is null;
    }
}
