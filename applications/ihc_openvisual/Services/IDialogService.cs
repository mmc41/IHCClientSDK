using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Validation;
using Ihc.Vis.Session;

namespace ihc_openvisual.Services;

/// <summary>The seam the shared report picker is shown through (R12/D4): a marker for the picker view-model,
/// keeping <see cref="IDialogService"/> uncoupled from the concrete ViewModels type. The dialog service passes
/// the instance to the picker window as its DataContext.</summary>
public interface IReportPickerViewModel;

/// <summary>
/// Which of the two documentation-report formats the picker chose (R12/D4) — the value that travels from its
/// format dropdown through the report workflow to the save dialog.
/// </summary>
/// <remarks>
/// An enum rather than the facade mimetype string it eventually becomes. The mimetype is what
/// <c>ProjectAppService.GenerateReport</c> takes, and it stays a string there because that is the shipped SDK
/// contract; but between the dropdown and that one call it was a string carrying a value the SDK's own report
/// class published and its report generator rejected. Two members means an unrepresentable format cannot be
/// constructed, so no arm anywhere has to throw on one.
/// </remarks>
public enum ReportFormat
{
    /// <summary>A self-contained HTML page, generated with the app's SVG icon mapping.</summary>
    Html,

    /// <summary>Plain text, generated with the SDK's default unicode icon stand-ins.</summary>
    Text,
}

/// <summary>The installer's answer to a "save changes before closing?" prompt.</summary>
public enum SaveChangesResult
{
    Save,
    Discard,
    Cancel
}

/// <summary>The edited values returned from the element Properties dialog (US-007): the new name and note.</summary>
/// <param name="ConditionsOr">The logical operator a <c>Betingelser</c> group combines its conditions with —
/// <c>true</c> for OR, <c>false</c> for AND — or <c>null</c> for every other node type, which has no such field.
/// The reference application shows it as a captioned <i>Logisk betingelse</i> combo in that group's own dialog
/// (alignment F-48).</param>
public sealed record PropertiesResult(string Name, string Note, bool? ConditionsOr = null);

/// <summary>
/// The read-only provenance of a function block that came from the LIBRARY (uxparity S-19): which library block it
/// was stamped from, its number and version, when it was made and by whom. Shown as a second, non-editable group
/// under the editable Name/Note — a block authored from scratch has none of this and the group is absent.
/// </summary>
public sealed record LibraryOrigin(string Name, string Number, string Version, string Created, string Developer);

/// <summary>The current values shown by the ordinary-variable Properties dialog (US-027, T016): name, note, and the
/// typed initial value whose <see cref="ResourceInitialValue.Kind"/> selects the value control (a Bool checkbox, a
/// Number box, a Time h/m/s(/ms) group, or nothing for <see cref="ResourceValueKind.None"/>).</summary>
/// <param name="HelpNote">The SECOND documentation field (US-027/W5, the <c>note-2</c> attribute): the
/// installer-facing help text shown alongside the function documentation. Defaults to blank so a caller that has
/// none is unaffected.</param>
/// <param name="SaveOnPowerLoss">The <c>backup</c> flag the vendor shows as <i>Ved strømsvigt → Gem aktuel
/// værdi</i> (FR-7.2, alignment F-27).</param>
/// <param name="ShowMilliseconds">Selects the time editor's shape: the original shows <c>00:00:00,000</c> for a
/// <c>resource_timer</c>/<c>resource_timertime</c> and <c>00.00.00</c> for a <c>resource_time</c>, which declares
/// no millisecond at all (alignment F-42).</param>
/// <param name="DecimalPlaces">The precision of the decimal editor, which is per type and was measured field by
/// field: kW/kWh show <c>0,000</c>, Kommatal <c>0,00</c>, Fugtighed/Temperatur <c>0,0</c>, and W/Wh a plain
/// <c>0</c>. It governs the SCREEN only — every one of these types stores two fraction digits (F-41/F-44) — and it
/// also rounds what the user types, which is how the original turns <c>42,7</c> in a W field into 43.</param>
/// <param name="ChoiceOptions">The state names a <see cref="ResourceValueKind.Choice"/> editor offers, in order.
/// Null means the weekday's own seven, which the dialog takes from the format; an ENUM variable supplies its
/// type's states instead, which is what the reference application's <i>Initial værdi</i> combo lists (alignment
/// F-50).</param>
public sealed record VariablePropertiesInput(string Title, string Name, string Note, ResourceInitialValue Current,
    string HelpNote = "", bool SaveOnPowerLoss = false, bool ShowMilliseconds = true, int DecimalPlaces = 2,
    IReadOnlyList<string>? ChoiceOptions = null,
    VariableDialogField? Focus = null);

