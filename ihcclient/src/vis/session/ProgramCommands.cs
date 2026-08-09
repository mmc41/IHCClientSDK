#nullable enable
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Programs;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>Authors a program event (US-028) on the program owning the events container. The caller resolves the
    /// owning program id (parent of the "events" container).</summary>
    public sealed record AddProgramEvent(ElementId ProgramId, ElementId VariableId, string Method, string Name, string? Note, ElementId? OperandId = null)
        : ProjectCommand
    {
        internal override string Describe(Project project) => "Tilføj hændelse";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireTag(ProgramId, "a program", "program_simple")
                .And(context.RequireUnlockedTarget(ProgramId, inclusive: true));   // T003
        internal override void Execute(ProjectEditor editor) =>   // OperandId is the second operand %S (T008), else unary
            editor.Program(ProgramId).AddEvent(Name, editor.Resource(VariableId), Method,
                OperandId is { } op ? editor.Resource(op) : null, note: Note);
    }

    /// <summary>Adds a Powerup system event (US-033) to a program.</summary>
    public sealed record AddPowerEvent(ElementId ProgramId) : ProjectCommand
    {
        internal override string Describe(Project project) => "Tilføj Powerup hændelse";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireTag(ProgramId, "a program", "program_simple")
                .And(context.RequireUnlockedTarget(ProgramId, inclusive: true));   // T003
        internal override void Execute(ProjectEditor editor) =>
            editor.Program(ProgramId).AddPowerEvent("Powerup",
                "Runs the program on controller power-up (also on project transfer and software restart).");
    }

    /// <summary>Authors a program action command (US-028) into a command container.</summary>
    public sealed record AddProgramCommand(ElementId ContainerId, ElementId VariableId, string Method, string Name, string? Note)
        : ProjectCommand
    {
        internal override string Describe(Project project) => "Tilføj kommando";
        internal override EditVerdict Evaluate(EditContext context) =>
            Programs.RequireCommandContainer(context, ContainerId)
                .And(context.RequireUnlockedTarget(ContainerId, inclusive: true));   // T003
        internal override void Execute(ProjectEditor editor) =>
            editor.Branch(ContainerId).AddAction(Name, editor.Resource(VariableId), Method, note: Note);
    }

    /// <summary>
    /// Adds a new, empty program to a function block's <c>programs</c> container (US-026, uxparity2 W4/RC3). A block
    /// may hold more than one program — <c>project2-CustomBlock.vis</c>'s `AutoProof` carries two — and creating one
    /// was the missing SDK command behind "no route to create a Program"; the GUI menu entry alone could not have
    /// worked. The program is created with the vendor's own decoration and its two mandatory containers, so a project
    /// containing it is structurally indistinguishable from a vendor-authored one.
    /// </summary>
    public sealed record AddProgram(ElementId ProgramsId, string Name) : ProjectCommand
    {
        internal override string Describe(Project project) => $"Tilføj program '{Name}'";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireTag(ProgramsId, "a programs container", "programs")
                .And(context.RequireUnlockedTarget(ProgramsId, inclusive: true));
        internal override void Execute(ProjectEditor editor) => ProgramGrammar.CreateProgram(editor, ProgramsId, Name);
    }

    /// <summary>Inserts a conditional sub-program (US-029) into a command container.</summary>
    public sealed record AddSubProgram(ElementId CommandsId) : ProjectCommand
    {
        internal override string Describe(Project project) => "Tilføj under program";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireTag(CommandsId, "a command container", "actions")
                .And(context.RequireUnlockedTarget(CommandsId, inclusive: true));   // T003
        internal override void Execute(ProjectEditor editor) => editor.Branch(CommandsId).AddSubProgram();
    }

    /// <summary>Adds a condition (US-029) to a conditions group.</summary>
    public sealed record AddCondition(ElementId ConditionsId, ElementId VariableId, string Method, string Name, string? Note, ElementId? OperandId = null)
        : ProjectCommand
    {
        internal override string Describe(Project project) => "Tilføj betingelse";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireTag(ConditionsId, "a conditions group", "conditions")
                .And(context.RequireUnlockedTarget(ConditionsId, inclusive: true));   // T003
        internal override void Execute(ProjectEditor editor) =>   // OperandId is the second operand %S (T008), else unary
            editor.ConditionsGroup(ConditionsId).AddCondition(Name, editor.Resource(VariableId), Method,
                OperandId is { } op ? editor.Resource(op) : null, note: Note);
    }

    /// <summary>Toggles a conditions group's AND/OR combination (US-029).</summary>
    public sealed record SetConditionsLogic(ElementId ConditionsId, bool Or) : ProjectCommand
    {
        internal override string Describe(Project project) => "Rediger betingelseslogik";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireTag(ConditionsId, "a conditions group", "conditions")
                .And(context.RequireUnlockedTarget(ConditionsId, inclusive: true));   // T004
        internal override void Execute(ProjectEditor editor)
        {
            ConditionsGroupRef group = editor.ConditionsGroup(ConditionsId);
            if (Or)
            {
                group.Or();
            }
            else
            {
                group.And();
            }
        }
    }

    /// <summary>Adds a nested logic group (US-029) inside a conditions group.</summary>
    public sealed record AddLogicGroup(ElementId ConditionsId) : ProjectCommand
    {
        internal override string Describe(Project project) => "Tilføj logik gruppe";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireTag(ConditionsId, "a conditions group", "conditions")
                .And(context.RequireUnlockedTarget(ConditionsId, inclusive: true));   // T003
        internal override void Execute(ProjectEditor editor) => editor.ConditionsGroup(ConditionsId).AddConditionGroup();
    }

    /// <summary>Authors one arithmetic command line (US-032) into a command container.</summary>
    public sealed record AddArithmeticCommand(ElementId CommandsId, ElementId TargetId, string Method, ElementId OperandId, string Name)
        : ProjectCommand
    {
        internal override string Describe(Project project) => "Tilføj aritmetik";
        internal override EditVerdict Evaluate(EditContext context) =>
            Programs.RequireCommandContainer(context, CommandsId)
                .And(context.RequireUnlockedTarget(CommandsId, inclusive: true));   // T003
        internal override void Execute(ProjectEditor editor) =>
            editor.Branch(CommandsId).AddAction(Name, editor.Resource(TargetId), Method, editor.Resource(OperandId));
    }

    /// <summary>Inserts a case structure (US-031) keyed on an eligible switch variable.</summary>
    public sealed record AddCase(ElementId CommandsId, ElementId SwitchVariableId) : ProjectCommand
    {
        internal override string Describe(Project project) => "Tilføj case";
        internal override EditVerdict Evaluate(EditContext context) =>
            (Programs.IsCommandContainer(context, CommandsId)
            && context.Index.FindById(SwitchVariableId) is { } v && ProgramMethodCatalog.EligibleCaseVariableTags.Contains(v.Tag)
                ? EditVerdict.Allow
                : EditVerdict.Refuse("Not an eligible case switch on a command container."))
            .And(context.RequireUnlockedTarget(CommandsId, inclusive: true));   // T003
        internal override void Execute(ProjectEditor editor) =>
            editor.Branch(CommandsId).AddCase("Case", editor.Resource(SwitchVariableId));
    }

    /// <summary>Adds a case-value branch (US-031). For a literal switch the <paramref name="Criterion"/> is embedded as
    /// the operand's <c>inivalue</c> on a <paramref name="SwitchTag"/>-typed operand; for an ENUM switch (T014, PG-6)
    /// <paramref name="EnumTypeName"/> is set and the criterion is a STATE name — routed to the engine's enum overload
    /// so the operand carries the type's <c>typedef</c> plus the state's <c>inivalue</c>. The caller supplies the
    /// switch tag / enum type resolved from the case's switch.</summary>
    public sealed record AddCaseValue(ElementId CaseId, string Criterion, string SwitchTag, string? EnumTypeName = null) : ProjectCommand
    {
        internal override string Describe(Project project) => "Tilføj case værdi";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireTag(CaseId, "a case", "program_case")
                .And(context.RequireUnlockedTarget(CaseId, inclusive: true));   // T003
        internal override void Execute(ProjectEditor editor)
        {
            if (EnumTypeName is { } typeName)
            {
                editor.Case(CaseId).Case(Criterion, editor.EnumDefinition(typeName), Criterion);
            }
            else
            {
                editor.Case(CaseId).Case(Criterion, SwitchTag, op => op.SetAttribute("inivalue", Criterion));
            }
        }
    }

    /// <summary>Sets an output's "Gem aktuel værdi" power-loss persistence (US-033).</summary>
    public sealed record SetOutputBackup(ElementId OutputId, bool Save) : ProjectCommand
    {
        internal override string Describe(Project project) => "Gem aktuel værdi";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireTag(OutputId, "an output", "resource_output", "dataline_output", "airlink_relay")
                .And(context.RequireUnlockedTarget(OutputId, inclusive: true));   // T004
        internal override void Execute(ProjectEditor editor) =>
            editor.Resolve(OutputId, "output").SetAttribute("backup", Save ? "yes" : "no");
    }

    /// <summary>Toggles a "Log …" row's log mark (US-068).</summary>
    public sealed record ToggleLogMark(ElementId LogRowId) : ProjectCommand
    {
        internal override string Describe(Project project) => "Skift logmærke";
        internal override EditVerdict Evaluate(EditContext context) =>
            (context.Index.FindById(LogRowId) is { } row && row.IsLogRow(context.Project)
                ? EditVerdict.Allow : EditVerdict.Refuse("Not a Logning row."))
            .And(context.RequireUnlockedTarget(LogRowId, inclusive: true));   // T004 (defensive: log rows are product-scoped)
        internal override void Execute(ProjectEditor editor) => editor.ToggleLogMark(LogRowId);
    }

    internal static class Programs
    {
        public static bool IsCommandContainer(EditContext context, ElementId id) =>
            context.Index.FindById(id)?.Tag is "actions" or "case_action";

        public static EditVerdict RequireCommandContainer(EditContext context, ElementId id) =>
            IsCommandContainer(context, id)
                ? EditVerdict.Allow : EditVerdict.Refuse("The target is not a command container.");
    }
}
