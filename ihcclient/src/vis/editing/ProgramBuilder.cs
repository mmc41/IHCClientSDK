#nullable enable
using System;
using System.Collections.Generic;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
namespace Ihc.Vis.Editing
{
    /// <summary>
    /// Nested fluent builder for authoring a custom program directly into a function block's <c>program_simple</c> —
    /// the "fill an empty block's logic by hand" path (project2's "Custom blok", counters 204–247), rather than
    /// receiving a program whole from a catalog deep-copy. Adds <c>event_power</c>/<c>event</c> triggers to the
    /// program's <c>events</c> container and nested <c>program_sub</c> logic (a conditions list + true/false action
    /// branches) to its root <c>actions</c> container. Leaf triggers/conditions/actions reference resources by live
    /// <see cref="ResourceRef"/> — the same handles <see cref="ProjectEditor.Link(ResourceRef,ResourceRef)"/> consumes — and carry an opaque
    /// <c>method</c> operation token (from the install <c>Data\mNN.def</c> vocabulary).
    /// </summary>
    /// <remarks>
    /// Every add allocates exactly one id off the project counter in user-action order (R3); adding a sub-program
    /// allocates its four-node skeleton (program_sub, conditions, true actions, false actions) contiguously — the
    /// vendor "add sub-program" gesture. Structural decorations (container names/notes/icons and the branch
    /// <c>type</c>) are the fixed vendor strings; the leaf <c>note</c> is caller-supplied (method-specific).
    /// </remarks>
    public sealed class ProgramBuilder
    {
        private readonly ProjectEditor editor;
        private readonly ElementId eventsId;
        private readonly ElementId actionsId;

        internal ProgramBuilder(ProjectEditor editor, ElementId programSimpleId)
        {
            this.editor = editor;
            eventsId = editor.RequireChildId(programSimpleId, "events");
            actionsId = editor.RequireChildId(programSimpleId, "actions");
        }

        /// <summary>Adds a power-up trigger (<c>event_power</c>, e.g. "Powerup") to the program's events. Returns this.</summary>
        public ProgramBuilder AddPowerEvent(string name, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            editor.AllocateChild(eventsId, "event_power", ProgramGrammar.LeafAttrs(name, ProgramGrammar.EventIcon, note));
            return this;
        }

        /// <summary>
        /// Adds a resource-triggered <c>event</c> — fires when <paramref name="link1"/> changes per the
        /// <paramref name="method"/> operation, optionally comparing against a second operand <paramref name="link2"/>.
        /// Returns this.
        /// </summary>
        public ProgramBuilder AddEvent(string name, ResourceRef link1, string method, ResourceRef? link2 = null, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(link1);
            ArgumentNullException.ThrowIfNull(method);
            ProgramGrammar.RequireLiveOperands(editor, link1, link2);
            editor.AllocateChild(eventsId, "event",
                ProgramGrammar.WiredAttrs(name, ProgramGrammar.EventIcon, note, link1, link2, method));
            return this;
        }

        /// <summary>
        /// Adds a top-level <c>action</c> command to the program's root actions (its "Commands" group) driving
        /// <paramref name="link1"/> per <paramref name="method"/>, optionally with a second operand
        /// <paramref name="link2"/> — the unconditional command counterpart of <see cref="AddEvent"/> (US-028: a
        /// program's events fire its commands top-to-bottom). Returns this for chaining.
        /// </summary>
        public ProgramBuilder AddAction(string name, ResourceRef link1, string method, ResourceRef? link2 = null, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(link1);
            ArgumentNullException.ThrowIfNull(method);
            ProgramGrammar.RequireLiveOperands(editor, link1, link2);
            editor.AllocateChild(actionsId, "action",
                ProgramGrammar.WiredAttrs(name, ProgramGrammar.ActionIcon, note, link1, link2, method));
            return this;
        }

        /// <summary>
        /// Adds a nested <c>program_sub</c> ("Under program") to the program's root actions, auto-creating its three
        /// children — a <c>conditions</c> container and the true/false <c>actions</c> branches — as four contiguous
        /// ids (the vendor "add sub-program" skeleton). Returns a handle for authoring its conditions and branches.
        /// </summary>
        public SubProgramRef AddSubProgram() => ProgramGrammar.CreateSubProgram(editor, actionsId);

