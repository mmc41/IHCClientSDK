using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Session;

namespace ihc_openvisual.Services;

/// <summary>The seam the Data tables dialog is shown through (T020): a marker for the view-model the dialog binds
/// to (implemented app-side by the Data tables VM), so <see cref="IDialogService"/> — a Services-layer abstraction —
/// is not coupled to a concrete ViewModels type. The dialog service passes the instance to the window as its
/// DataContext; the window's compiled bindings resolve against the concrete VM at runtime.</summary>
public interface IDataTablesDialogViewModel;

/// <summary>The seam the Reports view is shown through (T021): a marker for the view-model the Reports window binds
/// to (the app-side <c>ReportsViewModel</c>), so <see cref="IDialogService"/> stays uncoupled from a concrete
/// ViewModels type. The dialog service passes the instance to the window as its DataContext.</summary>
public interface IReportsDialogViewModel;

/// <summary>The installer's answer to a "save changes before closing?" prompt.</summary>
public enum SaveChangesResult
{
    Save,
    Discard,
    Cancel
}

/// <summary>The edited values returned from the element Properties dialog (US-007): the new name and note.</summary>
public sealed record PropertiesResult(string Name, string Note);

/// <summary>
/// The read-only provenance of a function block that came from the LIBRARY (uxparity S-19): which library block it
/// was stamped from, its number and version, when it was made and by whom. Shown as a second, non-editable group
/// under the editable Name/Note — a block authored from scratch has none of this and the group is absent.
/// </summary>
public sealed record LibraryOrigin(string Name, string Number, string Version, string Created, string Developer);

/// <summary>The current values shown by the ordinary-variable Properties dialog (US-027, T016): name, note, and the
/// typed initial value whose <see cref="ResourceInitialValue.Kind"/> selects the value control (a Bool checkbox, a
/// Number box, a Time h/m/s(/ms) group, or nothing for <see cref="ResourceValueKind.None"/>).</summary>
public sealed record VariablePropertiesInput(string Title, string Name, string Note, ResourceInitialValue Current);

/// <summary>The edited values returned from the ordinary-variable Properties dialog (US-027, T016): the new name,
/// note, and typed initial value.</summary>
public sealed record VariablePropertiesResult(string Name, string Note, ResourceInitialValue Value);

/// <summary>A locality option for the product-properties <i>Location</i> drop-down (US-011).</summary>
public sealed record LocalityChoice(string Id, string Name);

/// <summary>The current values + locality choices shown by the product-properties dialog (US-011). When
/// <c>IsWireless</c> is true the dialog omits the cable type/numbering fields (wireless products have no cabling,
/// US-014).</summary>
public sealed record ProductPropertiesInput(
    string Title, string Name, string Note, string CableType, string CableNumber,
    string IdentificationCode, string LightGroup,
    IReadOnlyList<LocalityChoice> Localities, string CurrentLocalityId,
    bool IsWireless = false, bool IsWirelessDimmer = false,
    IReadOnlyList<ProductTerminal>? Terminals = null, string Position = "", bool NameLocked = false,
    bool EndUserReport = false);

/// <summary>One input/output terminal row shown in the product-properties dialog's terminal grids (US-012). The
/// <c>Address</c> is the vendor-formatted <c>Datalinie N.PP</c> (blank when unassigned); <c>PinId</c> is the
/// terminal element's id token, used to open the terminal-addressing sub-dialog for that row.</summary>
public sealed record ProductTerminal(
    string Name, string Address, string CableColour, string Note, bool IsOutput, string PinId);

// ProductPropertiesResult and PinPropertiesResult moved to the SDK (Ihc.Vis.Session, fablerefac W2-6) — they are
// edit payloads for the product/pin commands, not presentation. Referenced here via `using Ihc.Vis.Session;`.

/// <summary>One row of the scene-container dialog's table: the scene membership seen from the product's side. The
/// first three columns are the opposite end of the membership's link (the function block's scene pin, the block, and
/// the block's locality); the last two are the member's own stored value.</summary>
public sealed record SceneContainerRow(
    string SceneName, string FunctionBlock, string Locality, string Value, string RampTime);

/// <summary>The scene container (<c>Scenarier</c>) dialog's contents (US-024): the container's read-only name, its
/// editable note, and one row per scene membership. Returned as <see cref="SceneContainerResult"/>, or null when
/// dismissed.</summary>
public sealed record SceneContainerInput(string Name, string Note, IReadOnlyList<SceneContainerRow> Rows);

