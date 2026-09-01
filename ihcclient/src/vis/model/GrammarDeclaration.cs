using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// One per-tag declaration record of a catalog component's inline DTD, covering all three corpus shapes:
    /// the adjacent <c>&lt;!ELEMENT tag ANY&gt;</c>+<c>&lt;!ATTLIST tag …&gt;</c> pair
    /// (<see cref="HasElementDecl"/> with attrs), the lone <c>&lt;!ELEMENT&gt;</c> (empty <see cref="Attrs"/> —
    /// the vendor's mis-cased <c>resource_Light</c> class), and the <b>orphan</b> <c>&lt;!ATTLIST&gt;</c> with no
    /// element declaration of its own (<see cref="HasElementDecl"/> false — the "med logning" class). An ordered
    /// list of these records reproduces a vendor header's declaration stream exactly (corpus: zero interleavings).
    /// Created through validated factories only; value semantics.
    /// </summary>
    public sealed record GrammarDeclaration
    {
        /// <summary>The element type name the declaration is for (ordinal-compared — the corpus contains a
        /// case-colliding pair that case-folding would merge).</summary>
        public string Tag { get; }

        /// <summary>Whether the record carries its own <c>&lt;!ELEMENT tag ANY&gt;</c> line — false for an orphan
        /// ATTLIST, which the catalog rendering re-emits without one while the project-block rendering synthesizes
        /// it (a <c>.vis</c> block requires the ELEMENT line).</summary>
        public bool HasElementDecl { get; }

        /// <summary>The ATTLIST's attribute declarations in declared order; empty for an ELEMENT-only record.</summary>
        public EquatableArray<GrammarAttr> Attrs { get; }

        // Private, and the properties are get-only rather than init: construction stays behind the validated
        // factories below, so `with` cannot bypass them and no caller can build a contradictory grammar.
        private GrammarDeclaration(string tag, bool hasElementDecl, EquatableArray<GrammarAttr> attrs)
        {
            Tag = tag;
            HasElementDecl = hasElementDecl;
            Attrs = attrs;
        }

        /// <summary>The common shape: <c>&lt;!ELEMENT tag ANY&gt;</c> followed by an ATTLIST with
        /// <paramref name="attrs"/> (may be empty, yielding an ELEMENT-only declaration).</summary>
        public static GrammarDeclaration Element(string tag, params GrammarAttr[] attrs) =>
            Create(tag, hasElementDecl: true,
                   attrs is null ? ImmutableArray<GrammarAttr>.Empty : attrs.ToImmutableArray());

        /// <summary>A lone <c>&lt;!ELEMENT tag ANY&gt;</c> with no ATTLIST.</summary>
        public static GrammarDeclaration ElementOnly(string tag) =>
            Create(tag, hasElementDecl: true, ImmutableArray<GrammarAttr>.Empty);

        /// <summary>An orphan <c>&lt;!ATTLIST tag …&gt;</c> with no element declaration of its own — must carry at
        /// least one attribute (an empty orphan would render as no text at all).</summary>
        public static GrammarDeclaration AttlistOnly(string tag, params GrammarAttr[] attrs) =>
            Create(tag, hasElementDecl: false,
                   attrs is null ? ImmutableArray<GrammarAttr>.Empty : attrs.ToImmutableArray());

        // The single validated construction path. Per-declaration DTD validity constraints (corpus-verified to
        // hold with zero violations): at most one ID-typed attribute (VC: One ID per Element Type) and unique
        // attribute names; plus the model's own consistency rules (XML-Name tag, non-empty orphan).
        internal static GrammarDeclaration Create(string tag, bool hasElementDecl, EquatableArray<GrammarAttr> attrs)
        {
            ArgumentNullException.ThrowIfNull(tag);
            GrammarAttr.VerifyXmlName(tag, $"declaration tag '{tag}'");
            EquatableArray<GrammarAttr> list = attrs;   // no default-normalization needed: the wrapper reads default as empty
            if (!hasElementDecl && list.IsEmpty)
            {
                throw new ArgumentException(
                    $"Orphan ATTLIST declaration for '{tag}' must carry at least one attribute.");
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            int idCount = 0;
            foreach (GrammarAttr attr in list)
            {
                if (attr is null)
                {
                    throw new ArgumentException($"Declaration for '{tag}' contains a null attribute.");
                }
                if (!names.Add(attr.Name))
                {
                    throw new ArgumentException($"Declaration for '{tag}' declares attribute '{attr.Name}' twice.");
                }
                if (attr.Type == GrammarAttrType.Id && ++idCount > 1)
                {
                    throw new ArgumentException(
                        $"Declaration for '{tag}' declares more than one ID-typed attribute (XML VC: One ID per Element Type).");
                }
            }
            return new GrammarDeclaration(tag, hasElementDecl, list);
        }

        /// <summary>The declared schema for the named attribute, or <c>null</c>.</summary>
        public GrammarAttr? FindAttr(string name)
        {
            foreach (GrammarAttr attr in Attrs)
            {
                if (attr.Name == name)
                {
                    return attr;
                }
            }
            return null;
        }

        // Equality and hashing are the record's, over EquatableArray<GrammarAttr> Attrs. ToString stays
        // handwritten: this DTD-shaped form is what diagnostics print, not the record's `Type { Prop = … }`.
        public override string ToString() =>
            $"GrammarDeclaration({Tag}, {(HasElementDecl ? "element" : "orphan-attlist")}, {Attrs.Length} attrs)";
    }
}
