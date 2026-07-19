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
        int DataLine, int Terminal, string CableColour, string Note, bool InitialValueOn);

    // ---- Commands ----

    /// <summary>Inserts a catalog product into a locality (US-010), producing the new product's id. The
    /// at-most-one-modem guard and its dialog stay GUI-side (before Apply).</summary>
    public sealed record AddProduct(ElementId LocalityId, ProductDefinition Definition) : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Insert product";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(LocalityId) is not null
                ? EditVerdict.Allow
                : EditVerdict.Refuse("The locality no longer exists.");
        internal override ElementId ExecuteCore(ProjectEditor editor) =>
            editor.Group(LocalityId).AddProduct(Definition).Id;
    }

    /// <summary>Inserts a preprogrammed library function block into a locality (US-018), producing its id.</summary>
    public sealed record AddFunctionBlock(ElementId LocalityId, FunctionBlockDefinition Definition)
        : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Insert function block";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(LocalityId) is not null
                ? EditVerdict.Allow
                : EditVerdict.Refuse("The locality no longer exists.");
        internal override ElementId ExecuteCore(ProjectEditor editor) =>
            editor.Group(LocalityId).AddFunctionBlock(Definition).Id;
    }

    /// <summary>Inserts an empty "from scratch" function block into a locality (US-019), producing its id.</summary>
    public sealed record AddEmptyFunctionBlock(
        ElementId LocalityId, FunctionBlockDefinition Template, DateOnly Created, string Name)
        : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Insert function block";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(LocalityId) is not null
                ? EditVerdict.Allow
                : EditVerdict.Refuse("The locality no longer exists.");
        internal override ElementId ExecuteCore(ProjectEditor editor) =>
            editor.Group(LocalityId).AddEmptyFunctionBlock(Template, Created, Name).Id;
    }

    /// <summary>Unlocks a library function block for editing (US-020): clears its <c>locked</c> flag.</summary>
    public sealed record UnlockFunctionBlock(ElementId Id) : ProjectCommand
    {
        internal override string Describe(Project project) => "Unlock function block";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(Id) is not null
                ? EditVerdict.Allow
                : EditVerdict.Refuse("The function block no longer exists.");
        internal override void Execute(ProjectEditor editor)
        {
            if (!editor.TryResolve(Id, out ElementRef? handle))
            {
                throw new EditRefusedException("The function block no longer exists.");
            }
            handle.SetAttribute("locked", "no");
        }
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
            if (!editor.TryResolve(Id, out ElementRef? handle))
            {
                throw new EditRefusedException("The pin no longer exists.");
            }
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
        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(Id) is not null
                ? EditVerdict.Allow
                : EditVerdict.Refuse("The product no longer exists.");
        internal override void Execute(ProjectEditor editor)
        {
            if (!editor.TryResolve(Id, out ElementRef? handle))
            {
                throw new EditRefusedException("The product no longer exists.");
            }
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
            if (ElementId.TryParse(Result.LocalityId, out ElementId target)
                && CurrentLocalityId is { } current && current != target)
            {
                editor.MoveSubtree(Id, target);   // Location changed → re-parent (ids preserved)
            }
        }
    }
}
