#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Schema;
using TypeCode = Ihc.Vis.Schema.TypeCode;
namespace Ihc.Vis.FunctionBlocks
{
    /// <summary>
    /// Authors the program graph of a function-block definition — the definition-layer parallel of
    /// <see cref="Ihc.Vis.Editing.ProgramBuilder"/>. Adds <c>event_power</c>/<c>event</c> triggers to the block's
    /// <c>program_simple</c> and nested <c>program_sub</c> (a conditions list plus true/false action branches) and
    /// <c>program_case</c> (a switch on a variable with per-value branches) logic to its root <c>actions</c>. Leaf
    /// triggers/conditions/actions reference resources by <see cref="FbResourceHandle"/> (in place of the edit-session
    /// <c>ResourceRef</c>) and carry an opaque <c>method</c> operation token.
    /// </summary>
    /// <remarks>
    /// Structural decorations (container names/notes/icons and branch <c>type</c>) default to the fixed vendor grammar
    /// (<see cref="FbGrammar"/>) but can be overridden per node — a code-authored recreation of a stock <c>.ifb</c>
    /// reproduces that file's exact notes and sub-program names, which vary per block. The builder accumulates an intent
    /// tree and materializes it into a <c>program_simple</c> subtree at
    /// <see cref="FunctionBlockDefinitionBuilder.Build"/>, allocating structural ids off the block's shared allocator;
    /// the leaf IDREFs already carry each resource's placeholder id, so nothing needs deferred resolution.
    /// </remarks>
    public sealed class FbProgramBuilder
    {
        private readonly IdAllocator ids;
        private readonly string name;
        private readonly List<PlannedLeaf> events = new();
        private readonly List<IPlannedNode> rootActions = new();
        private string? programNote;
        private string? eventsName;
        private string? actionsName;
        private string? eventsNote;
        private string? actionsNote;

        internal FbProgramBuilder(IdAllocator ids, string name)
        {
            this.ids = ids;
            this.name = name;
        }

        internal bool HasEvents => events.Count > 0;

        /// <summary>Overrides the program's <c>events</c> container note (defaults to
        /// <see cref="FbGrammar.EventsNote"/>). Returns this for chaining.</summary>
        public FbProgramBuilder EventsNote(string note)
        {
            eventsNote = note;
            return this;
        }

        /// <summary>Overrides the program's root <c>actions</c> container note (defaults to
        /// <see cref="FbGrammar.RootActionsNote"/>). Returns this for chaining.</summary>
        public FbProgramBuilder ActionsNote(string note)
        {
            actionsNote = note;
            return this;
        }

        /// <summary>Sets the <c>program_simple</c>'s own <c>note</c> (a stock block occasionally documents a program;
        /// the builder omits it by default). Returns this for chaining.</summary>
        public FbProgramBuilder Note(string note)
        {
            programNote = note;
            return this;
        }

        /// <summary>Overrides the program's <c>events</c> container display name (defaults to
        /// <see cref="FbGrammar.EventsName"/> — a stock block may use a different language/label). Returns this.</summary>
        public FbProgramBuilder EventsName(string name)
        {
            eventsName = name;
            return this;
        }

        /// <summary>Overrides the program's root <c>actions</c> container display name (defaults to
        /// <see cref="FbGrammar.RootActionsName"/>). Returns this for chaining.</summary>
        public FbProgramBuilder ActionsName(string name)
        {
            actionsName = name;
            return this;
        }

        /// <summary>Adds a power-up trigger (<c>event_power</c>, e.g. "Powerup") to the program's events. Returns this.</summary>
        public FbProgramBuilder AddPowerEvent(string name, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            events.Add(new PlannedLeaf("event_power", FbGrammar.EventIcon, name, note, null, null, null));
            return this;
        }

        /// <summary>Adds a resource-triggered <c>event</c> — fires when <paramref name="link1"/> changes per the
        /// <paramref name="method"/> operation, optionally comparing against a second operand <paramref name="link2"/>.
        /// Returns this for chaining.</summary>
        public FbProgramBuilder AddEvent(string name, FbResourceHandle link1, string method,
            FbResourceHandle? link2 = null, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(link1);
            ArgumentNullException.ThrowIfNull(method);
            events.Add(new PlannedLeaf("event", FbGrammar.EventIcon, name, note,
                link1.PlaceholderId, link2?.PlaceholderId, method));
            return this;
        }

