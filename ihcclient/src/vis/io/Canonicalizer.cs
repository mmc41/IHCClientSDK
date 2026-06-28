#nullable enable
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Projects
{
    /// <summary>
    /// Reduces a node subtree to its <b>canonical in-memory form</b> against the project schema registry: each
    /// node's attribute bag becomes exactly the attributes the serializer would write — in ATTLIST order, dropping
    /// any equal to its DTD default (omit-if-default, S9) and any not declared for the element type (editor-only
    /// attributes such as <c>helpid</c>/<c>access</c>). Applied recursively.
    /// </summary>
    /// <remarks>
    /// This is the bridge that makes authored/created/inserted trees both serialize byte-identically <em>and</em>
    /// re-load structurally equal: the .vis reader stores only the physically-present attributes in document
    /// (= ATTLIST) order, so a canonicalized tree matches what a save+reload yields. It is the single place the
    /// cross-DTD default materialization of the insert transform (spec ch. 09 §9.3.7) actually happens — once a
    /// catalog element's <em>effective</em> values are in its bag (via <see cref="CatalogReader"/>'s DTD defaults),
    /// canonicalizing against the project schema writes those that differ from the project default and omits the rest.
    /// </remarks>
    internal static class Canonicalizer
    {
        public static ProjectElement Canonicalize(ProjectElement element, ProjectSchemaView view)
        {
            ElementSchema schema = view.Get(element.Tag);

            var attrs = ImmutableArray.CreateBuilder<(string, string)>();
            foreach (AttrSchema attr in schema.Attrs)
            {
                string? value = element.GetAttribute(attr.Name);
                if (value is null)
                {
                    continue;                                   // omitted #IMPLIED / unset defaulted attribute
                }
                if (attr.Kind == AttrKind.Defaulted && value == attr.Default)
                {
                    continue;                                   // omit-if-default (exact string compare)
                }
                attrs.Add((attr.Name, value));
            }

            ImmutableArray<ProjectElement> children = element.Children.IsDefaultOrEmpty
                ? ImmutableArray<ProjectElement>.Empty
                : element.Children.Select(c => Canonicalize(c, view)).ToImmutableArray();

            return element with { Attrs = attrs.ToImmutable(), Children = children };
        }
    }
}
