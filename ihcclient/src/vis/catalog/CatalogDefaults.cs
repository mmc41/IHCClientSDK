using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Materializes a catalog component's inline-DTD <c>#IMPLIED</c>-with-default / defaulted ATTLIST values onto its
    /// <b>raw</b> body — reproducing the <em>effective</em> attribute values the insert transform's cross-DTD
    /// reconciliation consumes (spec ch. 09 §9.3.7). <see cref="CatalogReader"/> used to do this implicitly (via
    /// <see cref="System.Xml.XmlReader"/> DTD-default expansion), but its <c>Body</c> is now the raw file body so
    /// <see cref="CatalogFileWriter"/> can re-emit it faithfully; this restores the effective view on demand, keyed on
    /// the definition's own grammar (<see cref="ProjectSchemaView.For(ImmutableDictionary{string,string})"/>), applied
    /// at the single insert boundary. It is the exact inverse of <see cref="Ihc.Vis.Io.Canonicalizer"/>'s omit-if-default.
    /// </summary>
    internal static class CatalogDefaults
    {
        /// <summary>Returns a copy of <paramref name="element"/> with every defaulted ATTLIST attribute the element omits
        /// added at its declared default value, recursively. Present attributes (incl. any explicitly equal to a
        /// default) are untouched, preserving source order.</summary>
        public static ProjectElement Materialize(ProjectElement element, ProjectSchemaView view)
        {
            ElementSchema? schema = view.TryGet(element.Tag);
            ImmutableArray<(string Name, string Value)> present = element.Attrs.AsImmutableArray();
            ImmutableArray<(string Name, string Value)> attrs = present;
            if (schema is not null)
            {
                // Build once instead of re-allocating the whole bag per defaulted attribute. Probing `present`
                // rather than the growing bag is the same answer — schema attribute names are distinct, so an
                // appended default can never be what a later probe is looking for — and it keeps the probe O(present)
                // instead of growing with each append. Present attrs in source order then defaults in declaration
                // order is exactly what the old Add chain produced, so the materialized bag is unchanged.
                var builder = ImmutableArray.CreateBuilder<(string Name, string Value)>(
                    present.Length + schema.Attrs.Length);
                builder.AddRange(present);
                foreach (AttrSchema attr in schema.Attrs)
                {
                    if (attr.Kind == AttrKind.Defaulted && ProjectElement.GetAttribute(present, attr.Name) is null)
                    {
                        builder.Add((attr.Name, attr.Default));
                    }
                }
                attrs = builder.Count == present.Length ? present : builder.ToImmutable();
            }

            ImmutableArray<ProjectElement> children = element.Children.IsEmpty
                ? ImmutableArray<ProjectElement>.Empty
                : element.Children.Select(c => Materialize(c, view)).ToImmutableArray();

            return element with { Attrs = attrs, Children = children };
        }
    }
}