        /// <summary>Adds an <c>event</c> whose second operand is an embedded literal constant (a <c>%S</c> value of the
        /// operand's type materialized inline and wired as <c>link2</c>) rather than a reference to another resource.
        /// Returns this for chaining.</summary>
        public FbProgramBuilder AddEvent(string name, FbResourceHandle link1, string method,
            FbOperand operand, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(operand);
            events.Add(FbOperand.Leaf("event", FbGrammar.EventIcon, name, note, link1, method, operand, ids));
            return this;
        }

        /// <summary>Adds a leaf <c>action</c> command directly to the program's root actions (a top-level command that
        /// runs unconditionally on any event, alongside any <c>program_sub</c> logic). Returns this for chaining.</summary>
        public FbProgramBuilder AddAction(string name, FbResourceHandle link1, string method,
            FbResourceHandle? link2 = null, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(link1);
            ArgumentNullException.ThrowIfNull(method);
            rootActions.Add(new PlannedLeaf("action", FbGrammar.ActionIcon, name, note,
                link1.PlaceholderId, link2?.PlaceholderId, method));
            return this;
        }

        /// <summary>Adds a root <c>action</c> whose second operand is an embedded literal constant (wired as
        /// <c>link2</c>). Returns this for chaining.</summary>
        public FbProgramBuilder AddAction(string name, FbResourceHandle link1, string method,
            FbOperand operand, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(operand);
            rootActions.Add(FbOperand.Leaf("action", FbGrammar.ActionIcon, name, note, link1, method, operand, ids));
            return this;
        }

        /// <summary>Adds a nested <c>program_sub</c> to the program's root actions (auto-creating its conditions list
        /// and true/false action branches as the vendor four-node skeleton), named <paramref name="name"/>. Returns its
        /// handle.</summary>
        public FbSubProgramRef AddSubProgram(string name = FbGrammar.SubProgramName)
        {
            var sub = new PlannedSub { Name = name };
            rootActions.Add(sub);
            return new FbSubProgramRef(ids, sub);
        }

        /// <summary>Adds a <c>program_case</c> to the program's root actions — a switch on
        /// <paramref name="switchVariable"/> whose per-value <see cref="FbCaseRef.Case(string, FbEnumDefRef, string, string)"/>
        /// branches and trailing <see cref="FbCaseRef.Default"/> branch are added on the returned handle. Returns its
        /// handle.</summary>
        public FbCaseRef AddCase(string name, FbResourceHandle switchVariable, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(switchVariable);
            var plan = new PlannedCase(name, note, switchVariable.PlaceholderId);
            rootActions.Add(plan);
            return new FbCaseRef(ids, plan);
        }

        // The root actions viewed as an appendable branch (the decor-less FbBranchRef over the same list): lets the
        // catalog decompiler target root actions and program_sub/case branches uniformly, typed — appends through
        // the view land in rootActions itself, built exactly as the AddAction/AddSubProgram/AddCase methods above.
        internal FbBranchRef RootBranch => new(ids, rootActions);

        internal ProjectElement Materialize()
        {
            ElementId programId = ids.Allocate(TypeCode.RequireForTag("program_simple"));
            ProjectElement eventsContainer = FbGrammar.Container(ids, "events",
                eventsName ?? FbGrammar.EventsName, FbGrammar.EventsIcon, eventsNote ?? FbGrammar.EventsNote,
                events.Select(MaterializeLeaf).ToArray());
            ProjectElement actionsContainer = FbGrammar.Node("actions",
                ids.Allocate(TypeCode.RequireForTag("actions")),
                new[]
                {
                    ("name", actionsName ?? FbGrammar.RootActionsName), ("icon", FbGrammar.ActionsIcon),
                    ("note", actionsNote ?? FbGrammar.RootActionsNote), ("type", FbGrammar.RootActionsType),
                },
                rootActions.Select(MaterializeNode).ToArray());
            var programAttrs = new List<(string, string)>
            {
                ("name", name), ("icon", FbGrammar.ProgramSimpleIcon),
            };
            if (programNote is { } note)
            {
                programAttrs.Add(("note", note));
            }
            return FbGrammar.Node("program_simple", programId, programAttrs,
                new[] { eventsContainer, actionsContainer });
        }

        // ---- planned-tree materialization (walked centrally so the refs stay pure accumulators) ----

        private ProjectElement MaterializeNode(IPlannedNode node) => node switch
        {
            PlannedSub sub => MaterializeSub(sub),
            PlannedCase caseNode => MaterializeCase(caseNode),
            PlannedLeaf leaf => MaterializeLeaf(leaf),
            _ => throw new InvalidOperationException($"Unknown planned node kind: {node.GetType()}"),
        };