/// <summary>
/// Which of the element Properties dialog's fields a route wants the caret on (T044) — the dialog a locality,
/// a function block and a <c>Betingelser</c> group all share.
/// <para>Three keys, because the dialog has three editable fields. The library-provenance group below them is
/// <b>read-only</b> and deliberately has no key: a route that focused a greyed box would put the caret
/// somewhere nothing can be typed, so a <c>master_*</c> finding degrades to dialog-level instead.</para>
/// </summary>
public enum ElementDialogField
{
    /// <summary>The element's name.</summary>
    Name,

    /// <summary>The documentation note.</summary>
    Note,

    /// <summary>A <c>Betingelser</c> group's AND/OR operator — absent on every other element.</summary>
    Logic,
}

/// <summary>
/// Which of the variable editor's fields a route wants the caret on (T043).
/// <para>A DIALOG-LOCAL vocabulary, exactly as <see cref="PinDialogField"/> is and for the same reason: the
/// SDK's attribute names stop at the coordinator, which translates one into one of these, and the window maps
/// a key to whichever of its controls is showing.</para>
/// </summary>
public enum VariableDialogField
{
    /// <summary>The variable's name.</summary>
    Name,

    /// <summary>The function-documentation note.</summary>
    Note,

    /// <summary>The second documentation field, the installer-facing help text (<c>note-2</c>).</summary>
    HelpNote,

    /// <summary>
    /// The typed initial value.
    /// <para>ONE key for every type. Which control it lands on depends on the variable's kind — a checkbox, a
    /// number box, an enum combo, the first box of a time or date group — and the window is the only place that
    /// knows which of its panels is visible, so a caller naming a control instead of the VALUE would be
    /// guessing at the dialog's layout.</para>
    /// </summary>
    InitialValue,

    /// <summary>The save-on-power-loss flag.</summary>
    Backup,
}

/// <summary>The edited values returned from the ordinary-variable Properties dialog (US-027, T016): the new name,
/// both documentation fields, the typed initial value, and whether the value survives a power loss.
/// <paramref name="HelpNote"/> is the second field (W5); <paramref name="SaveOnPowerLoss"/> is F-27's.</summary>
/// <param name="EditEnumType">The installer pressed <i>Rediger</i> beside an enum's state list: commit this
/// dialog and then open the enumerator TYPE editor, which the reference application reaches the same way — a
/// second dialog behind a button, never the gesture on the variable row itself (alignment F-50).</param>
public sealed record VariablePropertiesResult(string Name, string Note, ResourceInitialValue Value,
    string HelpNote = "", bool SaveOnPowerLoss = false, bool EditEnumType = false);

/// <summary>The current values shown by the product-properties dialog (US-011). When <c>IsWireless</c> is true the
/// dialog omits the cable type/numbering fields (wireless products have no cabling, US-014).
/// <para>NEITHER properties dialog re-parents, so there is no locality list and no current-locality carrier here.
/// Moving a device between localities is a tree operation (US-054/A-13), and <c>Placering</c> — on the product
/// dialog and, since T014, on the modem dialog too — is the free-text POSITION descriptor the original shows.</para>
/// </summary>
// ProductPropertiesInput is gone (T030). Every family's dialog is composed now, and a hand-built input record
// stating which fields a family gets — IsWireless to hide the cabling, NameLocked to grey the name — was a second
// answer to a question the composer already answers from the measured oracle.

/// <summary>
/// One row of the sensors' <i>Indstillinger</i> grid (T070): a configurable setting's name, the note
/// explaining it, and its current value.
/// <para>Element data rather than dialog metadata, exactly like <see cref="ProductTerminal"/> — the
/// descriptor says WHETHER the grid appears; these are what goes in it.</para>
/// </summary>
/// <param name="Id">
/// The setting ELEMENT the row stands for — what makes the row a thing that can be selected, and later
/// edited, rather than three strings.
/// <para>Typed, where <see cref="ProductTerminal.PinId"/> is a token: a terminal's id travels back out
/// through <see cref="ProductDialogShowOptions.SelectTerminalPin"/>, which is a string because a route
/// composes it, while a setting's id is only ever read in C# — by the grid, and by the command that writes
/// its value. Parsing a token we ourselves had just printed would buy nothing.</para>
/// </param>
public sealed record ProductSetting(string Name, string Note, string Value, ElementId Id);

