#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>The edited advanced wireless-dimmer settings (US-015). An edit payload moved down from the GUI dialog.</summary>
    public sealed record AdvancedDimmerResult(
        int SoftOnMs, int SoftOffMs, int ManualRampS, int MinimumPercent, int MaximumPercent, string LoadMode);

    /// <summary>The edited modem documentation (US-013). An edit payload moved down from the GUI dialog.</summary>
    public sealed record ModemPropertiesResult(
        string Name, string LocalityId, string Note, string IdentificationCode,
        string Cable0V, string Cable24V, string CableRS485Minus, string CableRS485Plus,
        string PinCode, IReadOnlyList<string> PhoneNumbers);

    /// <summary>Applies edited project/customer/installer information (US-039); exercises the id-less metadata path.</summary>
    public sealed record UpdateProjectInfo(ProjectInfoData Data) : ProjectCommand
    {
        internal override string Describe(Project project) => "Edit project information";
        internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;
        internal override void Execute(ProjectEditor editor)
        {
            editor.SetMetadata("project_info",
                ("description", Data.Description), ("number", Data.Number), ("programmer", Data.Programmer));
            WriteContact(editor, "customer_info", Data.Customer);
            WriteContact(editor, "installer_info", Data.Installer);
        }

        private static void WriteContact(ProjectEditor editor, string tag, ContactInfo c) =>
            editor.SetMetadata(tag, ("name", c.Name), ("address", c.Address), ("city", c.City),
                ("zipcode", c.Zip), ("country", c.Country), ("phone", c.Phone), ("mobilephone", c.Mobile), ("email", c.Email));
    }

    /// <summary>Appends a user-defined text (US-049), creating the user-texts table on first use. The caller reports
    /// whether the table already exists.</summary>
    public sealed record AddUserText(string Text, bool TableExists) : ProjectCommand
    {
        internal override string Describe(Project project) => "Add text";
        internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;
        internal override void Execute(ProjectEditor editor)
        {
            EnumDefinitionRef def = TableExists
                ? editor.EnumDefinition(ProjectProjections.UserTextsTableName)
                : editor.AddEnumDefinition(ProjectProjections.UserTextsTableName);
            editor.AddEnumValues(def, Text);
        }
    }

    /// <summary>Renames a user-defined text by id (US-049 Edit).</summary>
    public sealed record UpdateUserText(ElementId TextId, string Text) : ProjectCommand
    {
        internal override string Describe(Project project) => "Edit text";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(TextId) is not null ? EditVerdict.Allow : EditVerdict.Refuse("The text no longer exists.");
        internal override void Execute(ProjectEditor editor)
        {
            if (!editor.TryResolve(TextId, out ElementRef? handle))
            {
                throw new EditRefusedException("The text no longer exists.");
            }
            handle.SetAttribute("name", Text);
        }
    }

    /// <summary>Deletes a user-defined text by id (US-049 Delete).</summary>
    public sealed record DeleteUserText(ElementId TextId) : ProjectCommand
    {
        internal override string Describe(Project project) => "Delete text";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(TextId) is not null ? EditVerdict.Allow : EditVerdict.Refuse("The text no longer exists.");
        internal override void Execute(ProjectEditor editor) =>
            editor.DeleteById(TextId, DeleteReferencePolicy.CascadeReferences);
    }

    /// <summary>Adds a typed variable to a function-block variable section (US-027), returning its id. The caller
    /// resolves the owning block id and the section tag.</summary>
    public sealed record AddVariable(ElementId BlockId, string SectionTag, string ResourceTag, string Name)
        : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Add variable";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(BlockId)?.Tag == "functionblock"
                ? EditVerdict.Allow : EditVerdict.Refuse("The target is not a function block.");
        internal override ElementId ExecuteCore(ProjectEditor editor)
        {
            FunctionBlockRef fb = editor.FunctionBlock(BlockId);
            ResourceRef added = SectionTag switch
            {
                "inputs" => fb.AddInput(ResourceTag, Name),
                "outputs" => fb.AddOutput(ResourceTag, Name),
                "settings" => fb.AddSetting(ResourceTag, Name),
                "internalsettings" => fb.AddInternalVariable(ResourceTag, Name),
                _ => throw new EditRefusedException($"<{SectionTag}> is not a function-block variable section."),
            };
            return added.Id ?? throw new EditRefusedException("The variable was not added.");
        }
    }

    /// <summary>Creates a project-global enumerator type and adds a variable of it to a block section (US-030),
    /// returning the variable's id. The caller resolves the owning block id and the section tag.</summary>
    public sealed record AddEnumVariable(
        ElementId BlockId, string SectionTag, string VariableName, string TypeName, IReadOnlyList<string> States)
        : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Add enumerator";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(BlockId)?.Tag == "functionblock"
                ? EditVerdict.Allow : EditVerdict.Refuse("The target is not a function block.");
        internal override ElementId ExecuteCore(ProjectEditor editor)
        {
            EnumDefinitionRef def = editor.AddEnumDefinition(TypeName, States.ToArray());
            FunctionBlockRef fb = editor.FunctionBlock(BlockId);
            void Configure(ElementRef r)
            {
                r.SetAttribute("typedef", def.Typedef);
                if (States.Count > 0)
                {
                    r.SetAttribute("inivalue", def.InitialValue(States[0]));
                }
            }
            ResourceRef added = SectionTag switch
            {
                "settings" => fb.AddSetting("resource_enum", VariableName, Configure),
                "internalsettings" => fb.AddInternalVariable("resource_enum", VariableName, Configure),
                _ => throw new EditRefusedException($"<{SectionTag}> does not accept an enum variable."),
            };
            return added.Id ?? throw new EditRefusedException("The variable was not added.");
        }
    }

    /// <summary>Appends newly-listed states to an existing enumerator type (US-030). The caller computes the not-yet-
    /// present states; an empty append is a no-op (NoChange) — the old hand-rolled bypass and its early-return die.</summary>
    public sealed record UpdateEnumStates(string DefName, IReadOnlyList<string> Added) : ProjectCommand
    {
        internal override string Describe(Project project) => "Edit enumerator";
        internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;
        internal override void Execute(ProjectEditor editor) =>
            editor.AddEnumValues(editor.EnumDefinition(DefName), Added.ToArray());
    }

    /// <summary>Applies edited advanced wireless-dimmer settings (US-015): the six dimmer_setting values.</summary>
    public sealed record UpdateDimmerSettings(ElementId ProductId, AdvancedDimmerResult Result) : ProjectCommand
    {
        internal override string Describe(Project project) => "Edit dimmer settings";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(ProductId) is not null ? EditVerdict.Allow : EditVerdict.Refuse("The product no longer exists.");
        internal override void Execute(ProjectEditor editor)
        {
            ProjectElement product = editor.Require(ProductId);
            void SetSetting(string tag, string value)
            {
                if (product.DescendantsAndSelf().FirstOrDefault(e => e.Tag == tag) is { Id: { } sid }
                    && editor.TryResolve(sid, out ElementRef? h))
                {
                    h.SetAttribute("value", value);
                }
            }
            static string Dec(int v) => v.ToString(CultureInfo.InvariantCulture);
            SetSetting("dimmer_setting_fade_rate_up", Dec(Result.SoftOnMs));
            SetSetting("dimmer_setting_fade_rate_down", Dec(Result.SoftOffMs));
            SetSetting("dimmer_setting_dimming_rate", Dec(Result.ManualRampS));
            SetSetting("dimmer_setting_minimum_value", Dec(Result.MinimumPercent));
            SetSetting("dimmer_setting_maximum_value", Dec(Result.MaximumPercent));
            SetSetting("dimmer_setting_load_mode", Result.LoadMode);
        }
    }

    /// <summary>Applies edited modem documentation (US-013): name/note/id-code, the four RS485 cable colours, the SIM
    /// pincode, the phone-number slots, and a re-parent when the Location changed (caller supplies the current parent).</summary>
    public sealed record UpdateModem(ElementId ModemId, ModemPropertiesResult Result, ElementId? CurrentLocalityId)
        : ProjectCommand
    {
        internal override string Describe(Project project) => "Edit modem";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(ModemId) is not null ? EditVerdict.Allow : EditVerdict.Refuse("The modem no longer exists.");
        internal override void Execute(ProjectEditor editor)
        {
            if (!editor.TryResolve(ModemId, out ElementRef? handle))
            {
                throw new EditRefusedException("The modem no longer exists.");
            }
            handle.SetAttribute("name", Result.Name);
            handle.SetAttribute("note", Result.Note);
            handle.SetAttribute("documentation_tag", Result.IdentificationCode);
            handle.SetAttribute("cablecolour_0V", Result.Cable0V);
            handle.SetAttribute("cablecolour_24V", Result.Cable24V);
            handle.SetAttribute("cablecolour_RS485Minus", Result.CableRS485Minus);
            handle.SetAttribute("cablecolour_RS485Plus", Result.CableRS485Plus);

            ProjectElement modem = editor.Require(ModemId);
            if (modem.DescendantsAndSelf().FirstOrDefault(e => e.Tag == "sms_modem_pincode") is { Id: { } pinId }
                && editor.TryResolve(pinId, out ElementRef? pinHandle))
            {
                pinHandle.SetAttribute("value", string.IsNullOrEmpty(Result.PinCode) ? "0" : Result.PinCode);
            }
            for (int i = 0; i < Result.PhoneNumbers.Count; i++)
            {
                string slot = (i + 1).ToString(CultureInfo.InvariantCulture);
                if (modem.DescendantsAndSelf().FirstOrDefault(e => e.Tag == "sms_modem_phonenumber"
                        && e.GetAttribute("address") == slot) is { Id: { } pnId }
                    && editor.TryResolve(pnId, out ElementRef? pnHandle))
                {
                    pnHandle.SetAttribute("phonenumber", Result.PhoneNumbers[i]);
                }
            }
            if (ElementId.TryParse(Result.LocalityId, out ElementId target)
                && CurrentLocalityId is { } current && current != target)
            {
                editor.MoveSubtree(ModemId, target);
            }
        }
    }
}
