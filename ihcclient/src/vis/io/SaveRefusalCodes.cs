#nullable enable
using Ihc.Vis.Problems;

namespace Ihc.Vis.Io
{
    /// <summary>
    /// The identity of every condition that stops a project being written, ready to raise.
    /// <para>
    /// Each member is a whole <see cref="RefusalIdentity"/> rather than a bare code, because a refusing site
    /// needs four things at once — the operation, the cause, and the Danish words for each — and three of the
    /// six sites sit below the validation engine, which may not be read from there. Bundling them is what keeps
    /// one spelling of a refusal instead of one per site.
    /// </para>
    /// <para>
    /// THE COMPOSITION RULE is D09's, unchanged from the load family: the operation carries the dotted
    /// <c>io.save</c> and each cause keeps the BARE catalogue id it was published under. No row is renamed into
    /// <c>io.save-attr-latin1</c> — that would rename a published id and leave anyone filtering on the old one
    /// seeing nothing.
    /// </para>
    /// <para>
    /// Four of the six causes are ALSO whole-project findings — <c>attr-latin1</c>, <c>attr-required</c>,
    /// <c>attr-undeclared</c> and <c>element-undeclared</c> each report at validate and refuse at save. That is
    /// one row with two faces, not two rows: the Danish sentence a user reads is the same either way, which is
    /// why the labels here are the catalogue's own templates and a test pins that they still agree.
    /// </para>
    /// <para>
    /// Those four labels carry ARGUMENT SLOTS, so the member below holds the template and the raising site binds
    /// it with <see cref="RefusalIdentity.Binding"/> — the site is the only place that knows which attribute on
    /// which element failed. Keeping the template on the member is what lets the drift gate compare it to the
    /// catalogue entry; a member that stored a bound sentence would have nothing to compare.
    /// </para>
    /// </summary>
    public static class SaveRefusalCodes
    {
        private static RefusalIdentity Refusing(string cause, string causeLabel) =>
            new(OperationCodes.Save, OperationCodes.SaveLabel, new ProblemCode(cause), causeLabel);

        /// <summary>An attribute value carries text outside ISO-8859-1, which the <c>.vis</c> encoding cannot represent.</summary>
        public static RefusalIdentity AttrLatin1 { get; } = Refusing("attr-latin1", "Tegn kan ikke gemmes i attributten '{attribute}' på <{tag}>.");

        /// <summary>A <c>#REQUIRED</c> attribute is missing, so the file would violate the DTD it declares inline.</summary>
        public static RefusalIdentity AttrRequired { get; } = Refusing("attr-required", "Den påkrævede attribut '{attribute}' mangler på <{tag}>.");

        /// <summary>An attribute is declared neither in the element's inline-DTD block nor in the registry.</summary>
        public static RefusalIdentity AttrUndeclared { get; } = Refusing("attr-undeclared", "Ukendt attribut '{attribute}' på <{tag}>.");

        /// <summary>An element type is declared neither in the file's inline DTD nor in the registry.</summary>
        public static RefusalIdentity ElementUndeclared { get; } = Refusing("element-undeclared", "Ukendt elementtype <{tag}>.");

        /// <summary>The destination cannot be written: locked, read-only, missing, or out of space.</summary>
        public static RefusalIdentity TargetUnwritable { get; } = Refusing("save-target-unwritable", "Filen kunne ikke skrives");

        /// <summary>Re-reading the just-written bytes does not reproduce the project.</summary>
        public static RefusalIdentity RoundTripMismatch { get; } =
            Refusing("save-roundtrip-mismatch", "Projektet kan ikke gemmes uden tab");

        /// <summary>Every refusal in this family, for the check that each cause has a catalogue entry.</summary>
        public static Ihc.Vis.Model.EquatableArray<RefusalIdentity> All { get; } =
            System.Collections.Immutable.ImmutableArray.Create(
                AttrLatin1, AttrRequired, AttrUndeclared, ElementUndeclared, TargetUnwritable, RoundTripMismatch);
    }
}
