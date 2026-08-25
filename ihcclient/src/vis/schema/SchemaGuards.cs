#nullable enable
using System;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
namespace Ihc.Vis.Schema
{
    /// <summary>
    /// The shared undeclared-attribute guard. Serialization, edit-session open and edit-session commit all reject
    /// an attribute the element's resolved DTD block does not declare — with the same exception and message — so
    /// the layers can never disagree: an attribute that would fail the save fails the edit session too, instead of
    /// being silently dropped by canonicalization on the way out.
    /// </summary>
    internal static class SchemaGuards
    {
        /// <summary>
        /// Throws when the element's bag carries an attribute its DTD block does not declare, WITHOUT a coded
        /// identity: the callers that reach this overload — edit commit, the insert transform, a definition
        /// build — refuse operations that have no operation head yet, and naming the wrong one would be worse
        /// than naming none. The English diagnostic is the same one either way.
        /// </summary>
        public static void GuardNoUnknownAttributes(ProjectElement element, ElementSchema schema) =>
            Guard(element, schema, refusing: null);

        /// <summary>
        /// Throws when the element's bag carries an attribute its DTD block does not declare, refusing with the
        /// caller's identity. The operation is the CALLER's fact: this same guard runs at save, at edit-session
        /// open and at edit commit, so a hard-coded operation here would be wrong at two of the three.
        /// </summary>
        /// <param name="element">The element whose attribute bag is checked.</param>
        /// <param name="schema">The element's resolved DTD block.</param>
        /// <param name="refusing">The operation being refused and the cause's published id.</param>
        public static void GuardNoUnknownAttributes(
            ProjectElement element, ElementSchema schema, RefusalIdentity refusing) =>
            Guard(element, schema, refusing);

        private static void Guard(ProjectElement element, ElementSchema schema, RefusalIdentity? refusing)
        {
            foreach ((string name, string _) in element.Attrs)
            {
                if (schema.FindAttr(name) is null)
                {
                    // One message for both overloads: an attribute that fails the save must fail the edit
                    // session with the same words, or the layers can be read as disagreeing.
                    string diagnostic =
                        $"Element '{element.Tag}'{Locate(element)} carries attribute '{name}' that is not declared " +
                        "in its canonical DTD block. The project's inline DTD or the schema registry must declare " +
                        "every attribute a project uses.";
                    // Two statements rather than one conditional throw: the coded refusal and the uncoded
                    // fall-through carry different exception types, and spelling each out keeps which is which
                    // readable at the throw site.
                    if (refusing is { } identity)
                    {
                        // The caller's label declares which attribute on which element; this guard is the only
                        // place that knows them, so it binds them in.
                        throw new RefusedOperationException(
                            identity.Binding(
                                new ProblemArgument("attribute", name),
                                new ProblemArgument("tag", element.Tag)),
                            diagnostic);
                    }

                    throw new InvalidOperationException(diagnostic);
                }
            }
        }

        /// <summary>Applies <see cref="GuardNoUnknownAttributes(ProjectElement, ElementSchema)"/> to a whole subtree.</summary>
        public static void GuardTreeNoUnknownAttributes(ProjectElement element, ProjectSchemaView view)
        {
            GuardNoUnknownAttributes(element, view.Get(element.Tag));
            foreach (ProjectElement child in element.Children)
            {
                GuardTreeNoUnknownAttributes(child, view);
            }
        }

        /// <summary>
        /// The same subtree walk, refusing with the caller's identity — what edit-session open uses, so a
        /// document it will not open says WHICH attribute stopped it under a code, instead of reaching a caller
        /// as an untyped failure.
        /// </summary>
        /// <param name="element">The subtree root to check.</param>
        /// <param name="view">The schema view the element's DTD blocks resolve through.</param>
        /// <param name="refusing">The operation being refused and the cause's published id.</param>
        public static void GuardTreeNoUnknownAttributes(
            ProjectElement element, ProjectSchemaView view, RefusalIdentity refusing)
        {
            GuardNoUnknownAttributes(element, view.Get(element.Tag), refusing);
            foreach (ProjectElement child in element.Children)
            {
                GuardTreeNoUnknownAttributes(child, view, refusing);
            }
        }

        private static string Locate(ProjectElement element) =>
            element.Id is { } id ? $" (id {id.ToToken()})" : string.Empty;
    }
}
