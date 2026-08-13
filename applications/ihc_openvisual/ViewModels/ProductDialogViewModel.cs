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
        Suggestions = field.SuggestionsOrEmpty;
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

/// <summary>One captioned (or uncaptioned) group of the generic dialog.</summary>
public sealed class ProductDialogGroupViewModel
{
    public ProductDialogGroupViewModel(
        DialogDescriptorGroup group,
        IReadOnlyList<ProductTerminal> terminals,
        IReadOnlyList<ProductSetting> settings)
    {
        Caption = group.Caption;
        Columns = Math.Max(1, group.Columns);
        Fields = new ObservableCollection<ProductDialogFieldViewModel>(
            group.Fields.Select(f => new ProductDialogFieldViewModel(f)));
        Widgets = group.Widgets;

        if (HasTerminalGrids)
        {
            Inputs = new ObservableCollection<ProductTerminal>(terminals.Where(t => !t.IsOutput));
            Outputs = new ObservableCollection<ProductTerminal>(terminals.Where(t => t.IsOutput));
        }
        if (HasSettingsGrid)
        {
            Settings = new ObservableCollection<ProductSetting>(settings);
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
    private static IReadOnlyList<ProductDialogRow> PackIntoRows(
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

    /// <summary>Whether this group hosts the input/output terminal grids (US-012).</summary>
    public bool HasTerminalGrids => Widgets.Contains(DialogWidgetKind.TerminalGrids);

    /// <summary>Whether this group hosts the wireless dimmer's <i>Avanceret</i> button (US-015).</summary>
    public bool HasAdvancedButton => Widgets.Contains(DialogWidgetKind.AdvancedDimmerButton);

    /// <summary>
    /// This group, once, when it hosts the terminal grids — and nothing otherwise.
    /// <para>The widget markup lives inside the per-group item template, so binding its visibility to a
    /// boolean would BUILD it for every group and hide all but one: several list boxes and buttons
    /// carrying the same automation ids, of which only one is reachable. Driving it from a 0-or-1
    /// collection means the non-hosting groups construct nothing at all. Same lesson as the field editors
    /// in T028 — hiding is not the same as not building.</para>
    /// </summary>
    public IReadOnlyList<ProductDialogGroupViewModel> TerminalSection =>
        HasTerminalGrids ? [this] : [];

    /// <summary>The same 0-or-1 trick for the advanced-dimmer button, for the same reason.</summary>
    public IReadOnlyList<ProductDialogGroupViewModel> AdvancedSection =>
        HasAdvancedButton ? [this] : [];

    /// <summary>Whether this group hosts the sensors' <i>Indstillinger</i> grid (T070).</summary>
    public bool HasSettingsGrid => Widgets.Contains(DialogWidgetKind.SettingsGrid);

    /// <summary>The 0-or-1 section for the settings grid, for the same reason as the other two.</summary>
    public IReadOnlyList<ProductDialogGroupViewModel> SettingsSection =>
        HasSettingsGrid ? [this] : [];

    /// <summary>The configurable settings shown in that grid, in declared order.</summary>
    public ObservableCollection<ProductSetting> Settings { get; } = [];

    public ObservableCollection<ProductTerminal> Inputs { get; } = [];

    public ObservableCollection<ProductTerminal> Outputs { get; } = [];

    public bool HasInputs => Inputs.Count > 0;

    public bool HasOutputs => Outputs.Count > 0;

    /// <summary>The group box's title, or null for an uncaptioned run of fields (no box is drawn).</summary>
    public string? Caption { get; }

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
    public ImmutableArray<DialogWidgetKind> Widgets { get; }
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