        /// <summary>
        /// Adds a <c>program_case</c> switch on <paramref name="switchVariable"/> to the program's root actions,
        /// eagerly allocating its default (Else) branch with it — the vendor case-insert gesture (US-031, ENG2-B2).
        /// Returns a handle for adding case values and reaching the default branch.
        /// </summary>
        public CaseRef AddCase(string name, ResourceRef switchVariable, string? note = null) =>
            ProgramGrammar.CreateCase(editor, actionsId, name, switchVariable, note);
    }

    /// <summary>
    /// A live handle to a nested <c>program_sub</c> authored via <see cref="ProgramBuilder.AddSubProgram"/> (or
    /// <see cref="BranchRef.AddSubProgram"/>): exposes its conditions list and its two action branches
    /// (<see cref="WhenTrue"/>/<see cref="WhenFalse"/>) for further authoring.
    /// </summary>
    public sealed class SubProgramRef
    {
        internal SubProgramRef(ProjectEditor editor, ElementId subId, ElementId conditionsId,
            ElementId trueActionsId, ElementId falseActionsId)
        {
            Id = subId;
            Conditions = new ConditionsGroupRef(editor, conditionsId);
            WhenTrue = new BranchRef(editor, trueActionsId);
            WhenFalse = new BranchRef(editor, falseActionsId);
        }

        internal ElementId Id { get; }

        /// <summary>
        /// This sub-program's top-level <c>conditions</c> group — the handle for OR/AND toggling, condition rows and
        /// nested logic groups (US-029).
        /// </summary>
        public ConditionsGroupRef Conditions { get; }

        /// <summary>The true-branch ("Kommandoer ved betingelser sande") action container.</summary>
        public BranchRef WhenTrue { get; }

        /// <summary>The false-branch ("Kommandoer ved betingelser falske") action container.</summary>
        public BranchRef WhenFalse { get; }

        /// <summary>
        /// Adds a <c>condition</c> to this sub-program's conditions list — a logical test on <paramref name="link1"/>
        /// per <paramref name="method"/>, optionally against <paramref name="link2"/>. Returns a handle for adding an
        /// embedded literal enum operand (see <see cref="ConditionRef.AddEnumOperand"/>).
        /// </summary>
        public ConditionRef AddCondition(string name, ResourceRef link1, string method, ResourceRef? link2 = null, string? note = null) =>
            Conditions.AddCondition(name, link1, method, link2, note);
    }

    /// <summary>
    /// A live handle to a <c>conditions</c> group — a boolean AND (default) or OR (<see cref="Or"/>) grouping of
    /// <c>condition</c> leaves and, recursively, nested logic groups (<see cref="AddConditionGroup"/> — the vendor
    /// "Logik gruppe", US-029). A sub-program's <see cref="SubProgramRef.Conditions"/> is its top-level group; an
    /// existing group loaded from file is addressed by id via <see cref="ProjectEditor.ConditionsGroup"/>.
    /// </summary>
    public sealed class ConditionsGroupRef
    {
        private readonly ProjectEditor editor;

        internal ConditionsGroupRef(ProjectEditor editor, ElementId id)
        {
            this.editor = editor;
            Id = id;
        }

        /// <summary>The <c>conditions</c> element's id.</summary>
        public ElementId Id { get; }

        /// <summary>
        /// Marks this group as an OR grouping — the vendor EK-dialog "Logisk betingelse" toggle, persisted as the
        /// literal <c>type="or"</c> and nothing else (ENG2-B1). Returns this.
        /// </summary>
        public ConditionsGroupRef Or()
        {
            editor.SetAttributeById(Id, "type", "or");
            return this;
        }

        /// <summary>
        /// Restores the default AND grouping — <c>type</c> returns to its DTD default <c>and</c>, which the
        /// canonicalizer re-omits, so an Or/And cycle leaves no byte trace. Returns this.
        /// </summary>
        public ConditionsGroupRef And()
        {
            editor.SetAttributeById(Id, "type", "and");
            return this;
        }