        private ProjectElement MaterializeSub(PlannedSub sub)
        {
            ElementId subId = ids.Allocate(TypeCode.RequireForTag("program_sub"));
            ProjectElement conditions = MaterializeConditionsGroup(sub.Conditions);
            ProjectElement trueActions = MaterializeBranch(sub.True);
            ProjectElement falseActions = MaterializeBranch(sub.False);
            var subAttrs = FbGrammar.LeafAttrs(sub.Name, FbGrammar.SubProgramIcon, sub.Note);
            return FbGrammar.Node("program_sub", subId, subAttrs, new[] { conditions, trueActions, falseActions });
        }

        // A conditions group renders as a <conditions> element (fixed name/icon, per-group note/type) holding condition
        // leaves and — recursively — nested conditions sub-groups (the vendor's AND/OR boolean tree).
        private ProjectElement MaterializeConditionsGroup(PlannedConditionsGroup group)
        {
            var attrs = new List<(string, string)>
            {
                ("name", group.Name ?? FbGrammar.ConditionsName), ("icon", FbGrammar.ConditionsIcon),
                ("note", group.Note ?? FbGrammar.ConditionsNote),
            };
            if (group.Type is { } type)
            {
                attrs.Add(("type", type));
            }
            IEnumerable<ProjectElement> children = group.Children.Select(child => child switch
            {
                PlannedConditionsGroup nested => MaterializeConditionsGroup(nested),
                PlannedLeaf leaf => MaterializeLeaf(leaf),
                _ => throw new InvalidOperationException($"Unknown condition node kind: {child.GetType()}"),
            });
            return FbGrammar.Node("conditions", ids.Allocate(TypeCode.RequireForTag("conditions")), attrs,
                children.ToArray());
        }

        private ProjectElement MaterializeCase(PlannedCase plan)
        {
            ElementId caseId = ids.Allocate(TypeCode.RequireForTag("program_case"));
            var children = new List<ProjectElement>();
            children.AddRange(plan.Cases.Select(MaterializeCaseAction));
            children.Add(MaterializeBranch(plan.Default));
            var attrs = FbGrammar.LeafAttrs(plan.Name, FbGrammar.ProgramCaseIcon, plan.Note);
            attrs.Add(("link", plan.SwitchVariable.ToToken()));   // the vendor writes note before link on program_case
            return FbGrammar.Node("program_case", caseId, attrs, children);
        }

        private ProjectElement MaterializeCaseAction(PlannedCaseAction ca)
        {
            ElementId id = ids.Allocate(TypeCode.RequireForTag("case_action"));
            var attrs = FbGrammar.LeafAttrs(ca.Name, FbGrammar.CaseActionIcon, ca.Note);
            attrs.Add(("variable", ca.Variable.ToToken()));   // the vendor writes note before variable/value on case_action
            attrs.Add(("value", ca.Operand.Id.ToToken()));
            var children = new List<ProjectElement>
            {
                FbGrammar.Leaf(ca.Operand.Tag, ca.Operand.Id, ca.Operand.Attrs),
            };
            children.AddRange(ca.Children.Select(MaterializeNode));
            return FbGrammar.Node("case_action", id, attrs, children);
        }

        private ProjectElement MaterializeBranch(PlannedBranch branch)
        {
            var attrs = new List<(string, string)>
            {
                ("name", branch.Name), ("icon", FbGrammar.ActionsIcon), ("note", branch.EffectiveNote),
            };
            if (branch.Type is { } type)
            {
                attrs.Add(("type", type));
            }
            return FbGrammar.Node("actions", ids.Allocate(TypeCode.RequireForTag("actions")), attrs,
                branch.Children.Select(MaterializeNode).ToArray());
        }

        private ProjectElement MaterializeLeaf(PlannedLeaf leaf)
        {
            var attrs = FbGrammar.LeafAttrs(leaf.Name, leaf.Icon, leaf.Note);
            if (leaf.Link1 is { } link1)
            {
                attrs.Add(("link1", link1.ToToken()));
            }
            if (leaf.Link2 is { } link2)
            {
                attrs.Add(("link2", link2.ToToken()));
            }
            if (leaf.Method is { } method)
            {
                attrs.Add(("method", method));
            }
            ElementId id = ids.Allocate(TypeCode.RequireForTag(leaf.Tag));
            IEnumerable<ProjectElement> children = leaf.Operand is { } operand
                ? new[] { FbGrammar.Leaf(operand.Tag, operand.Id, operand.Attrs) }
                : Array.Empty<ProjectElement>();
            return FbGrammar.Node(leaf.Tag, id, attrs, children);
        }
    }