/// <summary>The edited scene-container note (US-024) — the only editable field in the dialog.</summary>
public sealed record SceneContainerResult(string Note);

/// <summary>The current values shown by the advanced wireless-dimmer dialog (US-015). Times in ms/s, levels in %,
/// <c>LoadMode</c> is the stored token (<c>auto</c>/<c>rc</c>/<c>rl</c>).</summary>
public sealed record AdvancedDimmerInput(
    int SoftOnMs, int SoftOffMs, int ManualRampS, int MinimumPercent, int MaximumPercent, string LoadMode);

// AdvancedDimmerResult moved to the SDK (Ihc.Vis.Session, W2-10) — an edit payload for the dimmer command.

// ContactInfo and ProjectInfoData moved to the SDK (Ihc.Vis, fablerefac W1-5) — they are project read/edit
// models, not presentation DTOs. Referenced here via `using Ihc.Vis;`.

/// <summary>The current values shown by the enumerator dialog (US-030): the enum type's name and its ordered state
/// names. <c>IsNew</c> distinguishes creating a new type (name editable) from editing an existing one (append states).</summary>
public sealed record EnumDefinitionInput(string Title, string TypeName, IReadOnlyList<string> States, bool IsNew);

/// <summary>The edited enumerator returned from the dialog (US-030): the type name and the full ordered state list.</summary>
public sealed record EnumDefinitionResult(string TypeName, IReadOnlyList<string> States);

/// <summary>The current values shown by the scene-value dialog (US-024/US-058). A dimmer scene asks a light level
/// (%) + ramp time; a relay/socket scene an ON/OFF state.</summary>
public sealed record SceneValueInput(
    string Title, bool IsDimmer, bool On, int LevelPercent, int RampMinutes, int RampSeconds);

// SceneValueResult moved to the SDK (Ihc.Vis.Session, W2-7) — an edit payload for the scene commands.

/// <summary>The current values shown by the terminal-addressing dialog (US-012). <c>InUseTerminals</c> are the
/// already-used <c>line.terminal</c> addresses in the same direction.</summary>
public sealed record PinPropertiesInput(
    string Title, bool IsOutput, int DataLine, int Terminal, string CableColour, string Note,
    bool InitialValueOn, IReadOnlyList<string> InUseTerminals, string Name = "",
    bool SaveOnPowerFailure = false);


/// <summary>The current values shown by the modem properties dialog (US-013). <c>PhoneNumbers</c> holds telephone
/// numbers 1..N (slot order).</summary>
public sealed record ModemPropertiesInput(
    string Title, string Name, string Note, string IdentificationCode,
    string Cable0V, string Cable24V, string CableRS485Minus, string CableRS485Plus,
    string PinCode, IReadOnlyList<string> PhoneNumbers,
    IReadOnlyList<LocalityChoice> Localities, string CurrentLocalityId);

// ModemPropertiesResult moved to the SDK (Ihc.Vis.Session, W2-10) — an edit payload for the modem command.

/// <summary>
/// Abstraction over the modal dialogs the shell needs (confirm-save, file pickers, message boxes, the
/// About and settings windows). Kept free of Avalonia types so view-models and <see cref="ProjectWorkflow"/>
/// stay headlessly testable; the Avalonia implementation lives in the view layer.
/// </summary>
public interface IDialogService
{
    Task<SaveChangesResult> ConfirmSaveChangesAsync(string documentName);

    Task<bool> ConfirmAsync(string title, string message);

    Task ShowMessageAsync(string title, string message);

    /// <summary>Opens a project file picker; returns the chosen path, or null if cancelled.</summary>
    Task<string?> PickOpenProjectAsync(string? initialDirectory);

    /// <summary>Opens a save-as picker; returns the chosen path, or null if cancelled.</summary>
    Task<string?> PickSaveProjectAsync(string? initialDirectory, string suggestedFileName);

    /// <summary>Opens a save-as picker for an <c>.ifb</c> function-block file (US-021); returns the path, or null.</summary>
    Task<string?> PickSaveFunctionBlockAsync(string suggestedFileName);

    /// <summary>Opens a picker for a single catalog definition file (<c>.def</c>/<c>.ifb</c>) to import (US-059);
    /// returns the chosen path, or null if cancelled.</summary>
    Task<string?> PickCatalogFileAsync();

    /// <summary>Opens a folder picker for a catalog folder to import (US-060); returns the chosen path, or null.</summary>
    Task<string?> PickCatalogFolderAsync();

