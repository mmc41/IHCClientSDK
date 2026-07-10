#nullable enable
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
            ImmutableArray<(string Name, string Value)> attrs = element.AttrsOrEmpty();
            if (schema is not null)
            {
                foreach (AttrSchema attr in schema.Attrs)
                {
                    if (attr.Kind == AttrKind.Defaulted && ProjectElement.GetAttribute(attrs, attr.Name) is null)
                    {
                        attrs = attrs.Add((attr.Name, attr.Default));
                    }
                }
            }

            ImmutableArray<ProjectElement> children = element.Children.IsDefaultOrEmpty
                ? ImmutableArray<ProjectElement>.Empty
                : element.Children.Select(c => Materialize(c, view)).ToImmutableArray();

            return element with { Attrs = attrs, Children = children };
        }
    }
}