    /// <summary>
    /// A handle to a nested <c>program_sub</c> authored via <see cref="FbProgramBuilder.AddSubProgram"/> (or
    /// <see cref="FbBranchRef.AddSubProgram"/>): exposes its conditions list, its two action branches
    /// (<see cref="WhenTrue"/>/<see cref="WhenFalse"/>) and its per-node note overrides.
    /// </summary>
    public sealed class FbSubProgramRef
    {
        private readonly IdAllocator ids;
        private readonly PlannedSub sub;

        internal FbSubProgramRef(IdAllocator ids, PlannedSub sub)
        {
            this.ids = ids;
            this.sub = sub;
            WhenTrue = new FbBranchRef(ids, sub.True);
            WhenFalse = new FbBranchRef(ids, sub.False);
            Conditions = new FbConditionsGroupRef(ids, sub.Conditions);
        }

        /// <summary>The true-branch ("Kommandoer ved betingelser sande") action container.</summary>
        public FbBranchRef WhenTrue { get; }

        /// <summary>The false-branch ("Kommandoer ved betingelser falske") action container.</summary>
        public FbBranchRef WhenFalse { get; }

        /// <summary>This sub-program's top-level <c>conditions</c> group — add conditions and nested condition groups on
        /// it directly (or via the convenience <see cref="AddCondition(string, FbResourceHandle, string, FbResourceHandle, string)"/>
        /// / <see cref="OrConditions"/> that delegate to it).</summary>
        public FbConditionsGroupRef Conditions { get; }

        /// <summary>Sets the <c>program_sub</c>'s own note (a stock block occasionally documents a sub-program).
        /// Returns this for chaining.</summary>
        public FbSubProgramRef Note(string note)
        {
            sub.Note = note;
            return this;
        }

        /// <summary>Overrides the top-level <c>conditions</c> group note (defaults to
        /// <see cref="FbGrammar.ConditionsNote"/>). Returns this for chaining.</summary>
        public FbSubProgramRef ConditionsNote(string note)
        {
            Conditions.Note(note);
            return this;
        }

        /// <summary>Adds a <c>condition</c> to this sub-program's top-level conditions group — a logical test on
        /// <paramref name="link1"/> per <paramref name="method"/>, optionally against <paramref name="link2"/>.
        /// Returns its handle (for attaching an embedded literal enum operand).</summary>
        public FbConditionRef AddCondition(string name, FbResourceHandle link1, string method,
            FbResourceHandle? link2 = null, string? note = null) =>
            Conditions.AddCondition(name, link1, method, link2, note);

        /// <summary>Adds a <c>condition</c> whose second operand is an embedded literal constant (the <c>%S</c> of a
        /// "%P &lt;op&gt; %S" test, materialized inline and wired as <c>link2</c>). Returns its handle.</summary>
        public FbConditionRef AddCondition(string name, FbResourceHandle link1, string method,
            FbOperand operand, string? note = null) =>
            Conditions.AddCondition(name, link1, method, operand, note);

        /// <summary>Marks this sub-program's top-level conditions group as an OR grouping (<c>conditions type="or"</c> —
        /// the default is an implicit AND). Returns this for chaining.</summary>
        public FbSubProgramRef OrConditions()
        {
            Conditions.OrConditions();
            return this;
        }
    }

    /// <summary>
    /// A handle to a <c>conditions</c> group — a boolean AND (default) or OR (<see cref="OrConditions"/>) grouping of
    /// <c>condition</c> leaves and, recursively, nested condition groups (<see cref="AddConditionGroup"/>). A
    /// sub-program's <see cref="FbSubProgramRef.Conditions"/> is its top-level group.
    /// </summary>
    public sealed class FbConditionsGroupRef
    {
        private readonly IdAllocator ids;
        private readonly PlannedConditionsGroup group;

        internal FbConditionsGroupRef(IdAllocator ids, PlannedConditionsGroup group)
        {
            this.ids = ids;
            this.group = group;
        }

        /// <summary>Overrides this group's display <c>name</c> (defaults to <see cref="FbGrammar.ConditionsName"/>).
        /// Returns this for chaining.</summary>
        public FbConditionsGroupRef Name(string name)
        {
            group.Name = name;
            return this;
        }

        /// <summary>Overrides this group's <c>note</c> (defaults to <see cref="FbGrammar.ConditionsNote"/>).
        /// Returns this for chaining.</summary>
        public FbConditionsGroupRef Note(string note)
        {
            group.Note = note;
            return this;
        }

