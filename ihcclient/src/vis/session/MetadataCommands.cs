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
                ("description", Data.Description), ("number", Data.Number), ("programmer", Data.Programmer),
                ("type", Data.Type), ("drawing", Data.Drawing));
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
            context.RequireExists(TextId, "text");
        internal override void Execute(ProjectEditor editor) =>
            editor.Resolve(TextId, "text").SetAttribute("name", Text);
    }

    /// <summary>Deletes a user-defined text by id (US-049 Delete).</summary>
    public sealed record DeleteUserText(ElementId TextId) : ProjectCommand
    {
        internal override string Describe(Project project) => "Delete text";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireExists(TextId, "text");
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
            context.RequireTag(BlockId, "a function block", "functionblock")
                .And(context.RequireUnlockedTarget(BlockId, inclusive: true));   // T003
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
            context.RequireTag(BlockId, "a function block", "functionblock")
                .And(context.RequireUnlockedTarget(BlockId, inclusive: true));   // T003
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

    /// <summary>Adds a variable of an EXISTING project-global enumerator type to a block section (US-030 enum-type
    /// picker, PG-4): references the type's def-id (wiring <c>typedef</c> + the default <c>inivalue</c>), authoring NO
    /// new type. The caller resolves the owning block id and the existing type name.</summary>
    public sealed record AddEnumVariableOfExistingType(ElementId BlockId, string SectionTag, string VariableName, string TypeName)
        : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Add enumerator";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireTag(BlockId, "a function block", "functionblock")
                .And(context.RequireUnlockedTarget(BlockId, inclusive: true));   // T003
        internal override ElementId ExecuteCore(ProjectEditor editor)
        {
            EnumDefinitionRef def = editor.EnumDefinition(TypeName);   // resolve the EXISTING type — no new def authored
            FunctionBlockRef fb = editor.FunctionBlock(BlockId);
            void Configure(ElementRef r)
            {
                r.SetAttribute("typedef", def.Typedef);
                if (def.FirstValue is { } inivalue)
                {
                    r.SetAttribute("inivalue", inivalue);
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

    /// <summary>Authors a project-global enumerator TYPE with no variable (US-030 standalone/empty-type route, PG-7,
    /// D02): a 0-state, unreferenced type when <paramref name="States"/> is empty — distinct from the variable-insert
    /// "New…" which also inserts a resource_enum. The type lands in the project-global <c>enum_definitions</c> container.</summary>
    public sealed record AddStandaloneEnumType(string TypeName, IReadOnlyList<string> States) : ProjectCommand
    {
        internal override string Describe(Project project) => "Add enumerator type";
        internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;   // project-global — no block target
        internal override void Execute(ProjectEditor editor) => editor.AddEnumDefinition(TypeName, States.ToArray());
    }

    /// <summary>Edits an existing enumerator type (US-030): relabels changed existing states in place (T013) then
    /// appends the newly-listed ones. The caller diffs the dialog's full ordered list into <see cref="Relabels"/> and
    /// <paramref name="Added"/>; with both empty the command is a no-op (NoChange). Relabels run first (a built-in
    /// "[read only]" type is refused by the engine); reorder / remove / rename-type are out of scope (D05).</summary>
    public sealed record UpdateEnumStates(string DefName, IReadOnlyList<string> Added) : ProjectCommand
    {
        /// <summary>Position-keyed relabels of EXISTING values (T013): each targeted value's id paired with its new
        /// label. Defaults to none, so the append-only construction stays valid.</summary>
        public IReadOnlyList<(ElementId ValueId, string NewName)> Relabels { get; init; } = [];

        internal override string Describe(Project project) => "Edit enumerator";
        internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;
        internal override void Execute(ProjectEditor editor)
        {
            EnumDefinitionRef def = editor.EnumDefinition(DefName);
            foreach ((ElementId valueId, string newName) in Relabels)
            {
                def = editor.RelabelEnumValue(def, valueId, newName);
            }
            if (Added.Count > 0)
            {
                editor.AddEnumValues(def, Added.ToArray());
            }
        }
    }

    /// <summary>Renames a project-global enumerator TYPE (IHC Visual's <i>Bibliotek ▸ Rediger Enumerator typer ▸
    /// Omdøb</i>). References are by id, so every resource keeps pointing at it. The engine refuses a "[read only]"
    /// built-in, matching the vendor's greyed <i>Omdøb</i>.</summary>
    public sealed record RenameEnumType(string DefName, string NewName) : ProjectCommand
    {
        internal override string Describe(Project project) => "Rename enumerator type";
        internal override EditVerdict Evaluate(EditContext context) => EnumTypeTarget.RequireEditable(context, DefName);
        internal override void Execute(ProjectEditor editor) =>
            editor.RenameEnumDefinition(editor.EnumDefinition(DefName), NewName);
    }

    /// <summary>Deletes a project-global enumerator TYPE and its values (<i>Bibliotek ▸ … ▸ Slet</i>, types pane).
    /// The engine refuses a "[read only]" built-in and one still referenced by a resource, so a delete can never
    /// strand a <c>typedef</c>.</summary>
    public sealed record DeleteEnumType(string DefName) : ProjectCommand
    {
        internal override string Describe(Project project) => "Delete enumerator type";
        internal override EditVerdict Evaluate(EditContext context) =>
            EnumTypeTarget.RequireEditable(context, DefName)
                .And(EnumTypeTarget.RequireUnreferenced(context, DefName));
        internal override void Execute(ProjectEditor editor) =>
            editor.RemoveEnumDefinition(editor.EnumDefinition(DefName));
    }

    /// <summary>Appends ONE value to an enumerator type (<i>Bibliotek ▸ … ▸ Ny</i>, values pane). The one-at-a-time
    /// peer of <see cref="UpdateEnumStates"/>, which the vendor's values pane adds them as.</summary>
    public sealed record AddEnumValue(string DefName, string ValueName) : ProjectCommand
    {
        internal override string Describe(Project project) => "Add enumerator value";
        internal override EditVerdict Evaluate(EditContext context) => EnumTypeTarget.RequireEditable(context, DefName);
        internal override void Execute(ProjectEditor editor) =>
            editor.AddEnumValues(editor.EnumDefinition(DefName), ValueName);
    }

    /// <summary>Renames ONE value of an enumerator type (<i>Bibliotek ▸ … ▸ Omdøb</i>, values pane), addressed by its
    /// 0-based POSITION in the type's value list — what the dialog shows. Id and index are preserved.</summary>
    public sealed record RenameEnumValue(string DefName, int ValueIndex, string NewName) : ProjectCommand
    {
        internal override string Describe(Project project) => "Rename enumerator value";
        internal override EditVerdict Evaluate(EditContext context) =>
            EnumTypeTarget.RequireEditable(context, DefName)
                .And(EnumTypeTarget.RequireValueAt(context, DefName, ValueIndex));
        internal override void Execute(ProjectEditor editor)
        {
            EnumDefinitionRef def = editor.EnumDefinition(DefName);
            editor.RelabelEnumValue(def, EnumValueAddressing.At(def, ValueIndex), NewName);
        }
    }

    /// <summary>Deletes ONE value of an enumerator type (<i>Bibliotek ▸ … ▸ Slet</i>, values pane), addressed by its
    /// 0-based POSITION. The engine refuses a value still in use as some resource's initial value.</summary>
    public sealed record DeleteEnumValue(string DefName, int ValueIndex) : ProjectCommand
    {
        internal override string Describe(Project project) => "Delete enumerator value";
        internal override EditVerdict Evaluate(EditContext context) =>
            EnumTypeTarget.RequireEditable(context, DefName)
                .And(EnumTypeTarget.RequireValueAt(context, DefName, ValueIndex));
        internal override void Execute(ProjectEditor editor)
        {
            EnumDefinitionRef def = editor.EnumDefinition(DefName);
            editor.RemoveEnumValue(def, EnumValueAddressing.At(def, ValueIndex));
        }
    }

    /// <summary>Turns the dialog's 0-based value POSITION into the value's id — the one place the positional
    /// addressing the two value commands share becomes an id, so an out-of-range position refuses identically.</summary>
    internal static class EnumValueAddressing
    {
        internal static ElementId At(EnumDefinitionRef definition, int index) =>
            index >= 0 && index < definition.Values.Count
                ? definition.Values[index].Id
                : throw new EditRefusedException(
                    $"Enumerator type '{definition.Typedef}' has no value at position {index}.");
    }

    /// <summary>
    /// The LEGALITY gates the five enum-manager commands share, phrased as <see cref="EditVerdict"/>s so an illegal
    /// edit comes back <c>Refused</c> with a sentence the dialog can show — not <c>Failed</c>, which is what a bare
    /// engine <see cref="System.InvalidOperationException"/> would produce. The engine's own guards stay as the
    /// backstop; these exist so the refusal is a verdict rather than a fault.
    /// </summary>
    internal static class EnumTypeTarget
    {
        /// <summary>The type must exist and must not be a <c>typeid</c>-bearing built-in — IHC Visual greys
        /// <i>Slet</i>/<i>Omdøb</i> and all three value buttons on a "[read only]" one.</summary>
        internal static EditVerdict RequireEditable(EditContext context, string defName) =>
            Find(context.Project, defName) switch
            {
                null => EditVerdict.Refuse($"The project has no enumerator type named '{defName}'."),
                { } def when (def.GetAttribute("typeid") ?? ElementId.NullToken) != ElementId.NullToken =>
                    EditVerdict.Refuse($"Enumerator type '{defName}' is a built-in [read only] type and cannot be edited."),
                _ => EditVerdict.Allow,
            };

        /// <summary>The type must not still be referenced by a resource's <c>typedef</c>: deleting it would leave that
        /// resource pointing at nothing.</summary>
        internal static EditVerdict RequireUnreferenced(EditContext context, string defName)
        {
            if (Find(context.Project, defName) is not { Id: { } defId })
            {
                return EditVerdict.Allow;   // RequireEditable already refused it; do not double-report
            }
            int users = context.Project.Root.DescendantsAndSelf().Count(e =>
                e.GetAttribute("typedef") is { } token
                && ElementId.TryParse(token, out ElementId referenced)
                && referenced == defId);
            return users == 0
                ? EditVerdict.Allow
                : EditVerdict.Refuse($"Enumerator type '{defName}' is still used by {users} resource(s) and cannot be deleted.");
        }

        /// <summary>The 0-based value position must exist in the type — the dialog addresses values by position.</summary>
        internal static EditVerdict RequireValueAt(EditContext context, string defName, int index)
        {
            if (Find(context.Project, defName) is not { } def)
            {
                return EditVerdict.Allow;   // RequireEditable already refused it
            }
            int count = def.ChildrenOrEmpty().Count(v => v.Tag == "enum_value");
            return index >= 0 && index < count
                ? EditVerdict.Allow
                : EditVerdict.Refuse($"Enumerator type '{defName}' has no value at position {index}.");
        }

        private static ProjectElement? Find(Project project, string defName) =>
            project.Child("enum_definitions")?.ChildrenOrEmpty()
                .FirstOrDefault(c => c.Tag == "enum_definition" && c.GetAttribute("name") == defName);
    }

    /// <summary>Applies edited advanced wireless-dimmer settings (US-015): the six dimmer_setting values.</summary>
    public sealed record UpdateDimmerSettings(ElementId ProductId, AdvancedDimmerResult Result) : ProjectCommand
    {
        internal override string Describe(Project project) => "Edit dimmer settings";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireExists(ProductId, "product");
        internal override void Execute(ProjectEditor editor)
        {
            ProjectElement product = editor.Require(ProductId);
            void SetSetting(string tag, string value) =>
                editor.SetDescendantAttribute(product, e => e.Tag == tag, "value", value);
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
        internal override EditVerdict Evaluate(EditContext context)
        {
            EditVerdict exists = context.RequireExists(ModemId, "modem");
            return exists.Ok ? Relocation.Verdict(context, CurrentLocalityId, Result.LocalityId) : exists;
        }
        internal override void Execute(ProjectEditor editor)
        {
            ElementRef handle = editor.Resolve(ModemId, "modem");
            handle.SetAttribute("name", Result.Name);
            handle.SetAttribute("note", Result.Note);
            handle.SetAttribute("documentation_tag", Result.IdentificationCode);
            handle.SetAttribute("cablecolour_0V", Result.Cable0V);
            handle.SetAttribute("cablecolour_24V", Result.Cable24V);
            handle.SetAttribute("cablecolour_RS485Minus", Result.CableRS485Minus);
            handle.SetAttribute("cablecolour_RS485Plus", Result.CableRS485Plus);

            ProjectElement modem = editor.Require(ModemId);
            editor.SetDescendantAttribute(modem, e => e.Tag == "sms_modem_pincode", "value",
                string.IsNullOrEmpty(Result.PinCode) ? "0" : Result.PinCode);
            for (int i = 0; i < Result.PhoneNumbers.Count; i++)
            {
                string slot = (i + 1).ToString(CultureInfo.InvariantCulture);
                editor.SetDescendantAttribute(modem,
                    e => e.Tag == "sms_modem_phonenumber" && e.GetAttribute("address") == slot,
                    "phonenumber", Result.PhoneNumbers[i]);
            }
            Relocation.Apply(editor, ModemId, CurrentLocalityId, Result.LocalityId);   // Location changed → re-parent
        }
    }
}