        /// <summary>
        /// Adds a <c>condition</c> to this group — a logical test on <paramref name="link1"/> per
        /// <paramref name="method"/>, optionally against <paramref name="link2"/>. The persisted
        /// <paramref name="name"/> is the vendor's <c>%P</c>/<c>%S</c> template form, not the popup's substituted
        /// display label (ENG2-B1). Returns a handle for adding an embedded literal enum operand.
        /// </summary>
        public ConditionRef AddCondition(string name, ResourceRef link1, string method, ResourceRef? link2 = null, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(link1);
            ArgumentNullException.ThrowIfNull(method);
            ProgramGrammar.RequireLiveOperands(editor, link1, link2);
            ElementId id = editor.AllocateChild(Id, "condition",
                ProgramGrammar.WiredAttrs(name, ProgramGrammar.ConditionIcon, note, link1, link2, method));
            return new ConditionRef(editor, id);
        }

        /// <summary>
        /// Adds a nested logic group (vendor "Logik gruppe") — a nested <c>conditions</c> element reusing the
        /// Betingelser decoration verbatim (ENG2-B1: no distinct strings). Returns the nested group's handle.
        /// </summary>
        public ConditionsGroupRef AddConditionGroup()
        {
            ElementId id = editor.AllocateChild(Id, "conditions",
                ("name", ProgramGrammar.ConditionsName), ("icon", ProgramGrammar.ConditionsIcon),
                ("note", ProgramGrammar.ConditionsNote));
            return new ConditionsGroupRef(editor, id);
        }
    }

    /// <summary>
    /// A live handle to one action branch of a <see cref="SubProgramRef"/> (its true or false <c>actions</c>
    /// container): adds leaf <c>action</c> commands and further nested <c>program_sub</c> logic.
    /// </summary>
    public sealed class BranchRef
    {
        private readonly ProjectEditor editor;
        private readonly ElementId actionsId;

        internal BranchRef(ProjectEditor editor, ElementId actionsId)
        {
            this.editor = editor;
            this.actionsId = actionsId;
        }

        /// <summary>
        /// Adds an <c>action</c> command driving <paramref name="link1"/> per <paramref name="method"/> (optionally
        /// with a second operand <paramref name="link2"/>). Returns this for chaining.
        /// </summary>
        public BranchRef AddAction(string name, ResourceRef link1, string method, ResourceRef? link2 = null, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(link1);
            ArgumentNullException.ThrowIfNull(method);
            ProgramGrammar.RequireLiveOperands(editor, link1, link2);
            editor.AllocateChild(actionsId, "action",
                ProgramGrammar.WiredAttrs(name, ProgramGrammar.ActionIcon, note, link1, link2, method));
            return this;
        }

        /// <summary>Adds a nested <c>program_sub</c> (four-node skeleton) inside this branch; returns its handle.</summary>
        public SubProgramRef AddSubProgram() => ProgramGrammar.CreateSubProgram(editor, actionsId);

        /// <summary>
        /// Adds a <c>program_case</c> switch on <paramref name="switchVariable"/> inside this branch, eagerly
        /// allocating its default (Else) branch with it — the vendor case-insert gesture (US-031, ENG2-B2). Returns
        /// a handle for adding case values and reaching the default branch.
        /// </summary>
        public CaseRef AddCase(string name, ResourceRef switchVariable, string? note = null) =>
            ProgramGrammar.CreateCase(editor, actionsId, name, switchVariable, note);
    }

    /// <summary>
    /// A live handle to a <c>program_case</c> switch authored via <see cref="ProgramBuilder.AddCase"/> or
    /// <see cref="BranchRef.AddCase"/>. Pinned vendor semantics (ENG2-B2): the <c>program_case</c> and its default
    /// (Else) <c>actions</c> container allocate together at case-insert, but the default container serializes
    /// <b>last</b> — each added case value (a <c>case_action</c> wrapping its embedded literal operand as first
    /// child) inserts before it. Switch-type eligibility is deliberately open-world: no tag guard is applied (the
    /// validators flag genuinely broken wiring later).
    /// </summary>
    public sealed class CaseRef
    {
        private readonly ProjectEditor editor;
        private readonly ElementId defaultActionsId;

        internal CaseRef(ProjectEditor editor, ElementId caseId, ElementId defaultActionsId)
        {
            this.editor = editor;
            Id = caseId;
            this.defaultActionsId = defaultActionsId;
        }

        /// <summary>The <c>program_case</c> element's id.</summary>
        public ElementId Id { get; }