        /// <summary>Marks this group as an OR grouping (<c>type="or"</c>). Returns this for chaining.</summary>
        public FbConditionsGroupRef OrConditions()
        {
            group.Type = "or";
            return this;
        }

        /// <summary>Adds a <c>condition</c> to this group — a logical test on <paramref name="link1"/> per
        /// <paramref name="method"/>, optionally against <paramref name="link2"/>. Returns its handle.</summary>
        public FbConditionRef AddCondition(string name, FbResourceHandle link1, string method,
            FbResourceHandle? link2 = null, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(link1);
            ArgumentNullException.ThrowIfNull(method);
            var leaf = new PlannedLeaf("condition", FbGrammar.ConditionIcon, name, note,
                link1.PlaceholderId, link2?.PlaceholderId, method);
            group.Children.Add(leaf);
            return new FbConditionRef(ids, leaf);
        }

        /// <summary>Adds a <c>condition</c> whose second operand is an embedded literal constant (wired as
        /// <c>link2</c>). Returns its handle.</summary>
        public FbConditionRef AddCondition(string name, FbResourceHandle link1, string method,
            FbOperand operand, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(link1);
            ArgumentNullException.ThrowIfNull(method);
            ArgumentNullException.ThrowIfNull(operand);
            PlannedLeaf leaf = FbOperand.Leaf("condition", FbGrammar.ConditionIcon, name, note, link1, method, operand, ids);
            group.Children.Add(leaf);
            return new FbConditionRef(ids, leaf);
        }

        /// <summary>Adds a nested <c>conditions</c> sub-group (a bracketed sub-expression of the boolean tree); returns
        /// its handle to populate.</summary>
        public FbConditionsGroupRef AddConditionGroup()
        {
            var nested = new PlannedConditionsGroup();
            group.Children.Add(nested);
            return new FbConditionsGroupRef(ids, nested);
        }
    }

    /// <summary>
    /// A handle to one action branch — a program_sub's true/false <c>actions</c> container, or a case branch: adds leaf
    /// <c>action</c> commands, nested <c>program_sub</c> logic and <c>program_case</c> switches, and (for a program_sub
    /// branch) overrides the container name/note.
    /// </summary>
    public sealed class FbBranchRef
    {
        private readonly IdAllocator ids;
        private readonly PlannedBranch? decor;
        private readonly List<IPlannedNode> children;

        internal FbBranchRef(IdAllocator ids, PlannedBranch branch)
        {
            this.ids = ids;
            decor = branch;
            children = branch.Children;
        }

        // A case-action body has no actions-container decoration of its own (the case_action carries name/note); it is
        // just an appendable node list, so decor is null and Name/Note are inert.
        internal FbBranchRef(IdAllocator ids, List<IPlannedNode> children)
        {
            this.ids = ids;
            decor = null;
            this.children = children;
        }

        /// <summary>Overrides this branch's <c>actions</c> container name (a stock block occasionally renames a branch).
        /// No-op on a case-action body. Returns this for chaining.</summary>
        public FbBranchRef Name(string name)
        {
            if (decor is not null)
            {
                decor.Name = name;
            }
            return this;
        }

        /// <summary>Overrides this branch's <c>actions</c> container note. No-op on a case-action body. Returns this.</summary>
        public FbBranchRef Note(string note)
        {
            if (decor is not null)
            {
                decor.Note = note;
            }
            return this;
        }

        /// <summary>Adds an <c>action</c> command driving <paramref name="link1"/> per <paramref name="method"/>
        /// (optionally with a second operand <paramref name="link2"/>). Returns this for chaining.</summary>
        public FbBranchRef AddAction(string name, FbResourceHandle link1, string method,
            FbResourceHandle? link2 = null, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(link1);
            ArgumentNullException.ThrowIfNull(method);
            children.Add(new PlannedLeaf("action", FbGrammar.ActionIcon, name, note,
                link1.PlaceholderId, link2?.PlaceholderId, method));
            return this;
        }

        /// <summary>Adds an <c>action</c> whose second operand is an embedded literal constant (wired as <c>link2</c>).
        /// Returns this for chaining.</summary>
        public FbBranchRef AddAction(string name, FbResourceHandle link1, string method,
            FbOperand operand, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(operand);
            children.Add(FbOperand.Leaf("action", FbGrammar.ActionIcon, name, note, link1, method, operand, ids));
            return this;
        }

