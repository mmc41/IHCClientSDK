#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

using TypeCode = Ihc.Vis.Schema.TypeCode;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The eight IDENTITY rules: an id that is not a well-formed token, two elements sharing a token, two sharing
    /// a counter, a type code disagreeing with the element's tag, a reference to an id nothing carries, and the
    /// three mutually exclusive faults of the project's high-water mark.
    /// <para>
    /// ALL EIGHT READ ONE WALK. This is the case that justifies a shared analysis: the token set, the counter set,
    /// the maximum and the first-holder-wins ordering are established once and read by every rule here. Without
    /// it each rule re-walks the document, and the two duplicate rules would each have to re-derive the same
    /// "which of these is the duplicate" answer identically or disagree.
    /// </para>
    /// <para>
    /// THE THREE HIGH-WATER-MARK RULES ARE EXCLUSIVE, and the exclusivity lives in the ANALYSIS rather than in
    /// the rules. Each rule asks which fault holds and reports only if it is its own. That is what keeps the
    /// decision in one place — three rules each re-deriving an <c>else if</c> chain is three chances to disagree
    /// about which fault wins, and a token that never parsed reporting "0x0 is below the highest counter" is the
    /// noise the chain exists to prevent.
    /// </para>
    /// </summary>
    public static class IdentityRules
    {
        /// <summary>The eight rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "id-wellformed", MalformedTokens),
                Rule(catalog, "id-duplicate-token", DuplicateTokens),
                Rule(catalog, "id-duplicate-counter", DuplicateCounters),
                Rule(catalog, "id-typecode", TypeCodeMismatches),
                Rule(catalog, "idref-dangling", DanglingReferences),
                Rule(catalog, "luid-malformed", HighWaterMark(LastUniqueIdFault.Malformed)),
                Rule(catalog, "luid-ceiling", HighWaterMark(LastUniqueIdFault.AboveCeiling)),
                Rule(catalog, "luid-low", HighWaterMark(LastUniqueIdFault.BelowHighWaterMark)));
        }

        private static RuleDefinition Rule(ProblemCatalog catalog, string code, ProjectInspection body) =>
            catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry)
                ? new RuleBuilder(entry).Inspect(body).Build()
                : throw new RuleRegistrationException(new ProblemCode(code), RuleRegistrationFault.NoCatalogueEntry);

        /// <summary>
        /// An id that is not a well-formed <c>_0x</c> hex token in the legal packed range — nothing can reference
        /// the element reliably, and id allocation cannot account for it.
        /// </summary>
        private static void MalformedTokens(IProjectInspection inspection)
        {
            HashSet<ProjectElement> duplicates = Holders(inspection.Analyses.Ids.DuplicateTokenHolders);
            foreach (ProjectElement element in inspection.Analyses.Elements)
            {
                if (element.GetAttribute("id") is { } token
                    && !duplicates.Contains(element)
                    && !ElementId.TryParse(token, out _))
                {
                    inspection.Report(element, Arguments(("id", token), ("tag", element.Tag)));
                }
            }
        }

        /// <summary>Two elements carrying the same id token: every reference to it is ambiguous.</summary>
        private static void DuplicateTokens(IProjectInspection inspection)
        {
            foreach (ProjectElement element in inspection.Analyses.Ids.DuplicateTokenHolders)
            {
                inspection.Report(element, Arguments(
                    ("id", element.GetAttribute("id") ?? string.Empty), ("tag", element.Tag)));
            }
        }

        /// <summary>Two ids sharing a counter: the id space stops being a bijection and the next minted id may collide.</summary>
        private static void DuplicateCounters(IProjectInspection inspection)
        {
            foreach (ProjectElement element in inspection.Analyses.Ids.DuplicateCounterHolders)
            {
                inspection.Report(element, Arguments(
                    ("id", element.GetAttribute("id") ?? string.Empty), ("tag", element.Tag)));
            }
        }

        /// <summary>An id whose type code disagrees with its element tag — the vendor tool resolves the element
        /// to the wrong kind.</summary>
        private static void TypeCodeMismatches(IProjectInspection inspection)
        {
            HashSet<ProjectElement> duplicates = Holders(inspection.Analyses.Ids.DuplicateTokenHolders);
            foreach (ProjectElement element in inspection.Analyses.Elements)
            {
                if (element.GetAttribute("id") is not { } token
                    || duplicates.Contains(element)
                    || !ElementId.TryParse(token, out ElementId id)
                    || TypeCode.ForTag(element.Tag) is not { } expected
                    || expected == id.TypeCode)
                {
                    continue;
                }

                inspection.Report(element, Arguments(
                    ("id", token), ("tag", element.Tag), ("actual", id.TypeCode), ("expected", (object)expected)));
            }
        }

        /// <summary>
        /// A reference attribute naming an id no element carries. The null token is the sentinel for a
        /// deliberately unwired reference — a legitimate authored state — and is never this.
        /// </summary>
        private static void DanglingReferences(IProjectInspection inspection)
        {
            IIdAnalysis ids = inspection.Analyses.Ids;
            ProjectSchemaView view = inspection.Project.SchemaView;
            foreach (ProjectElement element in inspection.Analyses.Elements)
            {
                if (view.TryGet(element.Tag) is not { } schema)
                {
                    continue;
                }

                foreach ((string name, string value) in element.Attrs)
                {
                    if (schema.FindAttr(name) is { Render: AttrRender.IdRef }
                        && value != ElementId.NullToken
                        && !ids.IsKnownToken(value))
                    {
                        inspection.Report(element, Arguments(
                            ("attribute", name), ("value", value), ("tag", element.Tag)));
                    }
                }
            }
        }

        /// <summary>
        /// One of the three high-water-mark rules: report only when the analysis says THIS fault is the one that
        /// holds. The chain is decided once, in the analysis, so the three cannot both fire and cannot disagree.
        /// </summary>
        private static ProjectInspection HighWaterMark(LastUniqueIdFault fault) => inspection =>
        {
            if (inspection.Analyses.Ids.LastUniqueId != fault)
            {
                return;
            }

            inspection.Report(inspection.Project.Root, Arguments(
                ("value", inspection.Project.LastUniqueId ?? string.Empty),
                ("maximum", inspection.Analyses.Ids.MaxCounter)));
        };

        private static HashSet<ProjectElement> Holders(EquatableArray<ProjectElement> elements)
        {
            // BY REFERENCE: ProjectElement is a record, so two structurally identical siblings are equal by value,
            // and "is this the element the analysis flagged" is a question about WHICH one, not about what it holds.
            HashSet<ProjectElement> set = new(ReferenceEqualityComparer.Instance);
            foreach (ProjectElement element in elements)
            {
                set.Add(element);
            }

            return set;
        }

        private static EquatableArray<ProblemArgument> Arguments(params (string Name, object Value)[] values) =>
            values.Select(v => new ProblemArgument(v.Name, v.Value)).ToImmutableArray();

    }
}