        /// <summary>
        /// Adds a case value branch whose criterion equals the enum <paramref name="valueName"/> of
        /// <paramref name="definition"/> — a <c>case_action</c> wrapping a bare embedded <c>resource_enum</c>
        /// operand (<c>typedef</c>/<c>inivalue</c>, the fb08 shape). Returns the branch handle for adding actions.
        /// </summary>
        public BranchRef Case(string name, EnumDefinitionRef definition, string valueName, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(valueName);
            // Fail before wiring a since-deleted enum into the operand's typedef (the AddEnumOperand guard).
            editor.Require(definition.Id);
            return Case(name, "resource_enum",
                op => op.SetAttribute("typedef", definition.Typedef)
                        .SetAttribute("inivalue", definition.InitialValue(valueName)),
                note);
        }

        /// <summary>
        /// Adds a case value branch with an explicitly-typed embedded operand (e.g. a counter criterion's bare
        /// <c>&lt;resource_counter inivalue="100"&gt;</c> — ENG2-B2): allocates the <c>case_action</c>, then the
        /// <paramref name="operandTag"/> operand as its first child, hands the operand to
        /// <paramref name="configureOperand"/>, and wires <c>case_action@value</c> to it. Returns the branch handle.
        /// </summary>
        public BranchRef Case(string name, string operandTag, Action<ElementRef> configureOperand, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(operandTag);
            ArgumentNullException.ThrowIfNull(configureOperand);
            ProjectElement kase = editor.Require(Id);
            string criterion = kase.GetAttribute("link")
                ?? throw new InvalidOperationException("This program_case carries no switch criterion (link).");
            var attrs = new List<(string, string)> { ("name", name), ("icon", ProgramGrammar.CaseActionIcon) };
            if (note is not null)
            {
                attrs.Add(("note", note));
            }
            attrs.Add(("variable", criterion));
            // The vendor serializes case values before the doc-last Else, though the Else allocated first (§18-A).
            ElementId caseActionId = editor.AllocateChildAt(Id, "case_action", IndexOfDefault(kase), attrs.ToArray());
            ElementId operandId = editor.AllocateChild(caseActionId, operandTag);
            configureOperand(new ElementRef(editor, operandId));
            editor.SetAttributeById(caseActionId, "value", operandId.ToToken());
            return new BranchRef(editor, caseActionId);
        }

        /// <summary>The default (Else) branch — present from case-insert, document-last. Returns its handle.</summary>
        public BranchRef Default() => new(editor, defaultActionsId);

        private int IndexOfDefault(ProjectElement kase)
        {
            for (int i = 0; i < kase.Children.Length; i++)
            {
                if (kase.Children[i].Id == defaultActionsId)
                {
                    return i;
                }
            }
            throw new InvalidOperationException("This program_case has lost its default (Else) branch.");
        }
    }

    /// <summary>
    /// A live handle to a <c>condition</c> authored via <see cref="SubProgramRef.AddCondition"/>, for attaching an
    /// embedded literal enum operand.
    /// </summary>
    public sealed class ConditionRef
    {
        private readonly ProjectEditor editor;
        private readonly ElementId conditionId;

        internal ConditionRef(ProjectEditor editor, ElementId conditionId)
        {
            this.editor = editor;
            this.conditionId = conditionId;
        }