/// <summary>
/// What the <i>Rediger konstant</i> editor opens on: the setting being edited, and the value it currently holds.
///
/// <para>The value travels as TEXT, VERBATIM as the grid shows it, because that is what the installer sees and
/// overtypes. Turning it back into a stored number is the writing command's business — a window that parsed it
/// would be a second reading of a format the SDK already owns.</para>
/// </summary>
/// <param name="Setting">The setting element the accepted value is written to.</param>
/// <param name="Name">The setting's name, for the window's accessible naming — the vendor shows no visible label.</param>
/// <param name="Value">The current value, pre-filled and selected so typing replaces it.</param>
public sealed record ConstantEditorInput(ElementId Setting, string Name, string Value);

/// <summary>One input/output terminal row shown in the product-properties dialog's terminal grids (US-012). The
/// <c>Address</c> is the vendor-formatted <c>Datalinie N.PP</c> (blank when unassigned); <c>PinId</c> is the
/// terminal element's id token, used to open the terminal-addressing sub-dialog for that row.</summary>
public sealed record ProductTerminal(
    string Name, string Address, string CableColour, string Note, bool IsOutput, string PinId)
{
    /// <summary>The row read as ONE sentence, for the accessible name of its list item — the same answer
    /// <see cref="SceneContainerRow.Summary"/> gives, and for the same reason: the four columns are loose
    /// <c>TextBlock</c>s under a header grid, and Avalonia's Windows bridge exposes no table pattern, so a client
    /// otherwise meets four unassociated runs with no way to tell which header any belongs to. The captions are
    /// spelled into the value for that reason.
    /// <para>Without it the item falls back to the record's <c>ToString()</c>, which a screen reader reads out in
    /// full — brace syntax, the <c>IsOutput</c> flag and the internal <c>PinId</c> included (alignment F-35).
    /// An empty column is named and left blank rather than skipped, so the sentence keeps its shape.</para></summary>
    public string Summary =>
        $"Navn {Name}, Adresse {Address}, Ledningsfarve {CableColour}, Note {Note}";
}

// ProductPropertiesResult and PinPropertiesResult moved to the SDK (Ihc.Vis.Session, fablerefac W2-6) — they are
// edit payloads for the product/pin commands, not presentation. Referenced here via `using Ihc.Vis.Session;`.

/// <summary>One row of the scene-container dialog's table: the scene membership seen from the product's side. The
/// first three columns are the opposite end of the membership's link (the function block's scene pin, the block, and
/// the block's locality); the last two are the member's own stored value.</summary>
public sealed record SceneContainerRow(
    string SceneName, string FunctionBlock, string Locality, string Value, string RampTime)
{
    /// <summary>The row read as ONE sentence, for the accessible name of its list item. The five columns are laid
    /// out as loose <c>TextBlock</c>s under a header grid, and Avalonia's Windows bridge exposes no Grid/Table
    /// pattern — so without this a client meets five unassociated text runs and no way to tell which header any of
    /// them belongs to (UX review USE-01). The captions are spelled into the value for the same reason.</summary>
    public string Summary =>
        $"Scenarie navn {SceneName}, Funktionsblok {FunctionBlock}, Lokalitet {Locality}, "
        + $"Scenarie værdi {Value}, Ramptid {RampTime}";
}

/// <summary>The scene container (<c>Scenarier</c>) dialog's contents (US-024): the container's read-only name, its
/// editable note, and one row per scene membership. Returned as <see cref="SceneContainerResult"/>, or null when
/// dismissed.</summary>
public sealed record SceneContainerInput(string Name, string Note, IReadOnlyList<SceneContainerRow> Rows);

/// <summary>The edited scene-container note (US-024) — the only editable field in the dialog.</summary>
public sealed record SceneContainerResult(string Note);

// The advanced-dimmer input, its SDK result payload and the window they served are gone (T057). The vendor was
// measured to expand its Avanceret disclosure IN PLACE rather than opening a window, so the six settings became
// ordinary fields of the composed product dialog and the modal that held them had nothing left to do.

// ContactInfo and ProjectInfoData moved to the SDK (Ihc.Vis, fablerefac W1-5) — they are project read/edit
// models, not presentation DTOs. Referenced here via `using Ihc.Vis;`.

