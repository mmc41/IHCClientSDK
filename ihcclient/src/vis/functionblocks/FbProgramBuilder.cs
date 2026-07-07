#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    /// single <c>program_simple</c> and nested <c>program_sub</c> logic (a conditions list plus true/false action
    /// branches) to its root <c>actions</c>. Leaf triggers/conditions/actions reference resources by
    /// <see cref="FbResourceHandle"/> (in place of the edit-session <c>ResourceRef</c>) and carry an opaque
    /// <c>method</c> operation token.
    /// </summary>
    /// <remarks>
    /// Structural decorations (container names/notes/icons and the branch <c>type</c>) are the fixed vendor grammar
    /// (<see cref="FbGrammar"/>), materialized by the builder; only the leaf <c>name</c>/<c>note</c>, the <c>method</c>
    /// token and the wiring are caller-supplied. The builder accumulates an intent tree and materializes it into a
    /// <c>program_simple</c> subtree at <see cref="FunctionBlockDefinitionBuilder.Build"/>, allocating structural ids
    /// off the block's shared allocator; the leaf IDREFs already carry each resource's placeholder id, so nothing needs
    /// deferred resolution.
    /// </remarks>
    public sealed class FbProgramBuilder
    {
        private readonly IdAllocator ids;
        private readonly string name;
        private readonly List<PlannedLeaf> events = new();
        private readonly List<IPlannedNode> rootActions = new();

        internal FbProgramBuilder(IdAllocator ids, string name)
        {
            this.ids = ids;
            this.name = name;
        }

        internal bool HasEvents => events.Count > 0;

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

        /// <summary>Adds a nested <c>program_sub</c> to the program's root actions (auto-creating its conditions list
        /// and true/false action branches as the vendor four-node skeleton). Returns its handle.</summary>
        public FbSubProgramRef AddSubProgram()
        {
            var sub = new PlannedSub();
            rootActions.Add(sub);
            return new FbSubProgramRef(ids, sub);
        }

        internal ProjectElement Materialize()
        {
            ElementId programId = ids.Allocate(TypeCode.RequireForTag("program_simple"));
            ProjectElement eventsContainer = FbGrammar.Container(ids, "events",
                FbGrammar.EventsName, FbGrammar.EventsIcon, FbGrammar.EventsNote,
                events.Select(MaterializeLeaf).ToArray());
            ProjectElement actionsContainer = FbGrammar.Node("actions",
                ids.Allocate(TypeCode.RequireForTag("actions")),
                new[]
                {
                    ("name", FbGrammar.RootActionsName), ("icon", FbGrammar.ActionsIcon),
                    ("note", FbGrammar.RootActionsNote), ("type", FbGrammar.RootActionsType),
                },
                rootActions.Select(MaterializeNode).ToArray());
            return FbGrammar.Node("program_simple", programId,
                new[] { ("name", name), ("icon", FbGrammar.ProgramSimpleIcon) },
                new[] { eventsContainer, actionsContainer });
        }

        private ProjectElement MaterializeNode(IPlannedNode node) => node switch
        {
            PlannedSub sub => MaterializeSub(sub),
            PlannedLeaf leaf => MaterializeLeaf(leaf),
            _ => throw new InvalidOperationException($"Unknown planned node kind: {node.GetType()}"),
        };

        private ProjectElement MaterializeSub(PlannedSub sub)
        {
            ElementId subId = ids.Allocate(TypeCode.RequireForTag("program_sub"));
            var conditionAttrs = new List<(string, string)>
            {
                ("name", FbGrammar.ConditionsName), ("icon", FbGrammar.ConditionsIcon), ("note", FbGrammar.ConditionsNote),
            };
            if (sub.ConditionsType is { } conditionsType)
            {
                conditionAttrs.Add(("type", conditionsType));
            }
            ProjectElement conditions = FbGrammar.Node("conditions",
                ids.Allocate(TypeCode.RequireForTag("conditions")), conditionAttrs,
                sub.Conditions.Select(MaterializeLeaf).ToArray());
            ProjectElement trueActions = FbGrammar.Node("actions",
                ids.Allocate(TypeCode.RequireForTag("actions")),
                new[]
                {
                    ("name", FbGrammar.TrueActionsName), ("icon", FbGrammar.ActionsIcon),
                    ("note", FbGrammar.TrueActionsNote), ("type", FbGrammar.TrueBranchType),
                },
                sub.TrueChildren.Select(MaterializeNode).ToArray());
            ProjectElement falseActions = FbGrammar.Node("actions",
                ids.Allocate(TypeCode.RequireForTag("actions")),
                new[]
                {
                    ("name", FbGrammar.FalseActionsName), ("icon", FbGrammar.ActionsIcon),
                    ("note", FbGrammar.FalseActionsNote),
                },
                sub.FalseChildren.Select(MaterializeNode).ToArray());
            return FbGrammar.Node("program_sub", subId,
                new[] { ("name", FbGrammar.SubProgramName), ("icon", FbGrammar.SubProgramIcon) },
                new[] { conditions, trueActions, falseActions });
        }

        private ProjectElement MaterializeLeaf(PlannedLeaf leaf)
        {
            var attrs = new List<(string, string)> { ("name", leaf.Name), ("icon", leaf.Icon) };
            if (leaf.Note is { } note)
            {
                attrs.Add(("note", note));
            }
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
    /// <see cref="FbBranchRef.AddSubProgram"/>): exposes its conditions list and its two action branches
    /// (<see cref="WhenTrue"/>/<see cref="WhenFalse"/>).
    /// </summary>
    public sealed class FbSubProgramRef
    {
        private readonly IdAllocator ids;
        private readonly PlannedSub sub;

        internal FbSubProgramRef(IdAllocator ids, PlannedSub sub)
        {
            this.ids = ids;
            this.sub = sub;
            WhenTrue = new FbBranchRef(ids, sub.TrueChildren);
            WhenFalse = new FbBranchRef(ids, sub.FalseChildren);
        }

        /// <summary>The true-branch ("Kommandoer ved betingelser sande") action container.</summary>
        public FbBranchRef WhenTrue { get; }

        /// <summary>The false-branch ("Kommandoer ved betingelser falske") action container.</summary>
        public FbBranchRef WhenFalse { get; }

        /// <summary>Adds a <c>condition</c> to this sub-program's conditions list — a logical test on
        /// <paramref name="link1"/> per <paramref name="method"/>, optionally against <paramref name="link2"/>.
        /// Returns its handle (for attaching an embedded literal enum operand).</summary>
        public FbConditionRef AddCondition(string name, FbResourceHandle link1, string method,
            FbResourceHandle? link2 = null, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(link1);
            ArgumentNullException.ThrowIfNull(method);
            var leaf = new PlannedLeaf("condition", FbGrammar.ConditionIcon, name, note,
                link1.PlaceholderId, link2?.PlaceholderId, method);
            sub.Conditions.Add(leaf);
            return new FbConditionRef(ids, leaf);
        }

        /// <summary>Marks this sub-program's conditions list as an OR grouping (<c>conditions type="or"</c> — the
        /// default is an implicit AND). Returns this for chaining.</summary>
        public FbSubProgramRef OrConditions()
        {
            sub.ConditionsType = "or";
            return this;
        }
    }

    /// <summary>
    /// A handle to one action branch of an <see cref="FbSubProgramRef"/> (its true or false <c>actions</c> container):
    /// adds leaf <c>action</c> commands and further nested <c>program_sub</c> logic.
    /// </summary>
    public sealed class FbBranchRef
    {
        private readonly IdAllocator ids;
        private readonly List<IPlannedNode> children;

        internal FbBranchRef(IdAllocator ids, List<IPlannedNode> children)
        {
            this.ids = ids;
            this.children = children;
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

        /// <summary>Adds a nested <c>program_sub</c> (four-node skeleton) inside this branch; returns its handle.</summary>
        public FbSubProgramRef AddSubProgram()
        {
            var sub = new PlannedSub();
            children.Add(sub);
            return new FbSubProgramRef(ids, sub);
        }
    }

    /// <summary>
    /// A handle to a <c>condition</c> authored via <see cref="FbSubProgramRef.AddCondition"/>, for attaching an
    /// embedded literal enum operand.
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

    // ---- program-graph intent (accumulated by the refs, materialized at Build) ----

    /// <summary>A node in an action/branch list — either a <see cref="PlannedLeaf"/> command or a nested
    /// <see cref="PlannedSub"/>. The marker lets the lists stay typed instead of <c>List&lt;object&gt;</c>.</summary>
    internal interface IPlannedNode
    {
    }

    internal sealed class PlannedLeaf : IPlannedNode
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

    internal sealed class PlannedSub : IPlannedNode
    {
        public List<PlannedLeaf> Conditions { get; } = new();

        public string? ConditionsType { get; set; }

        public List<IPlannedNode> TrueChildren { get; } = new();

        public List<IPlannedNode> FalseChildren { get; } = new();
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