        /// <summary>Adds a nested <c>program_sub</c> (four-node skeleton) inside this branch; returns its handle.</summary>
        public FbSubProgramRef AddSubProgram(string name = FbGrammar.SubProgramName)
        {
            var sub = new PlannedSub { Name = name };
            children.Add(sub);
            return new FbSubProgramRef(ids, sub);
        }

        /// <summary>Adds a nested <c>program_case</c> switch inside this branch; returns its handle.</summary>
        public FbCaseRef AddCase(string name, FbResourceHandle switchVariable, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(switchVariable);
            var plan = new PlannedCase(name, note, switchVariable.PlaceholderId);
            children.Add(plan);
            return new FbCaseRef(ids, plan);
        }
    }

    /// <summary>
    /// A handle to a <c>program_case</c> switch authored via <see cref="FbProgramBuilder.AddCase"/> /
    /// <see cref="FbBranchRef.AddCase"/>: adds per-value <see cref="Case(string, FbEnumDefRef, string, string)"/>
    /// branches (each embedding a bare <c>resource_enum</c> match operand) and the trailing <see cref="Default"/>
    /// branch. The switch variable was fixed when the case was added.
    /// </summary>
    public sealed class FbCaseRef
    {
        private readonly IdAllocator ids;
        private readonly PlannedCase plan;

        internal FbCaseRef(IdAllocator ids, PlannedCase plan)
        {
            this.ids = ids;
            this.plan = plan;
        }

        /// <summary>Adds a case branch matched by the <paramref name="definition"/> value named
        /// <paramref name="valueName"/> (a bare <c>resource_enum</c> operand carrying its typedef/inivalue tokens is
        /// embedded and wired as the case value); returns the branch to add its commands to.</summary>
        public FbBranchRef Case(string name, FbEnumDefRef definition, string valueName, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(definition);
            return Case(name, FbOperand.EnumRaw(definition.Typedef, definition.InitialValue(valueName)), note);
        }

        /// <summary>Adds a case branch matched by an embedded literal <paramref name="operand"/> (an enum value or a
        /// value-type constant, wired as the case value); returns the branch to add its commands to.</summary>
        public FbBranchRef Case(string name, FbOperand operand, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(operand);
            PlannedResource materialized = operand.Materialize(ids);
            var caseAction = new PlannedCaseAction(name, note, plan.SwitchVariable, materialized);
            plan.Cases.Add(caseAction);
            return new FbBranchRef(ids, caseAction.Children);
        }

        /// <summary>The trailing default branch ("Udføres når ingen case er lig case værdien"), run when no case
        /// matches; returns it to add its commands to.</summary>
        public FbBranchRef Default() => new(ids, plan.Default);
    }

    /// <summary>
    /// A handle to a <c>condition</c> authored via
    /// <see cref="FbSubProgramRef.AddCondition(string, FbResourceHandle, string, FbResourceHandle, string)"/>, for
    /// attaching an embedded literal enum operand.
    /// </summary>
    public sealed class FbConditionRef
    {
        private readonly IdAllocator ids;
        private readonly PlannedLeaf leaf;

        internal FbConditionRef(IdAllocator ids, PlannedLeaf leaf)
        {
            this.ids = ids;
            this.leaf = leaf;
        }

        /// <summary>Embeds a literal <c>resource_enum</c> operand inside this condition (the constant of a
        /// "%P &lt;&gt; %S" comparison), typed by a <see cref="FbEnumDefRef"/> handle and initialised to its
        /// <paramref name="valueName"/> value (tokens resolved internally), then wires the condition's <c>link2</c>
        /// at it — the GUI-friendly form. Returns the operand's handle.</summary>
        public FbResourceHandle AddEnumOperand(string name, FbEnumDefRef definition, string valueName)
        {
            ArgumentNullException.ThrowIfNull(definition);
            return AddEnumOperand(name, definition.Typedef, definition.InitialValue(valueName));
        }

