using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;

namespace Ihc.Vis.Schema
{
    /// <summary>
    /// The complete wire-format facts for one element type: its <see cref="Tag"/>, the verbatim canonical DTD block
    /// (<see cref="CanonicalDtdBlock"/>) the DTD emitter writes byte-for-byte, and the ordered
    /// <see cref="Attrs"/> parsed out of that block (driving body attribute order, omit-if-default and render).
    /// The verbatim block and the parsed <see cref="Attrs"/> are one source of truth that cannot drift —
    /// <see cref="Attrs"/> is derived from <see cref="CanonicalDtdBlock"/> at registry init, and a schema built
    /// from a structured <see cref="GrammarDeclaration"/> derives both from the same record
    /// (<see cref="FromDeclaration"/> — no render-then-reparse round trip).
    /// </summary>
    internal sealed record ElementSchema(
        string Tag,
        string CanonicalDtdBlock,
        EquatableArray<AttrSchema> Attrs)
    {
        /// <summary>The schema-view projection of one structured grammar declaration — attr-for-attr, with the
        /// default literal decoded to its logical value and the block text rendered in the <c>.vis</c>-side form
        /// (orphan gets its synthesized <c>&lt;!ELEMENT tag ANY&gt;</c> line, so a project this block is hoisted
        /// into reloads).</summary>
        public static ElementSchema FromDeclaration(GrammarDeclaration declaration) =>
            new(declaration.Tag,
                CatalogDtdEmitter.RenderProjectBlock(declaration),
                declaration.Attrs.Select(FromGrammarAttr).ToImmutableArray());

        private static AttrSchema FromGrammarAttr(GrammarAttr attr) =>
            new(attr.Name,
                attr.Default switch
                {
                    GrammarDefault.Required => AttrKind.Required,
                    GrammarDefault.Implied => AttrKind.Implied,
                    _ => AttrKind.Defaulted,
                },
                attr.Type switch
                {
                    GrammarAttrType.Id => AttrRender.Id,
                    GrammarAttrType.IdRef => AttrRender.IdRef,
                    _ => AttrRender.Text,
                },
                attr.DecodedLiteral,
                attr.EnumTokens);
        /// <summary>The declared schema for the named attribute, or <c>null</c> when this element type does not declare it.</summary>
        public AttrSchema? FindAttr(string name)
        {
            foreach (AttrSchema attr in Attrs)
            {
                if (attr.Name == name)
                {
                    return attr;
                }
            }
            return null;
        }

        /// <summary>True when the named attribute is declared as an IDREF — i.e. it participates in id allocation and remapping.</summary>
        public bool IsIdRef(string name) => FindAttr(name)?.Render == AttrRender.IdRef;
    }
}
