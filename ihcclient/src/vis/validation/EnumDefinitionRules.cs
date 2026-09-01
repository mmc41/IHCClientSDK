using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The ENUM-DEFINITION rows: whether a declared type is usable, and whether its values can be told apart.
    ///
    /// <para><b>Two of them report a file no dialog can produce (⊘), and they are implemented anyway.</b> The
    /// enum editor answers <i>"Vælg et andet navn"</i> to a duplicate name, and it has neither a reorder nor an
    /// index field — values append and their indices follow insertion order. So a duplicate name or index arrives
    /// from a hand-edited or foreign file, which is exactly what the whole-project face is for.</para>
    ///
    /// <para><b>AN ABSENT <c>index</c> MEANS ZERO</b>, and that is the one fact this set would be wrong without:
    /// the canonicalizer elides a value equal to the DTD default, so the first value of every definition in the
    /// corpus carries no <c>index</c> at all (318 of 417 values carry one). A predicate comparing the raw attribute
    /// would miss the collision between an absent index and an explicit <c>index="0"</c> — which is precisely the
    /// shape a hand-edited file produces.</para>
    ///
    /// <para><b>The shape rows skip what the author does not own.</b> 40 of the corpus's 109 definitions are
    /// <c>typeid</c>-bearing SYSTEM tables shipped with the format (<i>Persienne tilstand</i>, <i>Logning</i>) —
    /// read-only furniture whose shape is the format's business, not something the author can answer for. The
    /// data-tables definition is skipped for the same reason: it is a TABLE of user-defined texts, not a type, and
    /// no variable is ever declared of it.</para>
    /// </summary>
    public static class EnumDefinitionRules
    {
        private const string DefinitionTag = "enum_definition";

        private const string ValueTag = "enum_value";

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "enum-def-duplicate-name", DuplicateValueName),
                Rule(catalog, "enum-def-duplicate-index", DuplicateValueIndex),
                Rule(catalog, "enum-def-empty", Empty),
                Rule(catalog, "enum-def-single-value", SingleValue));
        }

        /// <summary>
        /// Two values of one definition with the same name: the two states are indistinguishable to a reader.
        /// <para>SUBJECT: EVERY definition, system tables included — a duplicate in a shipped table would be a
        /// defect too, and the editor cannot produce one anywhere, so a file carrying one was not written by the
        /// editor. LOCATION: the second value, with the first as a related location.</para>
        /// </summary>
        private static void DuplicateValueName(IProjectInspection inspection)
        {
            foreach (ProjectElement definition in Definitions(inspection.Analyses))
            {
                Dictionary<string, ProjectElement> seen = new(StringComparer.Ordinal);
                foreach (ProjectElement value in Values(definition))
                {
                    if (value.GetAttribute("name") is not { Length: > 0 } name)
                    {
                        continue;
                    }

                    if (seen.TryGetValue(name, out ProjectElement? first))
                    {
                        inspection.ReportGroup(value, [first], Arguments(
                            ("enum", Name(definition)), ("value", name)));
                    }
                    else
                    {
                        seen[name] = value;
                    }
                }
            }
        }

        /// <summary>
        /// Two values of one definition at the same index: the stored value is ambiguous. The set's one ERROR.
        /// <para>
        /// EFFECTIVE INDEX, not the raw attribute: an absent <c>index</c> IS zero, because the canonicalizer omits
        /// a value equal to the DTD default. Every definition's first value in the corpus is stored that way, so a
        /// raw comparison would let an absent index and an explicit <c>index="0"</c> through — the collision a
        /// hand-edited file actually produces.
        /// </para>
        /// <para>An index that is not a number at all is the schema's business, not this row's, and is skipped.</para>
        /// </summary>
        private static void DuplicateValueIndex(IProjectInspection inspection)
        {
            foreach (ProjectElement definition in Definitions(inspection.Analyses))
            {
                Dictionary<int, ProjectElement> seen = [];
                foreach (ProjectElement value in Values(definition))
                {
                    if (EffectiveIndex(value) is not { } index)
                    {
                        continue;
                    }

                    if (seen.TryGetValue(index, out ProjectElement? first))
                    {
                        inspection.ReportGroup(value, [first], Arguments(
                            ("enum", Name(definition)), ("index", index)));
                    }
                    else
                    {
                        seen[index] = value;
                    }
                }
            }
        }

        /// <summary>
        /// An authored definition with no values: no variable of that type can hold a meaningful value.
        /// </summary>
        private static void Empty(IProjectInspection inspection)
        {
            foreach (ProjectElement definition in Definitions(inspection.Analyses).Where(EnumTypeIdentity.IsAuthored))
            {
                if (!Values(definition).Any())
                {
                    inspection.Report(definition, Arguments(("enum", Name(definition))));
                }
            }
        }

        /// <summary>
        /// An authored definition with exactly one value: a variable of that type can never change.
        /// </summary>
        private static void SingleValue(IProjectInspection inspection)
        {
            foreach (ProjectElement definition in Definitions(inspection.Analyses).Where(EnumTypeIdentity.IsAuthored))
            {
                if (Values(definition).ToImmutableArray() is [{ } only])
                {
                    inspection.Report(definition, Arguments(
                        ("enum", Name(definition)), ("value", Name(only))));
                }
            }
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>
        /// The index a value really occupies: its <c>index</c> attribute, or ZERO when it carries none. Null when
        /// the attribute is present but not a number — a schema fault, reported by its own row.
        /// </summary>
        private static int? EffectiveIndex(ProjectElement value) =>
            value.GetAttribute("index") switch
            {
                null or "" => 0,
                { } text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                    => index,
                _ => null,
            };

        private static IEnumerable<ProjectElement> Definitions(IProjectAnalyses analyses) =>
            analyses.WithTag(DefinitionTag);

        private static IEnumerable<ProjectElement> Values(ProjectElement definition) =>
            definition.Children.Where(c => c.Tag == ValueTag);
    }
}