/// <summary>The current values shown by the enumerator dialog (US-030): the enum type's name and its ordered state
/// names. <c>IsNew</c> distinguishes creating a new type (name editable) from editing an existing one (append states).</summary>
public sealed record EnumDefinitionInput(string Title, string TypeName, IReadOnlyList<string> States, bool IsNew);

/// <summary>The edited enumerator returned from the dialog (US-030): the type name and the full ordered state list.</summary>
public sealed record EnumDefinitionResult(string TypeName, IReadOnlyList<string> States);

/// <summary>
/// What the enumerator-type manager works over (US-030, uxparity2 W10). Modelled on the reference application's
/// <i>Bibliotek ▸ Rediger Enumerator typer</i>, measured 2026-08-04: <b>two</b> panes — types on the left, the
/// selected type's values on the right — each with <c>Ny</c> / <c>Slet</c> / <c>Omdøb</c>, and one <c>OK</c> that
/// just closes. There is no Cancel, because each button has ALREADY applied: the vendor edits live, and so do we.
/// </summary>
/// <param name="Types">Re-reads the project's types; called again after every applied operation, so the panes show
/// what the document actually holds rather than a copy that could drift from it.</param>
/// <param name="Apply">Applies one operation. Returns null on success, or the refusal sentence to show — an
/// engine-level "[read only]" / "still used" refusal has to reach the installer, not vanish.</param>
/// <param name="Blank">The blank-name decision, for the name prompts this manager raises itself. It builds its own
/// <see cref="NamePromptInput"/> — it is a View, so it can neither reach the SDK facade nor mint a validator — and
/// without this member those four prompts would be the one unvalidated door in the application.</param>
public sealed record EnumTypeManagerInput(
    string Title,
    Func<IReadOnlyList<EnumTypeView>> Types,
    Func<EnumTypeManagerOperation, Task<string?>> Apply,
    Func<string?, Problem?> Blank);

/// <summary>One thing the enumerator-type manager can do — the six buttons of the vendor's two panes, as data. The
/// dialog decides WHICH; the view-model owns what each one means, so naming and refusal rules stay in one place.</summary>
public abstract record EnumTypeManagerOperation
{
    private EnumTypeManagerOperation() { }

    /// <summary>Types pane, <c>Ny</c>: create an EMPTY type. The vendor's prompt is name-only — values are added
    /// afterwards in the right-hand pane, one at a time.</summary>
    public sealed record NewType(string Name) : EnumTypeManagerOperation;

    /// <summary>Types pane, <c>Omdøb</c>.</summary>
    public sealed record RenameType(string TypeName, string NewName) : EnumTypeManagerOperation;

    /// <summary>Types pane, <c>Slet</c>.</summary>
    public sealed record DeleteType(string TypeName) : EnumTypeManagerOperation;

    /// <summary>Values pane, <c>Ny</c>: append one value to the selected type.</summary>
    public sealed record NewValue(string TypeName, string Name) : EnumTypeManagerOperation;

    /// <summary>Values pane, <c>Omdøb</c>, addressing the value by its 0-based position in the list.</summary>
    public sealed record RenameValue(string TypeName, int ValueIndex, string NewName) : EnumTypeManagerOperation;

    /// <summary>Values pane, <c>Slet</c>, addressing the value by its 0-based position in the list.</summary>
    public sealed record DeleteValue(string TypeName, int ValueIndex) : EnumTypeManagerOperation;
}

/// <summary>What the one-field name prompt shows: its window title and the text the box starts with (selected, so
/// typing replaces it). The reference application raises exactly this for all four of its Ny/Omdøb buttons —
/// "Opret ny enumerator type", "Opret ny enumerator værdi", "Omdøb Enumerator type", "Omdøb Enumerator værdi".
/// <para><see cref="Blank"/> is the decision the window ASKS rather than makes: the caller supplies it from
/// <c>ProjectAppService.MissingRequiredField</c>, and OK calls it. A View may not name the facade, so this is how
/// the decision reaches a window that must present its answer — the same shape
/// <see cref="EnumTypeManagerInput.Types"/> and <see cref="EnumTypeManagerInput.Apply"/> already use, and the same
/// shape <see cref="SceneValueInput.Level"/> uses to thread an SDK constraint into a window. The window keeps the
/// whole interaction half: the inline line, the focus return, the dialog staying open.</para></summary>
/// <param name="Title">The window title.</param>
/// <param name="InitialName">The text the box starts with, selected so typing replaces it.</param>
/// <param name="Blank">Answers whether the submitted value counts as missing, and with which sentence.</param>
public sealed record NamePromptInput(string Title, string InitialName, Func<string?, Problem?> Blank);