        /// <summary>Embeds a literal <c>resource_enum</c> operand typed by raw <paramref name="typedefToken"/> /
        /// <paramref name="inivalueToken"/> IDREF tokens directly (the raw escape hatch), then wires the condition's
        /// <c>link2</c> at it. Returns the operand's handle.</summary>
        public FbResourceHandle AddEnumOperand(string name, string typedefToken, string inivalueToken)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId id = ids.Allocate(TypeCode.RequireForTag("resource_enum"));
            var attrs = new (string, string)[]
            {
                ("name", name), ("typedef", typedefToken), ("inivalue", inivalueToken), ("icon", FbGrammar.EnumOperandIcon),
            };
            leaf.Operand = new PlannedResource("resource_enum", id, attrs);
            leaf.Link2 = id;
            return new FbResourceHandle(name, id);
        }
    }

    /// <summary>
    /// Describes an embedded literal operand — the <c>%S</c> constant of a "%P &lt;op&gt; %S" event/action/condition or
    /// the match value of a <c>program_case</c> branch. It is materialized as a child resource element of the leaf, with
    /// the leaf's <c>link2</c> (or the case's <c>value</c>) wired to it. Either an enum value
    /// (<see cref="Enum(FbEnumDefRef, string, string, string)"/>) or a value-type constant
    /// (<see cref="Literal(string, string, Action{FbResourceDefBuilder})"/>).
    /// </summary>
    public sealed class FbOperand
    {
        private readonly string tag;
        private readonly string? name;
        private readonly string? icon;
        private readonly FbEnumDefRef? enumDefinition;
        private readonly string? valueName;
        private readonly string? typedefToken;
        private readonly string? inivalueToken;
        private readonly Action<FbResourceDefBuilder>? configure;

        private FbOperand(string tag, string? name, string? icon, FbEnumDefRef? enumDefinition, string? valueName,
            string? typedefToken, string? inivalueToken, Action<FbResourceDefBuilder>? configure)
        {
            this.tag = tag;
            this.name = name;
            this.icon = icon;
            this.enumDefinition = enumDefinition;
            this.valueName = valueName;
            this.typedefToken = typedefToken;
            this.inivalueToken = inivalueToken;
            this.configure = configure;
        }

        /// <summary>An embedded value-type constant of type <paramref name="tag"/> (e.g. <c>resource_time</c>,
        /// <c>resource_integer</c>) with an optional display <paramref name="name"/> and its value set via
        /// <paramref name="configure"/>. Unlike a container resource, no per-type defaults are applied — the operand
        /// carries exactly the attributes configured.</summary>
        public static FbOperand Literal(string tag, string? name = null, Action<FbResourceDefBuilder>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(tag);
            return new FbOperand(tag, name, null, null, null, null, null, configure);
        }

        /// <summary>An embedded <c>resource_enum</c> operand wired to <paramref name="definition"/>'s value named
        /// <paramref name="valueName"/>, with the operand's own <paramref name="name"/>/<paramref name="icon"/>
        /// (defaults match a condition operand; pass <c>null</c> for the bare form a case operand uses).</summary>
        public static FbOperand Enum(FbEnumDefRef definition, string valueName,
            string? name = "Enumerator", string? icon = FbGrammar.EnumOperandIcon)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(valueName);
            return new FbOperand("resource_enum", name, icon, definition, valueName, null, null, null);
        }

        /// <summary>An embedded <c>resource_enum</c> operand typed by raw <paramref name="typedefToken"/>/
        /// <paramref name="inivalueToken"/> IDREF tokens (the raw escape hatch).</summary>
        public static FbOperand EnumRaw(string typedefToken, string inivalueToken,
            string? name = null, string? icon = null)
        {
            ArgumentNullException.ThrowIfNull(typedefToken);
            ArgumentNullException.ThrowIfNull(inivalueToken);
            return new FbOperand("resource_enum", name, icon, null, valueName: null, typedefToken, inivalueToken, null);
        }

        internal PlannedResource Materialize(IdAllocator ids)
        {
            ElementId id = ids.Allocate(TypeCode.RequireForTag(tag));
            var attrs = new List<(string, string)>();
            if (name is not null)
            {
                attrs.Add(("name", name));
            }
            if (enumDefinition is not null)
            {
                attrs.Add(("typedef", enumDefinition.Typedef));
                attrs.Add(("inivalue", enumDefinition.InitialValue(valueName!)));
            }
            else if (typedefToken is not null)
            {
                attrs.Add(("typedef", typedefToken));
                attrs.Add(("inivalue", inivalueToken!));
            }
            if (configure is not null)
            {
                var configurator = new FbResourceDefBuilder();
                configure(configurator);
                attrs.AddRange(configurator.Attributes);
            }
            if (icon is not null)
            {
                attrs.Add(("icon", icon));
            }
            return new PlannedResource(tag, id, attrs);
        }

        // Builds an event/action/condition leaf whose link2 is this embedded operand (materialized as its child).
        internal static PlannedLeaf Leaf(string tag, string icon, string name, string? note,
            FbResourceHandle link1, string method, FbOperand operand, IdAllocator ids)
        {
            ArgumentNullException.ThrowIfNull(link1);
            ArgumentNullException.ThrowIfNull(method);
            PlannedResource materialized = operand.Materialize(ids);
            return new PlannedLeaf(tag, icon, name, note, link1.PlaceholderId, materialized.Id, method)
            {
                Operand = materialized,
            };
        }
    }

    // ---- program-graph intent (accumulated by the refs, materialized at Build) ----

    /// <summary>A node in an action/branch list — a <see cref="PlannedLeaf"/> command, a nested <see cref="PlannedSub"/>
    /// or a <see cref="PlannedCase"/> switch. The marker lets the lists stay typed instead of <c>List&lt;object&gt;</c>.</summary>
    internal interface IPlannedNode
    {
    }

    /// <summary>A node in a conditions group — a <see cref="PlannedLeaf"/> condition or a nested
    /// <see cref="PlannedConditionsGroup"/>. Lets a group's child list stay typed.</summary>
    internal interface IPlannedCondition
    {
    }

    internal sealed class PlannedLeaf : IPlannedNode, IPlannedCondition
    {
        public PlannedLeaf(string tag, string icon, string name, string? note,
            ElementId? link1, ElementId? link2, string? method)
        {
            Tag = tag;
            Icon = icon;
            Name = name;
            Note = note;
            Link1 = link1;
            Link2 = link2;
            Method = method;
        }

        public string Tag { get; }

        public string Icon { get; }

        public string Name { get; }

        public string? Note { get; }

        public ElementId? Link1 { get; }

        public ElementId? Link2 { get; set; }

        public string? Method { get; }

        public PlannedResource? Operand { get; set; }
    }

    // A program_sub's true or false action branch: its actions-container name (usually the vendor default), its optional
    // note (null => the vendor DefaultNote), its optional type (true=_0x1, false/none=null) and its child nodes.
    internal sealed class PlannedBranch
    {
        public PlannedBranch(string name, string defaultNote, string? type)
        {
            Name = name;
            DefaultNote = defaultNote;
            Type = type;
        }

        public string Name { get; set; }

        public string? Note { get; set; }

        public string DefaultNote { get; }

        public string? Type { get; }

        public List<IPlannedNode> Children { get; } = new();

        public string EffectiveNote => Note ?? DefaultNote;
    }

    // A boolean grouping of conditions: a per-group note/type ("or" or implicit AND) plus its children — condition
    // leaves and, recursively, nested sub-groups.
    internal sealed class PlannedConditionsGroup : IPlannedCondition
    {
        public string? Name { get; set; }

        public string? Note { get; set; }

        public string? Type { get; set; }

        public List<IPlannedCondition> Children { get; } = new();
    }

    internal sealed class PlannedSub : IPlannedNode
    {
        public string Name { get; set; } = FbGrammar.SubProgramName;

        public string? Note { get; set; }

        public PlannedConditionsGroup Conditions { get; } = new();

        public PlannedBranch True { get; } =
            new(FbGrammar.TrueActionsName, FbGrammar.TrueActionsNote, FbGrammar.TrueBranchType);

        public PlannedBranch False { get; } =
            new(FbGrammar.FalseActionsName, FbGrammar.FalseActionsNote, null);
    }

    internal sealed class PlannedCase : IPlannedNode
    {
        public PlannedCase(string name, string? note, ElementId switchVariable)
        {
            Name = name;
            Note = note;
            SwitchVariable = switchVariable;
        }

        public string Name { get; }

        public string? Note { get; }

        public ElementId SwitchVariable { get; }

        public List<PlannedCaseAction> Cases { get; } = new();

        public PlannedBranch Default { get; } =
            new(FbGrammar.DefaultCaseName, FbGrammar.DefaultCaseNote, FbGrammar.DefaultCaseType);
    }

    internal sealed class PlannedCaseAction
    {
        public PlannedCaseAction(string name, string? note, ElementId variable, PlannedResource operand)
        {
            Name = name;
            Note = note;
            Variable = variable;
            Operand = operand;
        }

        public string Name { get; }

        public string? Note { get; }

        public ElementId Variable { get; }

        public PlannedResource Operand { get; }

        public List<IPlannedNode> Children { get; } = new();
    }

    internal sealed class PlannedResource
    {
        public PlannedResource(string tag, ElementId id, IReadOnlyList<(string Name, string Value)> attrs)
        {
            Tag = tag;
            Id = id;
            Attrs = attrs;
        }

        public string Tag { get; }

        public ElementId Id { get; }

        public IReadOnlyList<(string Name, string Value)> Attrs { get; }
    }
}
