#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Schema;
using TypeCode = Ihc.Vis.Schema.TypeCode;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Reduces a component-definition body to the canonical form used to prove a code-authored builder reproduces a
    /// catalog file — the "a builder is a code-authored <see cref="CatalogReader"/>" contract. A raw
    /// <c>Build().Body == CatalogReader.Read(file)</c> cannot hold: the two use different placeholder id schemes (the
    /// <c>.def</c> files a plain counter, the <c>.ifb</c> files <c>(counter&lt;&lt;8)|typeCode</c>, the builder its own
    /// allocator), and the reader materializes DTD-default attributes a lean body omits. So
    /// <see cref="Normalize(ProjectElement, ImmutableDictionary{string, string})"/>
    /// canonicalizes against the source grammar (dropping its DTD-default attributes symmetrically), then renumbers
    /// every id in document order and remaps the schema-declared IDREFs through the same map. Two trees that carry the
    /// same structure, attributes and wiring then compare equal by <c>ProjectElement.Equals</c> regardless of
    /// their raw id tokens.
    /// </summary>
    /// <remarks>
    /// Promoted out of the test harness so the catalog code-generator's self-verify runs the <em>identical</em>
    /// Normalize/compare the oracle tests use (the generator's fidelity gate is the same logic pointed at the real
    /// <c>.def</c>/<c>.ifb</c>); <c>SyntheticOracle</c> and the generator both call this shared core.
    /// </remarks>
    internal static class DefinitionNormalizer
    {
        /// <summary>Reduces a body to the canonical comparison form: canonicalize against the source grammar (so its
        /// DTD-default attributes drop on both sides), then renumber ids document-order and remap IDREFs. When
        /// <paramref name="blocks"/> is empty the registry grammar alone is used.</summary>
        internal static ProjectElement Normalize(ProjectElement body, ImmutableDictionary<string, string> blocks)
        {
            ProjectSchemaView view = blocks.IsEmpty ? ProjectSchemaView.RegistryOnly : ProjectSchemaView.For(blocks);
            ProjectElement canonical = Canonicalizer.Canonicalize(body, view, UndeclaredAttributePolicy.Drop);
            return Renumber(canonical, view);
        }

        /// <summary>The same canonical reduction keyed on a definition's structured grammar (registry-only when the
        /// grammar carries no declarations).</summary>
        internal static ProjectElement Normalize(ProjectElement body, CatalogGrammar grammar)
        {
            ProjectSchemaView view = ProjectSchemaView.For(grammar);
            ProjectElement canonical = Canonicalizer.Canonicalize(body, view, UndeclaredAttributePolicy.Drop);
            return Renumber(canonical, view);
        }

        // Re-mints every id in document (pre-order) sequence off a fresh allocator, keeping each element's type-code
        // low byte, and remaps every schema-declared IDREF through the old→new token map (the shared two-pass core in
        // CatalogIds). Standalone equivalent of the insert transform's Reassign+RemapIdRefs, minus the project-only
        // passes (enum hoisting, icon stamping).
        //
        // Id assignment is ELEMENT-based, not token-based: catalog .def files legitimately carry duplicate ids (spec
        // ch. 09 §9.3.3 — e.g. a "Controller Link" product's 18 inputs all share one id, or a root and a child collide)
        // that the insert transform re-mints to distinct ids (InsertTransform.Reassign allocates per id-bearing element,
        // §2.2). So each element gets its OWN fresh id here too; the token map (last-wins for a duplicated token) is used
        // only to remap IDREFs, which always target a unique id. For a file with no duplicate ids the two are identical.
        private static ProjectElement Renumber(ProjectElement root, ProjectSchemaView view)
        {
            var assignAllocator = new IdAllocator(0);
            Dictionary<string, string> idRefMap = CatalogIds.BuildIdMap(root, e => FreshToken(assignAllocator, e));
            var rewriteAllocator = new IdAllocator(0);
            return CatalogIds.RewriteIds(root, view, e => FreshToken(rewriteAllocator, e), idRefMap);
        }

        private static string FreshToken(IdAllocator allocator, ProjectElement element)
        {
            int typeCode = TypeCode.ForTag(element.Tag) ?? (element.Id is { } id ? id.TypeCode : 0);
            return allocator.Allocate(typeCode).ToToken();
        }

        /// <summary>Renders a normalized tree as indented pseudo-XML for a readable structural diff on mismatch.</summary>
        internal static string Dump(ProjectElement element, int depth = 0)
        {
            var builder = new StringBuilder();
            string indent = new string(' ', depth * 2);
            string attrs = string.Join(" ",
                element.Attrs.Select(a => $"{a.Name}=\"{a.Value}\""));
            builder.Append(indent).Append('<').Append(element.Tag);
            if (attrs.Length > 0)
            {
                builder.Append(' ').Append(attrs);
            }
            if (element.Children.IsEmpty)
            {
                builder.Append("/>\n");
            }
            else
            {
                builder.Append(">\n");
                foreach (ProjectElement child in element.Children)
                {
                    builder.Append(Dump(child, depth + 1));
                }
                builder.Append(indent).Append("</").Append(element.Tag).Append(">\n");
            }
            return builder.ToString();
        }
    }
}
