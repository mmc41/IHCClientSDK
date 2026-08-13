#nullable enable
using System;
using System.Collections.Generic;

using Ihc.Vis.Model;
using Ihc.Vis.Schema;
namespace Ihc.Vis.Editing
{
    /// <summary>How an attribute must be supplied — the GUI-facing projection of the DTD ATTLIST default kind.</summary>
    public enum AttrRequirement
    {
        /// <summary><c>#REQUIRED</c> — always written and cannot be cleared.</summary>
        Required,

        /// <summary><c>#IMPLIED</c> — optional; written only when present (no default).</summary>
        Optional,

        /// <summary>Has a declared default; written only when the value differs from that default (omit-if-default).</summary>
        Defaulted,
    }

    /// <summary>
    /// One editable attribute of an element, as a property-grid row: its <see cref="Name"/>, how it must be
    /// supplied (<see cref="Kind"/>), the declared <see cref="Default"/> (for <see cref="AttrRequirement.Defaulted"/>
    /// only, else <c>null</c>), and the closed set of <see cref="AllowedValues"/> for an enumerated attribute (empty
    /// for free text). This is the public projection of the internal schema model, so the wire-grammar types stay
    /// encapsulated; it is what <see cref="ElementRef.EditableAttributes"/> returns to drive the F2 properties panel.
    /// </summary>
    public sealed record AttrInfo(
        string Name,
        AttrRequirement Kind,
        string? Default,
        EquatableArray<string> AllowedValues)
    {
        internal static AttrInfo From(AttrSchema attr) =>
            new(attr.Name,
                attr.Kind switch
                {
                    AttrKind.Required => AttrRequirement.Required,
                    AttrKind.Implied => AttrRequirement.Optional,
                    _ => AttrRequirement.Defaulted,
                },
                attr.Kind == AttrKind.Defaulted ? attr.Default : null,
                attr.EnumValues);   // implicit, and default reads as empty — no normalizing ternary needed
    }
}
