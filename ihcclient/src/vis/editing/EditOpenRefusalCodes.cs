#nullable enable
using Ihc.Vis.Problems;

namespace Ihc.Vis.Editing
{
    /// <summary>
    /// The identity of every condition that stops a project being opened for EDITING, ready to raise.
    /// <para>
    /// The read-to-write boundary runs the guards a save would fail on, once, before a user invests any work.
    /// Until this registry existed those guards had no operation head to name, so they refused with a bare
    /// <see cref="System.InvalidOperationException"/> and the session reported the generic
    /// <c>EditStatus.Failed</c> — an installer whose file carried one stray attribute was told the edit "failed"
    /// with an English engine sentence, where the same condition at save says which attribute, in Danish.
    /// </para>
    /// <para>
    /// THE COMPOSITION RULE is the one its three sibling families follow: the operation carries the dotted
    /// <c>edit.open</c> and the cause keeps the BARE catalogue id it was published under. The cause is the very
    /// same <c>attr-undeclared</c> the save family refuses with, because it is the same condition seen from the
    /// other side — one row with two operations, not two rows.
    /// </para>
    /// <para>
    /// The Danish sentence is written here rather than read from the catalogue because <c>Ihc.Vis.Editing</c> may
    /// not depend on <c>Ihc.Vis.Validation</c>. It carries the row's argument SLOTS, and the guard that knows the
    /// values binds a copy through <see cref="RefusalIdentity.Binding"/>; <c>RefusalLabelDriftTests</c> keeps this
    /// copy equal to the entry's template.
    /// </para>
    /// </summary>
    public static class EditOpenRefusalCodes
    {
        /// <summary>An attribute is declared neither in the element's inline-DTD block nor in the registry.</summary>
        public static RefusalIdentity AttrUndeclared { get; } = new(
            OperationCodes.EditOpen,
            OperationCodes.EditOpenLabel,
            new ProblemCode("attr-undeclared"),
            "Ukendt attribut '{attribute}' på <{tag}>.");

        /// <summary>Every refusal in this family, for the check that each cause has a catalogue entry.</summary>
        public static Ihc.Vis.Model.EquatableArray<RefusalIdentity> All { get; } =
            System.Collections.Immutable.ImmutableArray.Create(AttrUndeclared);
    }
}