/// <summary>The current values shown by the scene-value dialog (US-024/US-058). A dimmer scene asks a light level
/// (%) + ramp time; a relay/socket scene an ON/OFF state.
/// <para><see cref="Level"/> and <see cref="RampPart"/> are the SDK's own declared constraints for a scene value
/// (T045) — the level bounds the <c>SceneValue.Dimmer</c> factory enforces, and the mm:ss notation's per-part
/// bound. The window binds them rather than carrying 0–100 and 0–59 of its own.</para></summary>
public sealed record SceneValueInput(
    string Title, bool IsDimmer, bool On, int LevelPercent, int RampMinutes, int RampSeconds,
    FieldConstraintMetadata Level = default, FieldConstraintMetadata RampPart = default,
    SceneDialogField? Focus = null);

/// <summary>
/// Which field of the SCENE dialogs a route wants the caret on (T045).
/// <para>One vocabulary over BOTH dialogs — the container's and the member's — because a route names a field,
/// not a window, and which of the two opens follows from the element the finding is about. Each window maps the
/// keys it has and answers null for the rest, exactly as it answers null for a field its own variant does not
/// show.</para>
/// </summary>
public enum SceneDialogField
{
    /// <summary>The scenes container's documentation note — the one thing that dialog lets an installer edit.</summary>
    Note,

    /// <summary>A relay member's ON/OFF state.</summary>
    State,

    /// <summary>A dimmer member's light level.</summary>
    Level,

    /// <summary>A dimmer member's ramp time — the caret lands on the minutes box.</summary>
    RampTime,
}

// SceneValueResult moved to the SDK (Ihc.Vis.Session, W2-7) — an edit payload for the scene commands.

/// <summary>The current values shown by the terminal-addressing dialog (US-012). <c>InUseTerminals</c> are the
/// addresses already taken by other pins in the same direction — carried as <see cref="DatalineAddress"/> rather
/// than as a formatted key, so the producer and the dialog's "(i brug)" marking cannot disagree about the
/// spelling of one.</summary>
public sealed record PinPropertiesInput(
    string Title, bool IsOutput, int DataLine, int Terminal, string CableColour, string Note,
    bool InitialValueOn, IReadOnlyList<DatalineAddress> InUseTerminals, string Name = "",
    bool SaveOnPowerFailure = false,
    PinDialogField? Focus = null);

/// <summary>
/// Which of the terminal editor's fields a route wants the caret on.
/// <para>A DIALOG-LOCAL vocabulary, deliberately: the SDK's attribute names stop at the coordinator, which
/// translates one into one of these, and the window maps a key to its own control. Neither side has to know
/// the other's names, and no control identity travels through an SDK contract.</para>
/// </summary>
public enum PinDialogField
{
    /// <summary>The data-line/terminal address pair — the caret lands on the line list.</summary>
    Address,

    /// <summary>The cable colour.</summary>
    CableColour,

    /// <summary>The documentation note.</summary>
    Note,

    /// <summary>An output's power-up initial value.</summary>
    InitialValue,

    /// <summary>An output's save-on-power-failure flag.</summary>
    Backup,
}


/// <summary>
/// What the ONE generic product dialog returns: the fields the installer actually changed, already resolved to
/// the elements they write.
/// <para>An EMPTY list is a valid, ordinary result — OK pressed without touching anything. Only <c>null</c> is a
/// cancellation. The insert path depends on the distinction: treating an untouched OK as a cancel would delete a
/// product the installer had just placed and accepted.</para>
/// <para>There is no matching <c>Input</c> record. The dialog's input IS the composed
/// <see cref="ProductDialogDescriptor"/>, which already carries the title, the groups, and every field's caption,
/// current value, rule and write target — a hand-built input record here would be a second, drifting statement of
/// the same thing, which is exactly what the metadata engine exists to remove.</para>
/// </summary>
/// <param name="Edits">The changed fields, already resolved to the elements they write.</param>
public sealed record ProductDialogEdits(ImmutableArray<ProductDialogEdit> Edits);

