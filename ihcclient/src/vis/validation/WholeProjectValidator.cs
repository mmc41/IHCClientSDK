#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// THE FINDINGS FACE — the collect-all whole-project executor. Runs every rule that declares this face and
    /// that the profile selects, and reports everything it finds.
    /// <para>
    /// DETERMINISM is the property this face is gated on: output order must be the same for the same project and
    /// INDEPENDENT of registration order. Order is document-scan order, decided HERE and never by a rule — which
    /// is why <see cref="IProjectInspection"/> gives a rule no way to influence it.
    /// </para>
    /// <para>
    /// ONE pass is the design commitment: every lifecycle gate reads the findings of one run, never a second
    /// pipeline with its own rules.
    /// </para>
    /// </summary>
    public interface IWholeProjectValidator
    {
        /// <summary>Runs every rule the profile selects over the project.</summary>
        /// <param name="project">The project to validate.</param>
        /// <param name="profile">Which rules run, and at what severity.</param>
        EquatableArray<ValidationFinding> Validate(Project project, ValidationProfile profile);
    }

    /// <summary>
    /// The collect-all executor over a registered <see cref="RuleSet"/>.
    /// <para>
    /// A rule that THROWS does not abort the pass by default. It contributes one <c>internal.unexpected</c>
    /// finding carrying the exception as its English diagnostic and the run continues — otherwise a project with a
    /// novel shape stops being validated at all and the user gets a clean bill of health produced by a crash.
    /// <see cref="RuleFailurePolicy.Rethrow"/> is the diagnostic alternative.
    /// </para>
    /// <para>
    /// Immutable and safe to share: it holds the rule set and nothing per-run. Every run's state lives in locals.
    /// </para>
    /// </summary>
    public sealed class WholeProjectValidator : IWholeProjectValidator
    {
        private readonly RuleSet rules;

        /// <summary>Builds an executor over a registered rule set.</summary>
        /// <param name="rules">The rules this executor runs.</param>
        public WholeProjectValidator(RuleSet rules)
        {
            ArgumentNullException.ThrowIfNull(rules);
            this.rules = rules;
        }

        /// <inheritdoc/>
        public EquatableArray<ValidationFinding> Validate(Project project, ValidationProfile profile)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(profile);

            ProjectAnalyses analyses = new(project);
            ElementNodePath paths = new(analyses);
            Dictionary<ProjectElement, int> scanOrder = ScanOrder(analyses);
            List<Emission> emitted = [];

            // BY FACE, not every registered rule. A rule that declares only RuleFaces.DialogMetadata answers a
            // dialog's "what would be acceptable?" and has no business in the project report — and registration
            // cannot be what enforces that, because a constraint serving one face is legal there (only a
            // TRAVERSAL is required to declare this face). This is the single place the declaration can be
            // honoured, so until it was made here a face declaration meant nothing to a constraint row.
            foreach (RuleDefinition rule in rules.ForFace(RuleFaces.WholeProject))
            {
                if (!profile.Includes(rule.Entry))
                {
                    continue;
                }

                Collector collector = new(
                    project, profile.Controller, profile.Library, analyses, rule.Entry, emitted);
                try
                {
                    if (rule.Inspection is { } traversal)
                    {
                        traversal(collector);
                    }
                    else
                    {
                        RunConstraints(analyses, rule, collector);
                    }
                }
                // A shape violation is a COMPOSITION error, not a project defect, so it is deliberately outside
                // the report-and-continue net: swallowing it would turn "this rule contradicts its declaration"
                // into one more finding about the user's file, which is the opposite of what it means.
                catch (Exception ex) when (ex is not RuleRegistrationException
                    && profile.FailurePolicy == RuleFailurePolicy.ReportAndContinue)
                {
                    emitted.Add(new Emission(
                        rule.Entry,
                        null,
                        EquatableArray<ProjectElement>.Empty,
                        EquatableArray<ProblemArgument>.Empty,
                        emitted.Count,
                        ex));
                }
            }

            return emitted
                .Select(e => (Finding: Build(paths, e, profile), Key: SortKey(e, scanOrder)))
                .OrderBy(x => x.Key.Scan)
                .ThenBy(x => x.Key.Code, StringComparer.Ordinal)
                .ThenBy(x => x.Key.Locator, StringComparer.Ordinal)
                .ThenBy(x => x.Key.Arguments, StringComparer.Ordinal)
                .ThenBy(x => x.Key.Sequence)
                .Select(x => x.Finding)
                .ToImmutableArray();
        }

        private static void RunConstraints(IProjectAnalyses analyses, RuleDefinition rule, Collector collector)
        {
            RuleTarget target = rule.Entry.Target;
            if (rule.Constraints is not { } sequence)
            {
                return;
            }

            IEnumerable<ProjectElement> scope;
            if (target.Tag is { } tag)
            {
                // The shared per-run walk, not one of its own per declarative rule: this face is the one that
                // would scale with the rule population, since a constraint rule is registered per
                // (tag, attribute) pair.
                scope = analyses.WithTag(tag);
            }
            else if (target.Attribute is { } wildcard)
            {
                // A WILDCARD target — "this attribute, on whatever element the rule reports". Every element type
                // the registry says declares that attribute is in scope; this used to return early, so such a
                // rule registered, served the dialog face, and silently produced nothing here.
                //
                // Filtered out of Elements rather than concatenated from WithTag buckets, and that is not a
                // style choice: WithTag is order-safe for ONE tag only, so concatenating buckets would emit
                // every element of the first tag before any of the second, and the executor's sequence tiebreak
                // carries a rule's emission order into its findings.
                FrozenSet<string> declaring = TagsDeclaring(wildcard);
                scope = analyses.Elements.Where(element => declaring.Contains(element.Tag));
            }
            else
            {
                // The whole-project target: no attribute to constrain, so there is nothing for this face to do.
                return;
            }

            foreach (ProjectElement element in scope)
            {
                string? value = target.Attribute is { } attribute ? element.GetAttribute(attribute) : null;
                foreach (IValueConstraint constraint in sequence.Ordered)
                {
                    ValueConstraintVerdict verdict = constraint.Check(value);
                    if (!verdict.Satisfied)
                    {
                        collector.Report(element, verdict.Arguments);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Every element type the registry declares this attribute on — the wildcard's scope.
        /// <para>Memoized per attribute: the answer is a property of the static registry, so it cannot change
        /// between runs, and recomputing it would re-walk every declared element type on every validation.</para>
        /// </summary>
        private static FrozenSet<string> TagsDeclaring(string attribute) =>
            declaringTags.GetOrAdd(attribute, static name =>
                ProjectSchemaRegistry.AllSchemas
                    .Where(schema => schema.FindAttr(name) is not null)
                    .Select(schema => schema.Tag)
                    .ToFrozenSet(StringComparer.Ordinal));

        private static readonly ConcurrentDictionary<string, FrozenSet<string>> declaringTags = new(StringComparer.Ordinal);

        private static ValidationFinding Build(ElementNodePath paths, Emission emission, ValidationProfile profile)
        {
            ProblemCatalogEntry entry = emission.Entry;
            if (emission.Failure is { } failure)
            {
                return new ValidationFinding(
                    Problem.Unexpected($"Rule '{entry.Code.Value}' threw: {failure.Message}", failure),
                    ValidationSeverity.Error,
                    entry.Category ?? ValidationCategory.FileIntegrity,
                    null,
                    EquatableArray<FindingLocation>.Empty);
            }

            // BOTH texts bind from the same arguments. Binding only the message left the English diagnostic —
            // the one text written for the person reading the log — carrying its slots as literal placeholders.
            Problem problem = new(entry.Code, string.Empty, emission.Arguments, entry.Diagnostic);
            problem = problem with { Message = entry.BindTemplate(problem), Diagnostic = entry.BindDiagnostic(problem) };

            // A GROUPED finding gives every site its own text; a single-site one leaves it null, because there
            // the finding's own message already says everything. Without this a duplicate-id group listed N
            // locators with nothing to tell them apart, so a reader could navigate to them but not read them —
            // which is the difference between one navigable finding and N anonymous anchors.
            bool grouped = emission.Related.Length > 0;

            return new ValidationFinding(
                problem,
                profile.SeverityFor(entry),
                entry.Category ?? ValidationCategory.FileIntegrity,
                Locate(paths, emission.Primary, grouped ? DescribeSite(emission.Primary) : null),
                emission.Related.Select(r => Locate(paths, r, DescribeSite(r))!).ToImmutableArray())
            {
                // Projected from the entry, not re-derived: a host may not read the catalogue, so this is the
                // only door the fact has. Deliberately NOT on the failure branch above — a rule that threw
                // reports an engine fault, and an engine fault refuses none of the row's operations.
                RefusedOperations = entry.RefusedOperations,
                // Same door, same reason, and absent on the same branch: a rule that threw is about no field of
                // the user's project, so claiming one there would point a consumer at a fix location for a
                // defect in the engine.
                TargetAttribute = entry.Target.Attribute,
                // The OCCURRENCE's own answer where the rule gave one, which a consumer prefers over the
                // declared target above. Null is the ordinary case and changes nothing.
                Fix = emission.Fix,
            };
        }

        /// <summary>
        /// One site, as the three anchors a consumer can use: the raw locator, the parsed id when there is one,
        /// and — only where the locator does not select exactly one node — the exact path to it.
        /// <para>
        /// The path is decided HERE and nowhere later because this is the last place the element itself is known.
        /// Downstream, a consumer holds the locator string and no tree, so a malformed or shared token would be
        /// unrecoverable.
        /// </para>
        /// </summary>
        private static FindingLocation? Locate(ElementNodePath paths, ProjectElement? element, string? message) =>
            element is null
                ? null
                : new FindingLocation(
                    element.GetAttribute("id") ?? element.Tag,
                    element.Id,
                    message,
                    paths.WhenLocatorIsAmbiguous(element));

        /// <summary>
        /// One site of a group, as the reader must be able to tell it from its siblings.
        /// <para>
        /// The AUTHORED NAME comes first because that is what distinguishes two sites of the same collision: a
        /// duplicate id makes every site's locator identical, so a label built from the id alone would read the
        /// same N times and tell the reader nothing. The id is the fallback for an unnamed element and the bare
        /// tag the last resort.
        /// </para>
        /// </summary>
        private static string? DescribeSite(ProjectElement? element) => element switch
        {
            null => null,
            { } e when e.GetAttribute("name") is { Length: > 0 } name => $"<{e.Tag}> '{name}'",
            { } e when e.GetAttribute("id") is { Length: > 0 } id => $"<{e.Tag}> '{id}'",
            { } e => $"<{e.Tag}>",
        };

        /// <summary>
        /// The deterministic ordering key. The fourth element is the emission's ARGUMENT VALUES joined — not the
        /// finding's message, which does not exist yet: <see cref="Build"/> binds it afterwards. The arguments are
        /// what distinguish two findings of the same code on the same element, which is the job this slot does.
        /// </summary>
        private static (int Scan, string Code, string Locator, string Arguments, int Sequence) SortKey(
            Emission emission, Dictionary<ProjectElement, int> scanOrder) =>
        (
            emission.Primary is { } element && scanOrder.TryGetValue(element, out int index) ? index : -1,
            emission.Entry.Code.Value,
            emission.Primary is { } primary ? primary.GetAttribute("id") ?? primary.Tag : string.Empty,
            string.Join("|", emission.Arguments.Select(a => a.Value)),
            emission.Sequence
        );

        /// <summary>
        /// Pre-order document position of every element, keyed by REFERENCE.
        /// <para>
        /// Reference equality is load-bearing here: <see cref="ProjectElement"/> is a record, so two structurally
        /// identical siblings — two terminals with the same attributes, which is ordinary in a project — are EQUAL
        /// by value and would collapse into one dictionary entry, giving findings on the second the first's
        /// position. Identity is what document order is about.
        /// </para>
        /// </summary>
        private static Dictionary<ProjectElement, int> ScanOrder(IProjectAnalyses analyses)
        {
            Dictionary<ProjectElement, int> order = new(ReferenceEqualityComparer.Instance);
            int index = 0;
            foreach (ProjectElement element in analyses.Elements)
            {
                order.TryAdd(element, index++);
            }

            return order;
        }

        /// <summary>One reported violation, before it becomes a finding. Carries its emission order as a tiebreak.</summary>
        private sealed record Emission(
            ProblemCatalogEntry Entry,
            ProjectElement? Primary,
            EquatableArray<ProjectElement> Related,
            EquatableArray<ProblemArgument> Arguments,
            int Sequence,
            Exception? Failure)
        {
            /// <summary>This occurrence's own fix location, or null to let the entry's target speak.</summary>
            public FixLocation? Fix { get; init; }
        }

        private sealed class Collector : IProjectInspection
        {
            private readonly ProblemCatalogEntry entry;
            private readonly List<Emission> sink;

            public Collector(
                Project project,
                ControllerCapabilityLimits? controller,
                ILibraryBlockSource? library,
                IProjectAnalyses analyses,
                ProblemCatalogEntry entry,
                List<Emission> sink)
            {
                Project = project;
                Controller = controller;
                Library = library;
                Analyses = analyses;
                this.entry = entry;
                this.sink = sink;
            }

            public Project Project { get; }

            public ControllerCapabilityLimits? Controller { get; }

            public ILibraryBlockSource? Library { get; }

            public IProjectAnalyses Analyses { get; }

            public void Report(ProjectElement? element, EquatableArray<ProblemArgument> arguments) =>
                Report(element, arguments, null);

            public void Report(
                ProjectElement? element, EquatableArray<ProblemArgument> arguments, FixLocation? fix)
            {
                // The half of the shape contract a delegate hides from registration. A row that DECLARES a group
                // and then emits singletons publishes a promise the engine does not keep — N findings for one
                // repair, no relation between them — and before this the two duplicate-id rows and
                // dataline-address-duplicate did exactly that, unnoticed.
                if (entry.Shape == FindingShape.PrimaryWithRelated)
                {
                    throw new RuleRegistrationException(
                        entry.Code, RuleRegistrationFault.ShapeContradictsDeclaration);
                }

                sink.Add(new Emission(entry, element, EquatableArray<ProjectElement>.Empty, arguments, sink.Count, null)
                {
                    Fix = fix,
                });
            }

            public void ReportGroup(
                ProjectElement primary,
                EquatableArray<ProjectElement> related,
                EquatableArray<ProblemArgument> arguments)
            {
                if (entry.Shape != FindingShape.PrimaryWithRelated)
                {
                    throw new RuleRegistrationException(
                        entry.Code, RuleRegistrationFault.ShapeContradictsDeclaration);
                }

                sink.Add(new Emission(entry, primary, related, arguments, sink.Count, null));
            }
        }
    }
}
