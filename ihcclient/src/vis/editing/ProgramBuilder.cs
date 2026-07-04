#nullable enable
using System;
using System.Collections.Generic;

using Ihc.Vis.Model;
namespace Ihc.Vis.Editing
{
    /// <summary>
    /// Nested fluent builder for authoring a custom program directly into a function block's <c>program_simple</c> —
    /// the "fill an empty block's logic by hand" path (project2's "Custom blok", counters 204–247), rather than
    /// receiving a program whole from a catalog deep-copy. Adds <c>event_power</c>/<c>event</c> triggers to the
    /// program's <c>events</c> container and nested <c>program_sub</c> logic (a conditions list + true/false action
    /// branches) to its root <c>actions</c> container. Leaf triggers/conditions/actions reference resources by live
    /// <see cref="ResourceRef"/> — the same handles <see cref="ProjectEditor.Link"/> consumes — and carry an opaque
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
        /// Adds a nested <c>program_sub</c> ("Under program") to the program's root actions, auto-creating its three
        /// children — a <c>conditions</c> container and the true/false <c>actions</c> branches — as four contiguous
        /// ids (the vendor "add sub-program" skeleton). Returns a handle for authoring its conditions and branches.
        /// </summary>
        public SubProgramRef AddSubProgram() => ProgramGrammar.CreateSubProgram(editor, actionsId);
    }

    /// <summary>
    /// A live handle to a nested <c>program_sub</c> authored via <see cref="ProgramBuilder.AddSubProgram"/> (or
    /// <see cref="BranchRef.AddSubProgram"/>): exposes its conditions list and its two action branches
    /// (<see cref="WhenTrue"/>/<see cref="WhenFalse"/>) for further authoring.
    /// </summary>
    public sealed class SubProgramRef
    {
        private readonly ProjectEditor editor;
        private readonly ElementId conditionsId;

        internal SubProgramRef(ProjectEditor editor, ElementId subId, ElementId conditionsId,
            ElementId trueActionsId, ElementId falseActionsId)
        {
            this.editor = editor;
            Id = subId;
            this.conditionsId = conditionsId;
            WhenTrue = new BranchRef(editor, trueActionsId);
            WhenFalse = new BranchRef(editor, falseActionsId);
        }

        internal ElementId Id { get; }

        /// <summary>The true-branch ("Kommandoer ved betingelser sande") action container.</summary>
        public BranchRef WhenTrue { get; }

        /// <summary>The false-branch ("Kommandoer ved betingelser falske") action container.</summary>
        public BranchRef WhenFalse { get; }

        /// <summary>
        /// Adds a <c>condition</c> to this sub-program's conditions list — a logical test on <paramref name="link1"/>
        /// per <paramref name="method"/>, optionally against <paramref name="link2"/>. Returns a handle for adding an
        /// embedded literal enum operand (see <see cref="ConditionRef.AddEnumOperand"/>).
        /// </summary>
        public ConditionRef AddCondition(string name, ResourceRef link1, string method, ResourceRef? link2 = null, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(link1);
            ArgumentNullException.ThrowIfNull(method);
            ProgramGrammar.RequireLiveOperands(editor, link1, link2);
            ElementId id = editor.AllocateChild(conditionsId, "condition",
                ProgramGrammar.WiredAttrs(name, ProgramGrammar.ConditionIcon, note, link1, link2, method));
            return new ConditionRef(editor, id);
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
        public const string EventIcon = "_0xc";
        public const string ConditionIcon = "_0x1a";
        public const string ActionIcon = "_0x9";
        public const string SubProgramIcon = "_0x7";
        public const string ConditionsIcon = "_0x16";
        public const string ActionsIcon = "_0x8";
        public const string EnumOperandIcon = "_0x22";

        public const string SubProgramName = "Under program";
        public const string ConditionsName = "Betingelser";
        public const string ConditionsNote = "Gruppering af betingelser til logisk test";
        public const string TrueActionsName = "Kommandoer ved betingelser sande";
        public const string TrueActionsNote = "Gruppering af kommandoer som udføres når betingelser er sande";
        public const string FalseActionsName = "Kommandoer ved betingelser falske";
        public const string FalseActionsNote = "Gruppering af kommandoer som udføres når betingelser er falske";
        public const string TrueBranchType = "_0x1";

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