// The result used to carry a WidgetAction as well: the dialog closed on a composite gesture and handed the
// caller what to open next. That was the close-then-reopen protocol, and it is gone (T058) — the window stays
// open and the composite is handled through ProductDialogStep, so there is no moment at which an action has to
// travel out as a result.

/// <summary>
/// Stepping INTO a composite while the product dialog stays open.
///
/// <para>The dialog used to close itself to let a sub-dialog be opened, and the caller re-opened it afterwards.
/// That is not what the installer sees in the vendor: the parent stays on screen, and the sub-dialog appears on
/// top of it. Closing also destroyed the window, so anything the parent held that had not reached the document —
/// a typed value, a selected row, the scroll position — was gone by the time it came back.</para>
///
/// <para>The handler is awaited, and the dialog is still open when it returns, so the installer lands back where
/// they were.</para>
/// </summary>
/// <param name="action">The composite the installer activated.</param>
/// <returns>
/// What the dialog should now SHOW, or null to leave it as it is. The dialog re-projects from this rather than
/// from its own rendered strings — see <see cref="ProductDialogRefresh"/>.
/// </returns>
public delegate Task<ProductDialogRefresh?> ProductDialogStep(ProductDialogWidgetAction action);

/// <summary>
/// What the product dialog shows after a step — recomputed by the caller from the AUTHORITATIVE state.
///
/// <para>Authoritative means the visit's pending values over the document, not either alone. The document is
/// wrong mid-visit, because a terminal addressed inside the visit has not reached it yet; the dialog's own
/// rendered rows are wrong because they are a rendering, and re-deriving values from them would make the display
/// its own source of truth — the point at which a formatting change becomes a data change.</para>
/// </summary>
/// <param name="Terminals">The addressing rows as they now stand.</param>
/// <param name="Settings">The settings rows as they now stand.</param>
public sealed record ProductDialogRefresh(
    IReadOnlyList<ProductTerminal> Terminals,
    IReadOnlyList<ProductSetting> Settings);

/// <summary>
/// Where a route wants the product dialog to OPEN — as distinct from what it contains, which the descriptor
/// already decided.
///
/// <para>Every member is optional, and all of them absent is the ordinary open. They are carried together
/// because they describe one arrival: land on this field, with this terminal row picked, having already stepped
/// into this sub-item.</para>
/// </summary>
/// <param name="FocusAutomationId">
/// The <c>dlg.*</c> id of the field to focus and scroll into view. An id the dialog does not contain focuses
/// nothing — a route that promised a field the descriptor did not compose is a route that was wrong, and
/// focusing something else would hide that.
/// </param>
/// <param name="SelectTerminalPin">The terminal row to pre-select, as its pin's id token.</param>
/// <param name="SelectSettingId">
/// The <i>Indstillinger</i> row to pre-select, as its setting element's id token (T047).
/// <para>Its own slot rather than a shared "sub-item" one: the two grids are different lists with different
/// selections, and one field would have made a route that meant a terminal indistinguishable from one that
/// meant a constant.</para>
/// </param>
/// <param name="InitialAction">A composite the dialog should step into as it opens, as if the installer had.</param>
public sealed record ProductDialogShowOptions(
    string? FocusAutomationId = null,
    string? SelectTerminalPin = null,
    ProductDialogWidgetAction? InitialAction = null,
    string? SelectSettingId = null)
{
    /// <summary>The ordinary open: no field, no row, no step.</summary>
    public static ProductDialogShowOptions None { get; } = new();
}

/// <summary>
/// Abstraction over the modal dialogs the shell needs (confirm-save, file pickers, message boxes, the
/// About and settings windows). Kept free of Avalonia types so view-models and <see cref="ProjectWorkflow"/>
/// stay headlessly testable; the Avalonia implementation lives in the view layer.
/// </summary>
public interface IDialogService
{
    Task<SaveChangesResult> ConfirmSaveChangesAsync(string documentName);

    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>
    /// Shows an INFORMATIONAL text — help content, a diagnostic readout. Not for an outcome: something that failed,
    /// was refused or could not be carried through is a coded problem and goes through
    /// <see cref="ShowProblemAsync(string, Problem)"/>, so that identity reaches the installer (R18). The per-site
    /// register pins which sites may use this one and why.
    /// </summary>
    Task ShowMessageAsync(string title, string message);

