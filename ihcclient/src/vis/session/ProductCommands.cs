#nullable enable
using System;
using Ihc.Vis.Addressing;
using Ihc.Vis.Editing;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    // ---- Edit-payload records (fablerefac W2-6, moved down from the GUI dialogs — they are edit inputs, not
    //      presentation; their dialog *Input* counterparts stay GUI-side). ----

    /// <summary>The edited product documentation (US-011/US-012): the fields the product properties dialog writes.
    /// <c>LocalityId</c> is the chosen Location; a change re-parents the product.</summary>
    public sealed record ProductPropertiesResult(
        string Name, string LocalityId, string Note, string CableType, string CableNumber,
        string IdentificationCode, string LightGroup, bool OpenAdvanced = false,
        string? ConfigureTerminalPinId = null, string Position = "", bool EndUserReport = false);

    /// <summary>The edited pin addressing (US-012): the terminal address, cable colour, note, and (outputs) the
    /// initial on/off value.</summary>
    public sealed record PinPropertiesResult(
        int DataLine, int Terminal, string CableColour, string Note, bool InitialValueOn,
        bool SaveOnPowerFailure = false);

    // ---- Commands ----

    /// <summary>Inserts a catalog product into a locality (US-010), producing the new product's id. The
    /// at-most-one-modem guard and its dialog stay GUI-side (before Apply).</summary>
    public sealed record AddProduct(ElementId LocalityId, ProductDefinition Definition) : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Insert product";
        internal override EditVerdict Evaluate(EditContext context) => context.RequireExists(LocalityId, "locality");
        internal override ElementId ExecuteCore(ProjectEditor editor) =>
            editor.Group(LocalityId).AddProduct(Definition).Id;
    }

    /// <summary>Inserts a preprogrammed library function block into a locality (US-018), producing its id.</summary>
    public sealed record AddFunctionBlock(ElementId LocalityId, FunctionBlockDefinition Definition)
        : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Insert function block";
        internal override EditVerdict Evaluate(EditContext context) => context.RequireExists(LocalityId, "locality");
        internal override ElementId ExecuteCore(ProjectEditor editor) =>
            editor.Group(LocalityId).AddFunctionBlock(Definition).Id;
    }

    /// <summary>Inserts an empty "from scratch" function block into a locality (US-019), producing its id.</summary>
    public sealed record AddEmptyFunctionBlock(
        ElementId LocalityId, FunctionBlockDefinition Template, DateOnly Created, string Name)
        : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Insert function block";
        internal override EditVerdict Evaluate(EditContext context) => context.RequireExists(LocalityId, "locality");
        internal override ElementId ExecuteCore(ProjectEditor editor) =>
            editor.Group(LocalityId).AddEmptyFunctionBlock(Template, Created, Name).Id;
    }

    /// <summary>Unlocks a library function block for editing (US-020): clears its <c>locked</c> flag.</summary>
    public sealed record UnlockFunctionBlock(ElementId Id, string Programmer, DateOnly Unlocked) : ProjectCommand
    {
        internal override string Describe(Project project) => "Unlock function block";
        // A5: RequireTag (not the weaker RequireExists) so a wrong-tag id is a clean Refuse, not the engine throw
        // Execute would raise — matching the sibling SaveFunctionBlockToLibrary, whose Execute is the same FunctionBlock(Id).
        internal override EditVerdict Evaluate(EditContext context) => context.RequireTag(Id, "a function block", "functionblock");
        internal override void Execute(ProjectEditor editor) =>
            editor.FunctionBlock(Id).Unlock(Programmer, Unlocked);
    }

    /// <summary>Transforms an in-project function block into a locked library instance (US-021 Save-to-library, PG-3a):
    /// rename to <paramref name="Name"/>, stamp <c>master_*</c>, apply the library badge and <paramref name="Note"/>,
    /// and set <c>locked="yes"</c> — no re-insertion. Undoable, so one undo restores the prior unlocked block.</summary>
    public sealed record SaveFunctionBlockToLibrary(ElementId Id, string Name, string Programmer, DateOnly Date, string? Note)
        : ProjectCommand
    {
        internal override string Describe(Project project) => "Save function block to library";
        internal override EditVerdict Evaluate(EditContext context) => context.RequireTag(Id, "a function block", "functionblock");
        internal override void Execute(ProjectEditor editor) =>
            editor.FunctionBlock(Id).SaveAsLibraryInstance(Name, Programmer, Date, Note);
    }

    /// <summary>Applies edited pin addressing (US-012): terminal address, cable colour, note, and (outputs) initial
    /// value. Refuses an out-of-range terminal rather than clearing the address.</summary>
    public sealed record UpdatePin(ElementId Id, PinPropertiesResult Result) : ProjectCommand
    {
        internal override string Describe(Project project) => "Address pin";
        internal override EditVerdict Evaluate(EditContext context)
        {
            if (context.Index.FindById(Id) is not { } pin)
            {
                return EditVerdict.Refuse("The pin no longer exists.");
            }
            bool isOutput = pin.Tag == "dataline_output";
            return DatalineAddress.TryEncode(Result.DataLine, Result.Terminal, isOutput, out _)
                ? EditVerdict.Allow
                : EditVerdict.Refuse("The terminal is out of range for this line.");
        }
        internal override void Execute(ProjectEditor editor)
        {
            ElementRef handle = editor.Resolve(Id, "pin");
            bool isOutput = handle.Tag == "dataline_output";
            if (!DatalineAddress.TryEncode(Result.DataLine, Result.Terminal, isOutput, out string addressToken))
            {
                throw new EditRefusedException("The terminal is out of range for this line.");
            }
            handle.SetAttribute("address_dataline", addressToken);
            handle.SetAttribute("cable_colour", Result.CableColour);
            handle.SetAttribute("note", Result.Note);
            if (isOutput)
            {
                handle.SetAttribute("inivalue", Result.InitialValueOn ? "on" : "off");
                // "Save the current value" (the vendor's Ved strømsvigt ▸ Gem aktuel værdi): the output resumes
                // its last state after a power failure instead of its initial value. Outputs only — an input has
                // no state to restore.
                handle.SetAttribute("backup", Result.SaveOnPowerFailure ? "yes" : "no");
            }
        }
    }

    /// <summary>Applies edited product documentation (US-011): name/position/enduser/note/id-code/light-group, the
    /// cabling attributes for wired products only, and a re-parent when the Location changed.
    /// <paramref name="CurrentLocalityId"/> is the product's current parent (resolved by the caller), so the
    /// re-parent is skipped when the Location is unchanged (a same-parent move would re-order it).</summary>
    public sealed record UpdateProduct(ElementId Id, ProductPropertiesResult Result, ElementId? CurrentLocalityId)
        : ProjectCommand
    {
        internal override string Describe(Project project) => "Edit product";
        internal override EditVerdict Evaluate(EditContext context)
        {
            EditVerdict exists = context.RequireExists(Id, "product");
            return exists.Ok ? Relocation.Verdict(context, CurrentLocalityId, Result.LocalityId) : exists;
        }
        internal override void Execute(ProjectEditor editor)
        {
            ElementRef handle = editor.Resolve(Id, "product");
            handle.SetAttribute("name", Result.Name);
            handle.SetAttribute("position", Result.Position);
            handle.SetAttribute("enduser_report", Result.EndUserReport ? "yes" : "no");
            handle.SetAttribute("note", Result.Note);
            handle.SetAttribute("documentation_tag", Result.IdentificationCode);
            handle.SetAttribute("power_group", Result.LightGroup);
            if (!ProductClassifier.IsWireless(handle.Tag))
            {
                handle.SetAttribute("cabletype", Result.CableType);
                handle.SetAttribute("cablenumber", Result.CableNumber);
            }
            Relocation.Apply(editor, Id, CurrentLocalityId, Result.LocalityId);   // Location changed → re-parent
        }
    }

    /// <summary>The shared "change Location" guard for the product/modem property edits (US-011/US-013 re-parent).
    /// A re-parent is requested when the edited Location differs from the element's current parent; the target must
    /// resolve to an existing group (the only valid product/modem container — see <c>ProjectEditor.Group</c>).
    /// Centralizing it keeps <see cref="UpdateProduct"/>/<see cref="UpdateModem"/> Evaluate verdict and Execute
    /// move in lock-step, so a bad target Refuses instead of being silently dropped (unparseable id) or building an
    /// invalid tree that still saves (a non-group target) — review C3.</summary>
    internal static class Relocation
    {
        // A re-parent is requested when the selected Location differs from the element's current parent token; an
        // unchanged edit re-selects the current parent, so its token round-trips to equality here (no move).
        private static bool IsRequested(ElementId? currentParent, string selectedLocalityId) =>
            selectedLocalityId != (currentParent?.ToToken() ?? string.Empty);

        /// <summary>Allow unless a requested re-parent targets something that is not an existing group.</summary>
        public static EditVerdict Verdict(EditContext context, ElementId? currentParent, string selectedLocalityId) =>
            !IsRequested(currentParent, selectedLocalityId) || ResolveGroup(context, selectedLocalityId) is not null
                ? EditVerdict.Allow
                : EditVerdict.Refuse("The chosen Location is not an existing group.");

        /// <summary>Performs the re-parent when one is requested; the target was validated by <see cref="Verdict"/>.</summary>
        public static void Apply(ProjectEditor editor, ElementId elementId, ElementId? currentParent, string selectedLocalityId)
        {
            if (IsRequested(currentParent, selectedLocalityId) && ElementId.TryParse(selectedLocalityId, out ElementId target))
            {
                editor.MoveSubtree(elementId, target);   // ids preserved
            }
        }

        private static ProjectElement? ResolveGroup(EditContext context, string selectedLocalityId) =>
            ElementId.TryParse(selectedLocalityId, out ElementId target)
                && context.Index.FindById(target) is { Tag: "group" } group
                    ? group
                    : null;
    }
}
