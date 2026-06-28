#nullable enable
using System.Collections.Immutable;

namespace Ihc.Projects
{
    /// <summary>How an attribute's presence/default is declared in the DTD ATTLIST.</summary>
    internal enum AttrKind
    {
        /// <summary><c>#REQUIRED</c> — always serialized, in declared position.</summary>
        Required,

        /// <summary><c>#IMPLIED</c> — serialized only when present (no default).</summary>
        Implied,

        /// <summary>Has a declared default value — serialized iff the value differs from that default.</summary>
        Defaulted,
    }

    /// <summary>
    /// How an attribute value is rendered/interpreted on the wire. Only <see cref="Id"/>/<see cref="IdRef"/>
    /// participate in id allocation and IDREF remapping (the insert transform); every external <c>_0x</c> token
    /// (<c>typeid</c>, <c>icon</c>, <c>method</c>, <c>product_identifier</c>, …) is <see cref="Text"/> — opaque and
    /// copied verbatim. The byte-fidelity writer emits every value verbatim regardless of render; this
    /// classification drives the Stage-2 authoring/insert layer, not serialization.
    /// </summary>
    internal enum AttrRender
    {
        /// <summary>The element's own <c>id</c> (DTD type <c>ID</c>): an allocatable element identifier.</summary>
        Id,

        /// <summary>A reference to another element's id (DTD type <c>IDREF</c>): remapped on insert.</summary>
        IdRef,

        /// <summary>A plain unpadded decimal integer (machine-generated, e.g. <c>year</c>/<c>index</c>).</summary>
        Decimal,

        /// <summary>Opaque verbatim text — user strings, enumerated tokens, and all non-id <c>_0x</c> tokens.</summary>
        Text,
    }

    /// <summary>
    /// One attribute's wire-format facts, parsed out of the element's canonical DTD ATTLIST block (the single
    /// source of truth, <see cref="ElementSchema.CanonicalDtdBlock"/>): its <see cref="Name"/>, declaration
    /// <see cref="Kind"/>, <see cref="Render"/>, the DTD <see cref="Default"/> (empty unless
    /// <see cref="AttrKind.Defaulted"/>), and the DTD enumeration <see cref="EnumValues"/> (non-empty only for an
    /// enumerated attribute such as <c>(yes | no)</c>).
    /// </summary>
    internal sealed record AttrSchema(
        string Name,
        AttrKind Kind,
        AttrRender Render,
        string Default,
        ImmutableArray<string> EnumValues);
}