    /// <summary>
    /// Shows one coded problem: its Danish message with its identity as a bracketed suffix, rendered by the shell's
    /// single presentation path (T040). The title is the shell's own framing of the box.
    /// </summary>
    Task ShowProblemAsync(string title, Problem problem);

    /// <summary>
    /// Shows a cause/detail CHAIN — the shape a site has when it frames an SDK failure of its own (T006). Exactly
    /// one sentence reaches the installer, the cause's; the operation's code stays available for the log.
    /// </summary>
    Task ShowProblemAsync(string title, ProblemChain chain);

    /// <summary>
    /// Shows a set of INDEPENDENT problems — a head naming the failure as a whole, then every item, each as its
    /// own complete entry. The shape a refused save or upload has when validation stopped it: the head says the
    /// operation will not proceed and how many errors block it, and the items are those errors.
    /// <para>
    /// It is a separate overload rather than a flag because the two composition rules are the inverse of each
    /// other. Rendering an aggregate by the chain's rule would show one finding and silently discard the rest;
    /// rendering a chain by this one would show a single failure twice. The type decides, so no site can choose
    /// wrongly.
    /// </para>
    /// </summary>
    Task ShowProblemAsync(string title, ProblemAggregate aggregate);

    /// <summary>Opens a project file picker; returns the chosen path, or null if cancelled.</summary>
    Task<string?> PickOpenProjectAsync(string? initialDirectory);

    /// <summary>Opens a save-as picker; returns the chosen path, or null if cancelled.</summary>
    Task<string?> PickSaveProjectAsync(string? initialDirectory, string suggestedFileName);

    /// <summary>Opens a picker for a single catalog definition file (<c>.def</c>/<c>.ifb</c>) to import (US-059);
    /// returns the chosen path, or null if cancelled.</summary>
    Task<string?> PickCatalogFileAsync();

    /// <summary>Opens a folder picker for a catalog folder to import (US-060); returns the chosen path, or null.</summary>
    Task<string?> PickCatalogFolderAsync();

    Task ShowAboutAsync();

    Task ShowSettingsAsync(string settingsText);

    /// <summary>Opens a URL (or a local document) in the OS default handler. Returns whether the handler was
    /// actually launched — false means nothing opened, which the caller must report rather than treat as done.
    /// Never fatal; the underlying failure is also recorded to diagnostics.</summary>
    Task<bool> OpenExternalUrlAsync(string url);

    /// <summary>Opens the modal element Properties dialog (title, pre-filled name + note); returns the edited
    /// values, or null when the installer cancels. <paramref name="origin"/> adds the read-only library-provenance
    /// group a library function block shows below its editable fields (S-19); null for everything else.</summary>
    /// <param name="affirmative">Labels the commit button: a dialog that goes on to WRITE A FILE names the
    /// verb (<c>Save</c>) rather than saying OK (S-22).</param>
    /// <param name="userGroupCaption">Names the editable Name/Note group — the vendor's function-block dialog
    /// captions it <c>Bruger egenskaber</c> (F-24). Null leaves the fields uncaptioned, which is what its other
    /// properties dialogs do.</param>
    /// <param name="conditionsOr">Supplied only for a <c>Betingelser</c> group: its current logic operator, which
    /// the dialog then offers as the reference application's captioned <i>Logisk betingelse</i> AND/OR field.
    /// Null everywhere else, and the field is absent (alignment F-48).</param>
    /// <param name="focus">Where a route wants the caret; null is the ordinary open, on the name.</param>
    Task<PropertiesResult?> EditPropertiesAsync(string title, string name, string note, LibraryOrigin? origin = null,
        string affirmative = "OK", string? userGroupCaption = null, bool? conditionsOr = null,
        ElementDialogField? focus = null);

    /// <summary>Shows the ordinary-variable Properties dialog (US-027, T016): edits Name, Note, and the typed initial
    /// value (the control shown depends on the value's <see cref="ResourceValueKind"/>). Returns null on Cancel.</summary>
    Task<VariablePropertiesResult?> EditVariablePropertiesAsync(VariablePropertiesInput input);


    /// <summary>Shows a product's scene container (<c>Scenarier</c>) — its name (read-only), note, and the table of
    /// its scene memberships (US-024). Resolves to the edited note, or null when dismissed.</summary>
    Task<SceneContainerResult?> EditSceneContainerAsync(SceneContainerInput input);

