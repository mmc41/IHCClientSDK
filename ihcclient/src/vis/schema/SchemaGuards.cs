#nullable enable
using System;

namespace Ihc.Projects
{
    /// <summary>
    /// The shared undeclared-attribute guard. Serialization, edit-session open and edit-session commit all reject
    /// an attribute the element's resolved DTD block does not declare — with the same exception and message — so
    /// the layers can never disagree: an attribute that would fail the save fails the edit session too, instead of
    /// being silently dropped by canonicalization on the way out.
    /// </summary>
    internal static class SchemaGuards
    {
        /// <summary>Throws when the element's bag carries an attribute its DTD block does not declare.</summary>
        public static void GuardNoUnknownAttributes(ProjectElement element, ElementSchema schema)
        {
            if (element.Attrs.IsDefaultOrEmpty)
            {
                return;
            }
            foreach ((string name, string _) in element.Attrs)
            {
                if (!HasAttribute(schema, name))
                {
                    throw new InvalidOperationException(
                        $"Element '{element.Tag}'{Locate(element)} carries attribute '{name}' that is not declared " +
                        "in its canonical DTD block. The project's inline DTD or the schema registry must declare " +
                        "every attribute a project uses.");
                }
            }
        }

        /// <summary>Applies <see cref="GuardNoUnknownAttributes"/> to a whole subtree (edit-session open).</summary>
        public static void GuardTreeNoUnknownAttributes(ProjectElement element, ProjectSchemaView view)
        {
            GuardNoUnknownAttributes(element, view.Get(element.Tag));
            if (element.Children.IsDefaultOrEmpty)
            {
                return;
            }
            foreach (ProjectElement child in element.Children)
            {
                GuardTreeNoUnknownAttributes(child, view);
            }
        }

        internal static bool HasAttribute(ElementSchema schema, string name)
        {
            foreach (AttrSchema attr in schema.Attrs)
            {
                if (attr.Name == name)
                {
                    return true;
                }
            }
            return false;
        }

        private static string Locate(ProjectElement element) =>
            element.Id is { } id ? $" (id {id.ToToken()})" : string.Empty;
    }
}
