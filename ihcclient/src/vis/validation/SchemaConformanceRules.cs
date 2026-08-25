#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The five SCHEMA-CONFORMANCE rules, on the engine: a missing <c>#REQUIRED</c> attribute, an enumerated
    /// value outside its declared set, an attribute no schema declares, text the ISO-8859-1 writer cannot encode,
    /// and an element type no schema declares.
    /// <para>
    /// TRAVERSALS, not declarative constraints, and the reason is the measurement the engine choice was made on:
    /// these rules drive off DTD metadata resolved AT RUNTIME. Which attributes an element must carry is not
    /// known when the rule is written — it is read from the project's own inline DTD merged with the registry — so
    /// there is no <c>(tag, attribute)</c> target to declare and nothing a dialog could bind to. A rule set that
    /// could only express per-field predicates could not state these at all.
    /// </para>
    /// <para>
    /// Three of the five are also SAVE REFUSALS. An undeclared element, an undeclared attribute and non-Latin-1
    /// text each abandon a write, because the file would silently lose the value. Both faces survive: the refusal
    /// lives at the serializer's own throw site, and the finding is what lets a user see and repair the same
    /// condition before trying to save.
    /// </para>
    /// <para>
    /// The English sentence each one used to put in front of an installer is now the entry's DIAGNOSTIC, bound
    /// from the same arguments; the user-facing message is the entry's short Danish label. Nothing is lost — the
    /// developer text moved one slot over.
    /// </para>
    /// </summary>
    public static class SchemaConformanceRules
    {
        /// <summary>The five rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "attr-required", RequiredAttributes),
                Rule(catalog, "attr-enum-range", EnumeratedValues),
                Rule(catalog, "attr-undeclared", UndeclaredAttributes),
                Rule(catalog, "attr-latin1", NonLatin1Text),
                Rule(catalog, "element-undeclared", UndeclaredElements));
        }

        /// <summary>
        /// A <c>#REQUIRED</c> attribute the element does not carry. Runs even when the element carries no
        /// attributes at all, which is the case that matters: an element written by a foreign tool may be missing
        /// everything.
        /// </summary>
        private static void RequiredAttributes(IProjectInspection inspection)
        {
            foreach ((ProjectElement element, ElementSchema schema) in Declared(inspection))
            {
                foreach (AttrSchema attr in schema.Attrs)
                {
                    if (attr.Kind == AttrKind.Required && element.GetAttribute(attr.Name) is null)
                    {
                        inspection.Report(element, Arguments(("attribute", attr.Name), ("tag", element.Tag)));
                    }
                }
            }
        }

        /// <summary>An enumerated attribute holding a value outside its declared set — no defined meaning for
        /// reader or controller.</summary>
        private static void EnumeratedValues(IProjectInspection inspection)
        {
            foreach ((ProjectElement element, ElementSchema schema) in Declared(inspection))
            {
                foreach ((string name, string value) in element.Attrs)
                {
                    if (schema.FindAttr(name) is { } attr && !attr.EnumValues.IsEmpty && !attr.EnumValues.Contains(value))
                    {
                        inspection.Report(element, Arguments(
                            ("attribute", name),
                            ("value", value),
                            ("tag", element.Tag),
                            ("allowed", string.Join(" | ", attr.EnumValues))));
                    }
                }
            }
        }

        /// <summary>An attribute neither the element's inline-DTD block nor the registry declares: the value has
        /// no declared rendering, so writing the file would lose it.</summary>
        private static void UndeclaredAttributes(IProjectInspection inspection)
        {
            foreach ((ProjectElement element, ElementSchema schema) in Declared(inspection))
            {
                foreach ((string name, string _) in element.Attrs)
                {
                    if (schema.FindAttr(name) is null)
                    {
                        inspection.Report(element, Arguments(("attribute", name), ("tag", element.Tag)));
                    }
                }
            }
        }

        /// <summary>Text the <c>.vis</c> encoding cannot represent. Checked on every element, declared or not:
        /// the writer will meet the value either way.</summary>
        private static void NonLatin1Text(IProjectInspection inspection)
        {
            foreach (ProjectElement element in inspection.Analyses.Elements)
            {
                foreach ((string name, string value) in element.Attrs)
                {
                    if (!Latin1.Contains(value))
                    {
                        inspection.Report(element, Arguments(("attribute", name), ("tag", element.Tag)));
                    }
                }
            }
        }

        /// <summary>An element type no schema declares — it has no declared rendering, so a write would lose the
        /// whole element.</summary>
        private static void UndeclaredElements(IProjectInspection inspection)
        {
            ProjectSchemaView view = inspection.Project.SchemaView;
            foreach (ProjectElement element in inspection.Analyses.Elements)
            {
                if (view.TryGet(element.Tag) is null)
                {
                    inspection.Report(element, Arguments(("tag", element.Tag)));
                }
            }
        }

        /// <summary>Every element whose type the schema DOES declare, paired with its schema. The undeclared ones
        /// are <c>element-undeclared</c>'s business and are silent here rather than reported twice.</summary>
        private static IEnumerable<(ProjectElement Element, ElementSchema Schema)> Declared(IProjectInspection inspection)
        {
            ProjectSchemaView view = inspection.Project.SchemaView;
            foreach (ProjectElement element in inspection.Analyses.Elements)
            {
                if (view.TryGet(element.Tag) is { } schema)
                {
                    yield return (element, schema);
                }
            }
        }
    }
}