    Task ShowAboutAsync();

    Task ShowSettingsAsync(string settingsText);

    /// <summary>Opens a URL in the OS default browser; failures are recorded to diagnostics, never fatal.</summary>
    Task OpenExternalUrlAsync(string url);

    /// <summary>Opens the modal element Properties dialog (title, pre-filled name + note); returns the edited
    /// values, or null when the installer cancels. <paramref name="origin"/> adds the read-only library-provenance
    /// group a library function block shows below its editable fields (S-19); null for everything else.</summary>
    /// <paramref name="affirmative"/> labels the commit button: a dialog that goes on to WRITE A FILE names the
    /// verb (<c>Save</c>) rather than saying OK (S-22).
    Task<PropertiesResult?> EditPropertiesAsync(string title, string name, string note, LibraryOrigin? origin = null,
        string affirmative = "OK");

    /// <summary>Shows the ordinary-variable Properties dialog (US-027, T016): edits Name, Note, and the typed initial
    /// value (the control shown depends on the value's <see cref="ResourceValueKind"/>). Returns null on Cancel.</summary>
    Task<VariablePropertiesResult?> EditVariablePropertiesAsync(VariablePropertiesInput input);

    /// <summary>Opens the modal product-documentation Properties dialog (US-011); returns the edited documentation,
    /// or null when the installer cancels.</summary>
    Task<ProductPropertiesResult?> EditProductPropertiesAsync(ProductPropertiesInput input);

    /// <summary>Shows a product's scene container (<c>Scenarier</c>) — its name (read-only), note, and the table of
    /// its scene memberships (US-024). Resolves to the edited note, or null when dismissed.</summary>
    Task<SceneContainerResult?> EditSceneContainerAsync(SceneContainerInput input);

    /// <summary>Opens the modal terminal-addressing dialog for a product input/output pin (US-012); returns the
    /// edited addressing, or null when the installer cancels.</summary>
    /// <summary>Opens the terminal-addressing dialog. <paramref name="onApply"/> is invoked by the dialog's
    /// <i>Apply</i> button, which commits the current values and leaves the dialog open (the vendor's <i>Anvend</i>);
    /// the returned result is the OK commit, or null when cancelled.</summary>
    Task<PinPropertiesResult?> EditPinPropertiesAsync(PinPropertiesInput input, Func<PinPropertiesResult, Task>? onApply = null);

    /// <summary>Opens the modal modem properties dialog (US-013); returns the edited documentation, or null when the
    /// installer cancels.</summary>
    Task<ModemPropertiesResult?> EditModemPropertiesAsync(ModemPropertiesInput input);

    /// <summary>Opens the modal advanced wireless-dimmer dialog (US-015); returns the edited settings, or null when
    /// the installer cancels.</summary>
    Task<AdvancedDimmerResult?> EditAdvancedDimmerAsync(AdvancedDimmerInput input);

    /// <summary>Opens the modal scene-value dialog (US-024/US-058); returns the edited value, or null when the
    /// installer cancels.</summary>
    Task<SceneValueResult?> EditSceneValueAsync(SceneValueInput input);

    /// <summary>Opens the modal enumerator dialog (US-030) to create or edit an enum type and its ordered states;
    /// returns the edited type, or null when the installer cancels.</summary>
    Task<EnumDefinitionResult?> EditEnumDefinitionAsync(EnumDefinitionInput input);

    /// <summary>Opens the modal project-information dialog (US-039) prefilled with <paramref name="current"/>;
    /// returns the edited project/customer/installer info, or null when the installer cancels.</summary>
    Task<ProjectInfoData?> EditProjectInfoAsync(ProjectInfoData current);

    /// <summary>Opens the modal Data tables dialog (US-049) bound to the given view-model (T020: the
    /// <see cref="IDataTablesDialogViewModel"/> seam, not the concrete VM).</summary>
    Task ShowDataTablesAsync(IDataTablesDialogViewModel viewModel);

    /// <summary>Shows the Reports view (US-040 / T021) — the single navigable project-documentation document —
    /// bound to the given view-model (through the <see cref="IReportsDialogViewModel"/> seam, not the concrete VM).</summary>
    Task ShowReportsAsync(IReportsDialogViewModel viewModel);

    /// <summary>Opens the read-only Wired module address map dialog (US-050).</summary>
    Task ShowModuleMapAsync(ModuleAddressMap map);
}