    /// <summary>Opens the modal terminal-addressing dialog for a product input/output pin (US-012); returns the
    /// edited addressing, or null when the installer cancels.</summary>
    /// <summary>Opens the terminal-addressing dialog. <paramref name="onApply"/> is invoked by the dialog's
    /// <i>Apply</i> button, which commits the current values and leaves the dialog open (the vendor's <i>Anvend</i>);
    /// the returned result is the OK commit, or null when cancelled.</summary>
    Task<PinPropertiesResult?> EditPinPropertiesAsync(PinPropertiesInput input, Func<PinPropertiesResult, Task>? onApply = null);

    /// <summary>Opens the ONE generic product dialog for a composed descriptor; returns the edits the installer
    /// made (possibly none) plus any composite they stepped into, or null when they cancel.</summary>
    /// <param name="terminals">Rows for the terminal grids, when the descriptor declares that widget. Element
    /// data, not dialog metadata — which is why it travels beside the descriptor rather than inside it.</param>
    /// <param name="settings">Rows for the sensors' Indstillinger grid, when the descriptor declares that
    /// widget. Element data too, and for the same reason (T070).</param>
    Task<ProductDialogEdits?> EditProductDialogAsync(
        ProductDialogDescriptor descriptor, IReadOnlyList<ProductTerminal>? terminals = null,
        IReadOnlyList<ProductSetting>? settings = null,
        ProductDialogShowOptions? options = null,
        ProductDialogStep? onStep = null);

    /// <summary>Opens the <i>Rediger konstant</i> editor for one row of the Indstillinger grid (T040); returns the
    /// value the installer accepted, or null when they dismissed the window without accepting.</summary>
    Task<string?> EditConstantAsync(ConstantEditorInput input);

    /// <summary>Opens the modal scene-value dialog (US-024/US-058); returns the edited value, or null when the
    /// installer cancels.</summary>
    Task<SceneValueResult?> EditSceneValueAsync(SceneValueInput input);

    /// <summary>Opens the modal enumerator dialog (US-030) to create or edit an enum type and its ordered states;
    /// returns the edited type, or null when the installer cancels.</summary>
    Task<EnumDefinitionResult?> EditEnumDefinitionAsync(EnumDefinitionInput input);

    /// <summary>Opens the two-pane enumerator types-and-values editor (US-030, W10) and returns when it is closed.
    /// It has no result: every button applied as it was pressed, exactly as the reference application does.</summary>
    Task ManageEnumTypesAsync(EnumTypeManagerInput input);

    /// <summary>Asks for ONE name in a modal prompt (the vendor's "Opret ny …" / "Omdøb …"); returns the trimmed
    /// name, or null when the installer cancels.</summary>
    Task<string?> PromptForNameAsync(NamePromptInput input);

    /// <summary>Opens the modal project-information dialog (US-039) prefilled with <paramref name="current"/>;
    /// returns the edited project/customer/installer info, or null when the installer cancels.</summary>
    Task<ProjectInfoData?> EditProjectInfoAsync(ProjectInfoData current, ProjectInfoSuggestions suggestions);

    /// <summary>Shows the shared report picker (R12/D4) — report type pre-selected per the invoking menu
    /// entry, mode choice, and the view/save actions — bound through the <see cref="IReportPickerViewModel"/> seam.</summary>
    Task ShowReportPickerAsync(IReportPickerViewModel viewModel);

    /// <summary>Opens the save dialog for a generated report in the <paramref name="format"/> the picker chose
    /// (the suggested name carries that format's extension too, but it is a display string, not the format's
    /// source). Returns the chosen path, or null when the installer cancels.</summary>
    Task<string?> PickSaveReportAsync(string suggestedFileName, ReportFormat format);

    /// <summary>Opens the save dialog for the Problemer panel's findings list (US-085). Returns the chosen path,
    /// or null when the installer cancels.</summary>
    /// <remarks>
    /// A door of its own rather than a third value on <see cref="PickSaveReportAsync"/>'s format, and the reason
    /// is the dialog's own strings: a findings list is not a report, so it is offered under its own title and its
    /// own filter label. It also takes no format argument at all — <c>ExportFindings</c> writes exactly one — so
    /// there is no way to ask for a findings file in a report's format, or the reverse.
    /// </remarks>
    Task<string?> PickSaveFindingsAsync(string suggestedFileName);

    /// <summary>Opens the read-only data-line module map dialog (US-050).</summary>
    Task ShowModuleMapAsync(DatalineModuleMap map);
}