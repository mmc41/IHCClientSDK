#nullable enable
using System;

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
    /// Structural decorations (container names/notes/icons and the branch <c>type</c>) are the fixed vendor grammar,
    /// materialized by the builder; only the leaf <c>name</c>/<c>note</c>, the <c>method</c> token and the wiring are
    /// caller-supplied. Stage-1 design preview: every member throws <see cref="NotImplementedException"/>.
    /// </remarks>
    public sealed class FbProgramBuilder
    {
        internal FbProgramBuilder() => throw new NotImplementedException();

        /// <summary>Adds a power-up trigger (<c>event_power</c>, e.g. "Powerup") to the program's events. Returns this.</summary>
        public FbProgramBuilder AddPowerEvent(string name, string? note = null) => throw new NotImplementedException();

        /// <summary>Adds a resource-triggered <c>event</c> — fires when <paramref name="link1"/> changes per the
        /// <paramref name="method"/> operation, optionally comparing against a second operand <paramref name="link2"/>.
        /// Returns this for chaining.</summary>
        public FbProgramBuilder AddEvent(string name, FbResourceHandle link1, string method,
            FbResourceHandle? link2 = null, string? note = null) => throw new NotImplementedException();

        /// <summary>Adds a nested <c>program_sub</c> to the program's root actions (auto-creating its conditions list
        /// and true/false action branches as the vendor four-node skeleton). Returns its handle.</summary>
        public FbSubProgramRef AddSubProgram() => throw new NotImplementedException();
    }

    /// <summary>
    /// A handle to a nested <c>program_sub</c> authored via <see cref="FbProgramBuilder.AddSubProgram"/> (or
    /// <see cref="FbBranchRef.AddSubProgram"/>): exposes its conditions list and its two action branches
    /// (<see cref="WhenTrue"/>/<see cref="WhenFalse"/>).
    /// </summary>
    public sealed class FbSubProgramRef
    {
        internal FbSubProgramRef() => throw new NotImplementedException();

        /// <summary>The true-branch ("Kommandoer ved betingelser sande") action container.</summary>
        public FbBranchRef WhenTrue { get; } = null!;

        /// <summary>The false-branch ("Kommandoer ved betingelser falske") action container.</summary>
        public FbBranchRef WhenFalse { get; } = null!;

        /// <summary>Adds a <c>condition</c> to this sub-program's conditions list — a logical test on
        /// <paramref name="link1"/> per <paramref name="method"/>, optionally against <paramref name="link2"/>.
        /// Returns its handle (for attaching an embedded literal enum operand).</summary>
        public FbConditionRef AddCondition(string name, FbResourceHandle link1, string method,
            FbResourceHandle? link2 = null, string? note = null) => throw new NotImplementedException();
    }

    /// <summary>
    /// A handle to one action branch of an <see cref="FbSubProgramRef"/> (its true or false <c>actions</c> container):
    /// adds leaf <c>action</c> commands and further nested <c>program_sub</c> logic.
    /// </summary>
    public sealed class FbBranchRef
    {
        internal FbBranchRef() => throw new NotImplementedException();

        /// <summary>Adds an <c>action</c> command driving <paramref name="link1"/> per <paramref name="method"/>
        /// (optionally with a second operand <paramref name="link2"/>). Returns this for chaining.</summary>
        public FbBranchRef AddAction(string name, FbResourceHandle link1, string method,
            FbResourceHandle? link2 = null, string? note = null) => throw new NotImplementedException();

        /// <summary>Adds a nested <c>program_sub</c> (four-node skeleton) inside this branch; returns its handle.</summary>
        public FbSubProgramRef AddSubProgram() => throw new NotImplementedException();
    }

    /// <summary>
    /// A handle to a <c>condition</c> authored via <see cref="FbSubProgramRef.AddCondition"/>, for attaching an
    /// embedded literal enum operand.
    /// </summary>
    public sealed class FbConditionRef
    {
        internal FbConditionRef() => throw new NotImplementedException();

        /// <summary>Embeds a literal <c>resource_enum</c> operand inside this condition (the constant of a
        /// "%P &lt;&gt; %S" comparison), typed by a <see cref="FbEnumDefRef"/> handle and initialised to its
        /// <paramref name="valueName"/> value (tokens resolved internally), then wires the condition's <c>link2</c>
        /// at it — the GUI-friendly form. Returns the operand's handle.</summary>
        public FbResourceHandle AddEnumOperand(string name, FbEnumDefRef definition, string valueName) =>
            throw new NotImplementedException();

        /// <summary>Embeds a literal <c>resource_enum</c> operand typed by raw <paramref name="typedefToken"/> /
        /// <paramref name="inivalueToken"/> IDREF tokens directly (the raw escape hatch), then wires the condition's
        /// <c>link2</c> at it. Returns the operand's handle.</summary>
        public FbResourceHandle AddEnumOperand(string name, string typedefToken, string inivalueToken) =>
            throw new NotImplementedException();
    }
}
