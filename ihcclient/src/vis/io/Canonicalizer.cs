using System.Collections.Immutable;

using Ihc.Vis.Model;
using Ihc.Vis.Schema;
namespace Ihc.Vis.Io
{
    /// <summary>
    /// What <see cref="Canonicalizer.Canonicalize"/> does with an attribute the element's DTD block does not
    /// declare. <see cref="Drop"/> is for catalog-sourced trees (insert / File→New), whose <c>.def</c>-only
    /// editor attributes (<c>helpid</c>/<c>access</c>/…) must be shed exactly as IHC Visual does;
    /// <see cref="Throw"/> is for the edit-session commit, where an undeclared attribute can only be an
    /// authoring error and dropping it would be silent data loss the plain serializer refuses.
    /// </summary>
    internal enum UndeclaredAttributePolicy
    {
        Drop,
        Throw,
    }

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
    /// catalog element's <em>effective</em> values are in its bag (via <see cref="Ihc.Vis.Catalog.CatalogReader"/>'s DTD defaults),
    /// canonicalizing against the project schema writes those that differ from the project default and omits the rest.
    /// </remarks>
    internal static class Canonicalizer
    {
        public static ProjectElement Canonicalize(ProjectElement element, ProjectSchemaView view,
            UndeclaredAttributePolicy policy)
        {
            ElementSchema schema = view.Get(element.Tag);
            if (policy == UndeclaredAttributePolicy.Throw)
            {
                SchemaGuards.GuardNoUnknownAttributes(element, schema);
            }

            var attrs = ImmutableArray.CreateBuilder<(string Name, string Value)>();
            foreach (AttrSchema attr in schema.Attrs)
            {
                string? value = element.GetAttribute(attr.Name);
                if (value is null)
                {
                    continue;                                   // omitted #IMPLIED / unset defaulted attribute
                }
                if (attr.OmitsOnWrite(value))
                {
                    continue;                                   // omit-if-default — the one AttrSchema.OmitsOnWrite rule (no drift)
                }
                attrs.Add((attr.Name, value));
            }
            EquatableArray<(string Name, string Value)> canonAttrs = attrs.ToImmutable();

            // Canonicalize each child, tracking whether any produced a NEW instance (a subtree that was not already
            // canonical). An unchanged child returns itself (below), so it stays reference-equal here.
            ImmutableArray<ProjectElement> sourceChildren = element.Children.AsImmutableArray();
            bool anyChildRematerialized = false;
            ImmutableArray<ProjectElement> children;
            if (sourceChildren.IsEmpty)
            {
                children = ImmutableArray<ProjectElement>.Empty;
            }
            else
            {
                var childBuilder = ImmutableArray.CreateBuilder<ProjectElement>(sourceChildren.Length);
                foreach (ProjectElement child in sourceChildren)
                {
                    ProjectElement canonChild = Canonicalize(child, view, policy);
                    anyChildRematerialized |= !ReferenceEquals(canonChild, child);
                    childBuilder.Add(canonChild);
                }
                children = childBuilder.MoveToImmutable();
            }

            // P3 sharing-preserving commit (fablerefac W4-3): when canonicalization would reproduce this element
            // verbatim — its attributes are already in canonical order/defaults and every child is unchanged — return
            // the ORIGINAL instance instead of an identical copy. This shares untouched subtrees (reference-equal)
            // between the source and the committed snapshot, so a commit path-copies only what it changed and a
            // reference-equality diff can skip whole subtrees. The canonical FORM is byte-identical either way — only
            // fewer allocations; the W4-3 CsCheck property test pins the byte-equivalence.
            if (element.Attrs == canonAttrs && !anyChildRematerialized)
            {
                return element;
            }
            return element with { Attrs = canonAttrs, Children = children };
        }
    }
}
