#nullable enable
using System.Collections.Immutable;

namespace Ihc.Projects
{
    /// <summary>
    /// The complete wire-format facts for one element type: its <see cref="Tag"/>, type-code byte
    /// (<see cref="Code"/>, null for the five id-less elements), the verbatim canonical DTD block
    /// (<see cref="CanonicalDtdBlock"/>) the DTD emitter writes byte-for-byte, and the ordered
    /// <see cref="Attrs"/> parsed out of that block (driving body attribute order, omit-if-default and render).
    /// The verbatim block and the parsed <see cref="Attrs"/> are one source of truth that cannot drift —
    /// <see cref="Attrs"/> is derived from <see cref="CanonicalDtdBlock"/> at registry init.
    /// </summary>
    internal sealed record ElementSchema(
        string Tag,
        int? Code,
        string CanonicalDtdBlock,
        ImmutableArray<AttrSchema> Attrs);
}
