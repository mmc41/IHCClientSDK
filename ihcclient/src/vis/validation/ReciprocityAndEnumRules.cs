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
    /// The RECIPROCITY and ENUM rules: the two bijection checks over follow-links and scene rows, and the
    /// two that keep an enumerated variable consistent with the type it names.
    /// <para>
    /// THE TWO BIJECTIONS ARE ONE CHECK WITH TWO CONFIGURATIONS, and keeping them one is what closed a real gap:
    /// the scene variant once never verified the PARTNER KIND, so a scene member wired to another member instead
    /// of its scene link slipped through. They differ in exactly two ways — which tags are halves, and whether an
    /// UNWIRED half is legitimate. A scene row may be authored unwired and is skipped; a follow-link half is
    /// never unwired, so an unwired one is corruption.
    /// </para>
    /// <para>
    /// Because the shipped code passes the rule id in as a parameter, the two ids are easy to swap by accident
    /// and a call-site scan cannot even see them. Here each is bound to its own registered rule, so the two
    /// cannot be confused and neither can go missing unnoticed.
    /// </para>
    /// <para>
    /// The enum pair defers to the schema reference check for an absent, null or dangling type reference: those
    /// are one broken reference and belong to one rule. What is left for these two is the pair of questions only
    /// they can answer — is the referenced element actually an enum definition, and is the initial state one of
    /// its values.
    /// </para>
    /// </summary>
    public static class ReciprocityAndEnumRules
    {
        /// <summary>The complementary partner kind(s) for each reciprocal half, derived from the shared tag source
        /// rather than re-listed: a from-half pairs with a to-half and vice versa, and a scene link pairs with any
        /// member row.</summary>
        private static readonly ImmutableDictionary<string, ImmutableHashSet<string>> Complements = BuildComplements();

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "link-bijection",
                    Reciprocity(ReciprocalTags.FollowLinkHalfTags, "half", allowUnwired: false)),
                Rule(catalog, "scene-bijection",
                    Reciprocity(ReciprocalTags.SceneHalfTags, "scene row", allowUnwired: true)),
                Rule(catalog, "enum-typedef", EnumTypedef),
                Rule(catalog, "enum-inivalue", EnumInitialValue));
        }

        /// <summary>
        /// One reciprocal-pair check. Every wired half must point at a live partner of the COMPLEMENTARY kind
        /// that points back at it.
        /// </summary>
        /// <param name="halfTags">Which element tags are halves of this kind of pair.</param>
        /// <param name="noun">What one half is called, for the diagnostic.</param>
        /// <param name="allowUnwired">Whether an unwired half is a legitimate authored state.</param>
        private static ProjectInspection Reciprocity(
            IReadOnlySet<string> halfTags, string noun, bool allowUnwired) => inspection =>
        {
            Dictionary<string, ProjectElement> halves = new(StringComparer.Ordinal);
            foreach (ProjectElement element in inspection.Analyses.Elements)
            {
                if (halfTags.Contains(element.Tag) && element.GetAttribute("id") is { } id)
                {
                    halves[id] = element;
                }
            }

            foreach (ProjectElement half in halves.Values)
            {
                string? partnerId = half.GetAttribute("link");
                bool unwired = partnerId is null || partnerId == ElementId.NullToken;
                if (unwired && allowUnwired)
                {
                    continue;
                }

                // ONE report for both ways a partner can be absent — no link at all, or a link pointing at
                // nothing. They are indistinguishable to a user repairing the file, and were reported as one
                // before this moved.
                if (unwired || !halves.TryGetValue(partnerId!, out ProjectElement? partner))
                {
                    inspection.Report(half, Arguments(
                        ("noun", noun), ("tag", half.Tag),
                        ("id", half.GetAttribute("id") ?? string.Empty)));
                    continue;
                }

                ImmutableHashSet<string> expected = Complements[half.Tag];
                if (!expected.Contains(partner.Tag))
                {
                    inspection.Report(half, Arguments(
                        ("noun", noun), ("tag", half.Tag),
                        ("id", half.GetAttribute("id") ?? string.Empty),
                        ("actual", partner.Tag),
                        ("expected", string.Join(" or ", expected))));
                }
                else if (partner.GetAttribute("link") != half.GetAttribute("id"))
                {
                    inspection.Report(half, Arguments(
                        ("noun", noun), ("tag", half.Tag),
                        ("id", half.GetAttribute("id") ?? string.Empty)));
                }
            }
        };

        /// <summary>An enumerated variable whose type reference points at something that is not an enum
        /// definition: the variable has no value domain at all.</summary>
        private static void EnumTypedef(IProjectInspection inspection)
        {
            foreach ((ProjectElement element, ProjectElement definition) in TypedEnums(inspection))
            {
                if (definition.Tag != "enum_definition")
                {
                    inspection.Report(element, Arguments(
                        ("typedef", element.GetAttribute("typedef") ?? string.Empty),
                        ("name", element.GetAttribute("name") ?? string.Empty),
                        ("tag", definition.Tag)));
                }
            }
        }

        /// <summary>An enumerated variable starting at a state its own type does not admit.</summary>
        private static void EnumInitialValue(IProjectInspection inspection)
        {
            foreach ((ProjectElement element, ProjectElement definition) in TypedEnums(inspection))
            {
                if (definition.Tag != "enum_definition")
                {
                    // Which element the type reference names is the other rule's question; without a definition
                    // there is no set of values for this one to check against.
                    continue;
                }

                string? initial = element.GetAttribute("inivalue");
                if (initial is null || initial == ElementId.NullToken)
                {
                    continue;
                }

                if (!definition.Children.Any(v => v.Tag == "enum_value" && v.GetAttribute("id") == initial))
                {
                    inspection.Report(element, Arguments(
                        ("inivalue", initial),
                        ("name", element.GetAttribute("name") ?? string.Empty),
                        ("typedef", definition.GetAttribute("name") ?? string.Empty)));
                }
            }
        }

        /// <summary>
        /// Every enumerated variable whose type reference RESOLVES, paired with what it resolves to. An absent,
        /// null or dangling reference is the schema reference check's business: it is one broken reference, and
        /// two rules reporting it would say so twice.
        /// </summary>
        private static IEnumerable<(ProjectElement Element, ProjectElement Definition)> TypedEnums(IProjectInspection inspection)
        {
            // Resolution goes through the topology analysis, which IS the first-holder-wins id map, rather
            // than a second one built per rule from a second scan of every element.
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (ProjectElement element in inspection.Analyses.WithTag("resource_enum"))
            {
                if (element.GetAttribute("typedef") is { } typedef
                    && typedef != ElementId.NullToken
                    && topology.ByToken(typedef) is { } definition)
                {
                    yield return (element, definition);
                }
            }
        }

        private static ImmutableDictionary<string, ImmutableHashSet<string>> BuildComplements()
        {
            ImmutableDictionary<string, ImmutableHashSet<string>>.Builder map =
                ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>(StringComparer.Ordinal);
            map[ReciprocalTags.FollowLinkFromTag] = [ReciprocalTags.FollowLinkToTag];
            map[ReciprocalTags.FollowLinkToTag] = [ReciprocalTags.FollowLinkFromTag];
            map[ReciprocalTags.SceneLinkTag] = [.. ReciprocalTags.SceneMemberTags];
            foreach (string member in ReciprocalTags.SceneMemberTags)
            {
                map[member] = [ReciprocalTags.SceneLinkTag];
            }

            return map.ToImmutable();
        }
    }
}
