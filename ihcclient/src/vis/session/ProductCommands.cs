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

    // ProductPropertiesResult and its UpdateProduct command are gone (T031), superseded by ApplyProductDialog.
    // A fixed record of "the fields the product dialog writes" could only ever describe ONE family's dialog: it
    // named Kabeltype and Lysgruppe, which the LED dimmer does not declare and the modem does not show, so every
    // other family either wrote attributes it lacks or carried blanks through fields it never offered. The
    // composed descriptor states per family what is writable, and ApplyProductDialog carries only what changed.

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
        internal override string Describe(Project project) => "Indsæt produkt";
        internal override EditVerdict Evaluate(EditContext context) => context.RequireExists(LocalityId, "Lokaliteten");
        internal override ElementId ExecuteCore(ProjectEditor editor) =>
            editor.Group(LocalityId).AddProduct(Definition).Id;
    }

    /// <summary>Inserts a preprogrammed library function block into a locality (US-018), producing its id.</summary>
    public sealed record AddFunctionBlock(ElementId LocalityId, FunctionBlockDefinition Definition)
        : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Indsæt funktionsblok";
        internal override EditVerdict Evaluate(EditContext context) => context.RequireExists(LocalityId, "Lokaliteten");
        internal override ElementId ExecuteCore(ProjectEditor editor) =>
            editor.Group(LocalityId).AddFunctionBlock(Definition).Id;
    }

    /// <summary>Inserts an empty "from scratch" function block into a locality (US-019), producing its id.</summary>
    public sealed record AddEmptyFunctionBlock(
        ElementId LocalityId, FunctionBlockDefinition Template, DateOnly Created, string Name)
        : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Indsæt funktionsblok";
        internal override EditVerdict Evaluate(EditContext context) => context.RequireExists(LocalityId, "Lokaliteten");
        internal override ElementId ExecuteCore(ProjectEditor editor) =>
            editor.Group(LocalityId).AddEmptyFunctionBlock(Template, Created, Name).Id;
    }

    /// <summary>Unlocks a library function block for editing (US-020): clears its <c>locked</c> flag.</summary>
    public sealed record UnlockFunctionBlock(ElementId Id, string Programmer, DateOnly Unlocked) : ProjectCommand
    {
        internal override string Describe(Project project) => "Oplås funktionsblok";
        // A5: RequireTag (not the weaker RequireExists) so a wrong-tag id is a clean Refuse, not the engine throw
        // Execute would raise — matching the sibling SaveFunctionBlockToLibrary, whose Execute is the same FunctionBlock(Id).
        internal override EditVerdict Evaluate(EditContext context) => context.RequireTag(Id, "en funktionsblok", "functionblock");
        internal override void Execute(ProjectEditor editor) =>
            editor.FunctionBlock(Id).Unlock(Programmer, Unlocked);
    }

    /// <summary>Transforms an in-project function block into a locked library instance (US-021 Save-to-library, PG-3a):
    /// rename to <paramref name="Name"/>, stamp <c>master_*</c>, apply the library badge and <paramref name="Note"/>,
    /// and set <c>locked="yes"</c> — no re-insertion. Undoable, so one undo restores the prior unlocked block.</summary>
    public sealed record SaveFunctionBlockToLibrary(ElementId Id, string Name, string Programmer, DateOnly Date, string? Note)
        : ProjectCommand
    {
        internal override string Describe(Project project) => "Gem funktionsblok i biblioteket";
        internal override EditVerdict Evaluate(EditContext context) => context.RequireTag(Id, "en funktionsblok", "functionblock");
        internal override void Execute(ProjectEditor editor) =>
            editor.FunctionBlock(Id).SaveAsLibraryInstance(Name, Programmer, Date, Note);
    }

    /// <summary>Applies edited pin addressing (US-012): terminal address, cable colour, note, and (outputs) initial
    /// value. Refuses an out-of-range terminal rather than clearing the address.</summary>
    public sealed record UpdatePin(ElementId Id, PinPropertiesResult Result) : ProjectCommand
    {
        internal override string Describe(Project project) => "Adresser klemme";
        internal override EditVerdict Evaluate(EditContext context)
        {
            if (context.Index.FindById(Id) is not { } pin)
            {
                return EditVerdict.Refuse(EditRefusalCodes.TerminalMissing, "Klemmen findes ikke længere.");
            }
            bool isOutput = pin.Tag == "dataline_output";
            return DatalineAddress.TryEncode(Result.DataLine, Result.Terminal, isOutput, out _)
                ? EditVerdict.Allow
                : EditVerdict.Refuse(EditRefusalCodes.TerminalAddressRange, "Klemmenummeret ligger uden for datalinjens område.");
        }
        internal override void Execute(ProjectEditor editor)
        {
            ElementRef handle = editor.Resolve(Id, "Klemmen");
            bool isOutput = handle.Tag == "dataline_output";
            if (!DatalineAddress.TryEncode(Result.DataLine, Result.Terminal, isOutput, out string addressToken))
            {
                throw new EditRefusedException(
                    EditRefusalCodes.TerminalAddressRange,
                    "Klemmenummeret ligger uden for datalinjens område.");
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

    // UpdateProduct is gone (T031). It wrote a FIXED attribute list on every product, and the family variation it
    // could not express had already cost one crash: product_rs485_led_dimmer declares no power_group, cabletype or
    // cablenumber, so committing its dialog threw out of SetAttribute — from a dialog that had opened perfectly
    // well. The fix at the time was a SetIfDeclared escape hatch for three attributes; ApplyProductDialog needs no
    // such hatch, because it writes only the fields the composed dialog actually offered.

    // The shared "change Location" re-parent guard that used to live here is GONE. Neither properties dialog
    // re-parents any more: the product dialog never offered the choice (A-13), and the modem dialog's
    // `Placering` is the vendor's free-text POSITION descriptor — where in the room the device physically
    // sits — not a locality picker. Moving a product between localities is a tree operation (US-054).
}