        /// <summary>
        /// Embeds a literal <c>resource_enum</c> operand inside this condition (the constant "%S" of a
        /// "%P &lt;&gt; %S" enum comparison), typed by <paramref name="definition"/> and initialised to its
        /// <paramref name="valueName"/> value, then wires the condition's <c>link2</c> to point at it. Returns the
        /// operand's handle.
        /// </summary>
        public ResourceRef AddEnumOperand(string name, EnumDefinitionRef definition, string valueName)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(valueName);
            // Fail before wiring a since-deleted enum into the resource_enum's typedef — a dangling reference the
            // default save would otherwise persist (the enum peer of RequireLive on the wire path).
            editor.Require(definition.Id);
            ElementId enumId = editor.AllocateChild(conditionId, "resource_enum",
                ("name", name), ("typedef", definition.Typedef), ("inivalue", definition.InitialValue(valueName)),
                ("icon", ProgramGrammar.EnumOperandIcon));
            editor.SetAttributeById(conditionId, "link2", enumId.ToToken());
            return new ResourceRef(name, enumId);
        }
    }

    /// <summary>
    /// The fixed vendor grammar for hand-authored programs: the canonical icons, the <c>program_sub</c> skeleton's
    /// container names/notes/branch-type, and the leaf attribute shapes — factored here so
    /// <see cref="ProgramBuilder"/> and <see cref="BranchRef"/> compose sub-programs identically (D.R.Y). All strings
    /// are transcribed from the authentic oracle (project2-CustomBlock.vis); the byte-fidelity gate is V4 (step 3.7).
    /// </summary>
    internal static class ProgramGrammar
    {
        // The icons/names/branch-type shared with code-authored blocks alias FbGrammar's constants (FunctionBlocks
        // sits below Editing in the layering), so each shared vendor gesture has exactly one transcription. Only the
        // container NOTES stay local — they genuinely differ per vendor context: these were transcribed from a
        // project-embedded custom block (project2-CustomBlock.vis), FbGrammar's from the FunctionBlocks\*.ifb set.
        public const string EventIcon = FbGrammar.EventIcon;
        public const string ConditionIcon = FbGrammar.ConditionIcon;
        public const string ActionIcon = FbGrammar.ActionIcon;
        public const string SubProgramIcon = FbGrammar.SubProgramIcon;
        public const string ConditionsIcon = FbGrammar.ConditionsIcon;
        public const string ActionsIcon = FbGrammar.ActionsIcon;
        public const string EnumOperandIcon = FbGrammar.EnumOperandIcon;

        // The decoration a `.vis` program carries. Transcribed from the empty-block template
        // (BuiltInCatalog.Templates.cs) and verified against the vendor-authored programs in
        // project2-CustomBlock.vis. The container NAMES and icons are the same vendor words an authored `.ifb`
        // block uses, so they alias FbGrammar; the NOTES are deliberately NOT reused — a `.vis` program words them
        // differently ("Hændelser som starter program" vs the block's "Hændelser der udløser programmet"), and
        // sharing them would silently change what a project file says.
        public const string ProgramSimpleIcon = FbGrammar.ProgramSimpleIcon;
        public const string ProgramEventsName = FbGrammar.EventsName;
        public const string ProgramEventsIcon = FbGrammar.EventsIcon;
        public const string ProgramEventsNote = "Hændelser som starter program";
        public const string ProgramActionsName = FbGrammar.RootActionsName;
        public const string ProgramActionsNote = "Gruppering af kommandoer som udføres når hændelse er indtruffet";
        // A program's ROOT commands container is type _0x2, NOT the _0x1 a sub-program's true branch carries.
        public const string ProgramActionsType = FbGrammar.RootActionsType;

        public const string SubProgramName = FbGrammar.SubProgramName;
        public const string ConditionsName = FbGrammar.ConditionsName;
        public const string ConditionsNote = "Gruppering af betingelser til logisk test";
        public const string TrueActionsName = FbGrammar.TrueActionsName;
        public const string TrueActionsNote = "Gruppering af kommandoer som udføres når betingelser er sande";
        public const string FalseActionsName = FbGrammar.FalseActionsName;
        public const string FalseActionsNote = "Gruppering af kommandoer som udføres når betingelser er falske";
        public const string TrueBranchType = FbGrammar.TrueBranchType;
        public const string ProgramCaseIcon = FbGrammar.ProgramCaseIcon;
        public const string CaseActionIcon = FbGrammar.CaseActionIcon;
        public const string DefaultCaseName = FbGrammar.DefaultCaseName;
        public const string DefaultCaseNote = FbGrammar.DefaultCaseNote;
        public const string DefaultCaseType = FbGrammar.DefaultCaseType;

        /// <summary>Attribute set for a trigger with no resource operands (<c>event_power</c>): name, icon, optional note.</summary>
        public static (string, string)[] LeafAttrs(string name, string icon, string? note)
        {
            var attrs = new List<(string, string)> { ("name", name), ("icon", icon) };
            if (note is not null)
            {
                attrs.Add(("note", note));
            }
            return attrs.ToArray();
        }

        /// <summary>Attribute set for a resource-wired leaf (<c>event</c>/<c>condition</c>/<c>action</c>).</summary>
        public static (string, string)[] WiredAttrs(string name, string icon, string? note,
            ResourceRef link1, ResourceRef? link2, string method)
        {
            var attrs = new List<(string, string)> { ("name", name), ("icon", icon) };
            if (note is not null)
            {
                attrs.Add(("note", note));
            }
            attrs.Add(("link1", RequireId(link1, nameof(link1))));
            if (link2 is not null)
            {
                attrs.Add(("link2", RequireId(link2, nameof(link2))));
            }
            attrs.Add(("method", method));
            return attrs.ToArray();
        }

        /// <summary>
        /// Allocates a new empty <c>program_simple</c> under a block's <c>programs</c> container, together with the
        /// two containers a program must own: <c>events</c> and the root <c>actions</c> (uxparity2 W4). The
        /// decoration is the vendor's, transcribed from the empty-block template and cross-checked against the
        /// two vendor-authored programs in <c>project2-CustomBlock.vis</c>, so an added program and an authored one
        /// serialize alike. Returns the new program's id.
        /// </summary>
        public static ElementId CreateProgram(ProjectEditor editor, ElementId programsId, string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId programId = editor.AllocateChild(programsId, "program_simple",
                ("name", name), ("icon", ProgramSimpleIcon));
            editor.AllocateChild(programId, "events",
                ("name", ProgramEventsName), ("icon", ProgramEventsIcon), ("note", ProgramEventsNote));
            editor.AllocateChild(programId, "actions",
                ("name", ProgramActionsName), ("icon", ActionsIcon), ("note", ProgramActionsNote),
                ("type", ProgramActionsType));
            return programId;
        }

        /// <summary>
        /// Allocates a <c>program_sub</c> and its three children (conditions, true actions, false actions) as four
        /// contiguous ids in document order (R1) under <paramref name="parentActionsId"/>, returning the sub handle.
        /// </summary>
        public static SubProgramRef CreateSubProgram(ProjectEditor editor, ElementId parentActionsId)
        {
            ElementId subId = editor.AllocateChild(parentActionsId, "program_sub",
                ("name", SubProgramName), ("icon", SubProgramIcon));
            ElementId conditionsId = editor.AllocateChild(subId, "conditions",
                ("name", ConditionsName), ("icon", ConditionsIcon), ("note", ConditionsNote));
            ElementId trueActionsId = editor.AllocateChild(subId, "actions",
                ("name", TrueActionsName), ("icon", ActionsIcon), ("note", TrueActionsNote), ("type", TrueBranchType));
            ElementId falseActionsId = editor.AllocateChild(subId, "actions",
                ("name", FalseActionsName), ("icon", ActionsIcon), ("note", FalseActionsNote));
            return new SubProgramRef(editor, subId, conditionsId, trueActionsId, falseActionsId);
        }

        /// <summary>
        /// Allocates a <c>program_case</c> and — together with it, per the ENG2-B2 census — its default (Else)
        /// <c>actions</c> container under <paramref name="parentActionsId"/>, returning the case handle. The
        /// criterion is always written to <c>program_case@link</c> (Fb-builder precedent, though the DTD says
        /// #IMPLIED); the Else carries the fixed vendor decoration and branch type.
        /// </summary>
        public static CaseRef CreateCase(ProjectEditor editor, ElementId parentActionsId, string name,
            ResourceRef switchVariable, string? note)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(switchVariable);
            string criterion = editor.RequireLive(switchVariable).Id!.Value.ToToken();
            var attrs = new List<(string, string)> { ("name", name), ("icon", ProgramCaseIcon) };
            if (note is not null)
            {
                attrs.Add(("note", note));
            }
            attrs.Add(("link", criterion));
            ElementId caseId = editor.AllocateChild(parentActionsId, "program_case", attrs.ToArray());
            ElementId defaultActionsId = editor.AllocateChild(caseId, "actions",
                ("name", DefaultCaseName), ("icon", ActionsIcon), ("note", DefaultCaseNote), ("type", DefaultCaseType));
            return new CaseRef(editor, caseId, defaultActionsId);
        }

        private static string RequireId(ResourceRef resource, string paramName) =>
            (resource.Id ?? throw new ArgumentException(
                $"Resource '{resource.Name}' has no allocated id; it cannot be wired into a program.", paramName)).ToToken();

        /// <summary>
        /// Requires the wired operands to still exist in the session — wiring a stale handle would persist a
        /// program leaf whose <c>link1</c>/<c>link2</c> points at nothing.
        /// </summary>
        public static void RequireLiveOperands(ProjectEditor editor, ResourceRef link1, ResourceRef? link2)
        {
            editor.RequireLive(link1);
            if (link2 is not null)
            {
                editor.RequireLive(link2);
            }
        }
    }
}
