#nullable enable
using System;
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
    /// What a traversal rule is given: the project, a place to put findings, and the target controller's limits
    /// when a caller supplied them.
    /// <para>
    /// Note what it does NOT give: any way to influence ORDER or to aggregate. Both live in the executor, so
    /// ordering is a property of the run and identical for the same project regardless of which rule went first.
    /// A rule that could sort would make the determinism claim unprovable.
    /// </para>
    /// </summary>
    public interface IProjectInspection
    {
        /// <summary>The project being validated. Immutable; a rule is a pure read.</summary>
        Project Project { get; }

        /// <summary>Reports one violation, with the element it is about and the declared argument bindings.</summary>
        /// <param name="element">The element the finding is about, or null when it is about the project.</param>
        /// <param name="arguments">The declared argument bindings for the rule's Danish template.</param>
        void Report(ProjectElement? element, EquatableArray<ProblemArgument> arguments);

        /// <summary>
        /// Reports one violation and says where THIS occurrence is repaired — see <see cref="FixLocation"/>.
        /// </summary>
        /// <param name="element">The element the finding is about, or null when it is about the project.</param>
        /// <param name="arguments">The declared argument bindings for the rule's Danish template.</param>
        /// <param name="fix">
        /// The occurrence's own fix location, or null when this occurrence has none.
        /// <para>NULLABLE so that a rule which mints one can hand over whatever it got. A fix location is minted
        /// from an element's id, and an element without an id cannot be addressed — so "no location" is an
        /// ordinary answer of the minting helpers, not a case for the caller to fork on. Taking it non-nullable
        /// put the same three-line null-fork in every rule module that emits one.</para>
        /// </param>
        void Report(ProjectElement? element, EquatableArray<ProblemArgument> arguments, FixLocation? fix);

        /// <summary>
        /// Reports a violation with a primary element plus related ones — the
        /// <see cref="FindingShape.PrimaryWithRelated"/> case, where one repair clears everything but the user
        /// must see every site to make it.
        /// </summary>
        /// <param name="primary">The element the finding is anchored to.</param>
        /// <param name="related">The other elements the reader must see.</param>
        /// <param name="arguments">The declared argument bindings for the rule's Danish template.</param>
        void ReportGroup(
            ProjectElement primary,
            EquatableArray<ProjectElement> related,
            EquatableArray<ProblemArgument> arguments);

        /// <summary>
        /// The target controller's limits, when the caller supplied them. A rule declaring
        /// <see cref="ProblemCatalogEntry.RequiresControllerLimits"/> is not run unless they were, so such a rule
        /// never has to handle absence.
        /// </summary>
        ControllerCapabilityLimits? Controller { get; }

        /// <summary>
        /// The library a placed block's claimed identity can be looked up in, when the caller supplied one. A rule
        /// declaring <see cref="ProblemCatalogEntry.RequiresLibrary"/> is not run unless it was, so such a rule
        /// never has to handle absence.
        /// </summary>
        ILibraryBlockSource? Library { get; }

        /// <summary>
        /// The analyses this run computes at most once. A rule reads what it needs from here rather than walking
        /// the document again, which is what stops per-rule cost scaling with rule count.
        /// </summary>
        IProjectAnalyses Analyses { get; }
    }

    /// <summary>A traversal rule body: walk the project, report what is found.</summary>
    /// <param name="inspection">The project, the finding sink and the controller limits.</param>
    public delegate void ProjectInspection(IProjectInspection inspection);

    /// <summary>
    /// One authored rule: the catalogue entry that classifies it, plus EXACTLY ONE of two bodies.
    /// <para>
    /// A rule is either a declarative <see cref="Constraints"/> sequence — value predicates, consumable by every
    /// face — or an imperative <see cref="Inspection"/> traversal, whole-project only. Never both, never neither.
    /// That is what keeps the multi-face claim honest: a traversal cannot serve the dialog face, because it has
    /// nothing a dialog could bind to.
    /// </para>
    /// <para>
    /// It carries the ENTRY rather than a separate descriptor. Kind, category, disposition, shape, target and
    /// faces all live there, so a rule cannot present one severity to a dialog and another to a report.
    /// </para>
    /// </summary>
    /// <param name="Entry">The catalogue entry — identity and all classification.</param>
    /// <param name="Constraints">The declarative body, or null for a traversal rule.</param>
    /// <param name="Inspection">The traversal body, or null for a declarative rule.</param>
    public sealed record RuleDefinition(
        ProblemCatalogEntry Entry,
        ConstraintSequence? Constraints,
        ProjectInspection? Inspection);

    /// <summary>
    /// Thrown when rule registration is refused — a composition-time programming error, so an exception rather
    /// than a problem. The reason is a <see cref="CatalogViolation"/> or a
    /// <see cref="RuleRegistrationFault"/> so that each refusal has one spelling in the codebase.
    /// <para>
    /// Failing fast at composition NAMES the offending rule, where a later sweep can only report that the rule
    /// set is inconsistent.
    /// </para>
    /// </summary>
    public sealed class RuleRegistrationException : InvalidOperationException
    {
        /// <summary>Refuses a rule for a fault in the rule itself.</summary>
        /// <param name="code">The rule that was refused.</param>
        /// <param name="fault">Why.</param>
        public RuleRegistrationException(ProblemCode code, RuleRegistrationFault fault)
            : base($"{code.Value}: {fault}")
        {
            Code = code;
            Fault = fault;
        }

        /// <summary>The rule that was refused.</summary>
        public ProblemCode Code { get; }

        /// <summary>Why it was refused.</summary>
        public RuleRegistrationFault Fault { get; }
    }

    /// <summary>
    /// Why a rule cannot be registered. Each is a programming error at composition time, not a project defect.
    /// <para>
    /// There is deliberately no "missing kind" member. Folding the rule descriptor into the catalogue entry made
    /// that unrepresentable — <see cref="ProblemCatalogEntry.Kind"/> is a non-nullable enum on the entry, so a
    /// rule with no kind cannot be constructed, which is a better outcome than rejecting one at registration.
    /// </para>
    /// </summary>
    public enum RuleRegistrationFault
    {
        /// <summary>Two rules claim the same code.</summary>
        DuplicateCode,

        /// <summary>The code has no catalogue entry, so nothing declares what it means.</summary>
        NoCatalogueEntry,

        /// <summary>The rule declares no face, so nothing would ever run it.</summary>
        NoFaceDeclared,

        /// <summary>The rule has both bodies, or neither.</summary>
        BodyCount,

        /// <summary>A traversal rule declared a face other than the whole-project one.</summary>
        TraversalCannotServeFace,

        /// <summary>The (tag, attribute) target is not declared by the schema.</summary>
        UnknownTarget,

        /// <summary>The entry is retired or ruled out, so no rule may implement it.</summary>
        CodeNotActive,

        /// <summary>
        /// The rule's emission shape contradicts the <see cref="FindingShape"/> its entry declares — a
        /// <see cref="FindingShape.PrimaryWithRelated"/> row reporting a lone site, or a single-site row
        /// reporting a group.
        /// <para>
        /// The declaration is not decoration: it is what tells a consumer whether N findings are N repairs or one,
        /// and whether a finding has other sites worth navigating to. A row that declares a group and emits
        /// singletons publishes a promise the engine does not keep, and nothing but this fault notices.
        /// </para>
        /// </summary>
        ShapeContradictsDeclaration,
    }

    /// <summary>
    /// The registered rules — introspectable METADATA a consumer can read, not only executable code.
    /// <para>
    /// The CATALOGUE is the rule catalogue: classification, target and face set live on the entry, and this type
    /// adds only the bodies and the registration checks. A second descriptor beside the entry would have been the
    /// same five facts declared twice with nothing comparing the copies.
    /// </para>
    /// <para>
    /// Immutable once created and safe to share across threads, for the same reason the catalogue is: it is built
    /// at composition and holds no per-run state.
    /// </para>
    /// </summary>
    public sealed class RuleSet
    {
        private readonly Dictionary<string, RuleDefinition> byCode;
        private readonly Dictionary<RuleFaces, ImmutableArray<RuleDefinition>> byFace;
        private readonly Dictionary<RuleTarget, ImmutableArray<RuleDefinition>> byTarget;

        private RuleSet(EquatableArray<RuleDefinition> rules)
        {
            Rules = rules;
            byCode = rules.ToDictionary(r => r.Entry.Code.Value, StringComparer.Ordinal);
            Codes = rules.Select(r => r.Entry.Code).ToImmutableArray();

            // The three views below are derived ONCE, here, for the reason the set itself is: it is built at
            // composition and never changes. Re-filtering per call re-walked all ~112 rules and allocated a fresh
            // array on every Validate and every DescribeField — the field face asks per dialog field.
            byFace = new Dictionary<RuleFaces, ImmutableArray<RuleDefinition>>
            {
                [RuleFaces.WholeProject] = Select(rules, RuleFaces.WholeProject),
                [RuleFaces.DialogMetadata] = Select(rules, RuleFaces.DialogMetadata),
            };
            byTarget = rules
                .GroupBy(r => r.Entry.Target)
                .ToDictionary(group => group.Key, group => group.ToImmutableArray());
        }

        /// <summary>Every registered rule, ordered by code.</summary>
        public EquatableArray<RuleDefinition> Rules { get; }

        /// <summary>Every code something implements — what a completeness check compares the catalogue against.</summary>
        public EquatableArray<ProblemCode> Codes { get; }

        /// <summary>The rules one face consumes.</summary>
        /// <param name="face">The face to list for.</param>
        public EquatableArray<RuleDefinition> ForFace(RuleFaces face) =>
            byFace.TryGetValue(face, out ImmutableArray<RuleDefinition> found)
                ? found
                : Select(Rules, face);

        /// <summary>
        /// The rules about one target — how the dialog face finds the constraints on a field without executing
        /// anything.
        /// <para>A WILDCARD declaration — <c>RuleTarget(null, attribute)</c>, "this attribute on whatever element
        /// the rule reports" — is about this field too, so a concrete query returns it alongside the rules
        /// declared on the tag itself. Without that union a wildcard rule would be registered, listed by code,
        /// and invisible to the only face that asks by target.</para>
        /// </summary>
        /// <param name="target">The (tag, attribute) pair to list for.</param>
        public EquatableArray<RuleDefinition> ForTarget(RuleTarget target)
        {
            ImmutableArray<RuleDefinition> exact =
                byTarget.TryGetValue(target, out ImmutableArray<RuleDefinition> found)
                    ? found
                    : ImmutableArray<RuleDefinition>.Empty;

            // Asking for the wildcard itself already reads its own bucket; only a CONCRETE query needs widening.
            if (target.Tag is null
                || target.Attribute is not { } attribute
                || !byTarget.TryGetValue(new RuleTarget(null, attribute), out ImmutableArray<RuleDefinition> wildcard)
                || wildcard.IsEmpty)
            {
                return exact;
            }

            // Ordered by code, like every other view here, so which bucket a rule was declared in cannot change
            // the order a caller sees.
            return exact.IsEmpty
                ? wildcard
                : exact.AddRange(wildcard)
                    .Sort(static (a, b) => string.CompareOrdinal(a.Entry.Code.Value, b.Entry.Code.Value));
        }

        private static ImmutableArray<RuleDefinition> Select(EquatableArray<RuleDefinition> rules, RuleFaces face) =>
            rules.Where(r => (r.Entry.Faces & face) != 0).ToImmutableArray();

        /// <summary>Looks up one rule by its code.</summary>
        /// <param name="code">The code to find.</param>
        /// <param name="rule">The rule implementing it, when one is registered.</param>
        public bool TryGet(ProblemCode code, out RuleDefinition rule)
        {
            if (code.Value is { } value && byCode.TryGetValue(value, out RuleDefinition? found))
            {
                rule = found;
                return true;
            }

            rule = null!;
            return false;
        }

        /// <summary>
        /// Registers a set of rules against a catalogue, refusing anything inconsistent.
        /// <para>
        /// The TARGET check reads the SDK's own schema registry directly. An abstraction over it would have been
        /// justified on the grounds that the view is internal — but it is internal to the SAME assembly, and the
        /// existing validator already calls it. The check is weaker than it reads either way, and deliberately so:
        /// a project's inline DTD can declare attributes the static registry does not, so an unknown target is
        /// rejected only when the registry is sure it does not exist.
        /// </para>
        /// </summary>
        /// <param name="catalog">The catalogue the rules must be declared in.</param>
        /// <param name="definitions">The rules to register.</param>
        /// <exception cref="RuleRegistrationException">A rule is inconsistent with the catalogue or with itself.</exception>
        public static RuleSet Create(ProblemCatalog catalog, IEnumerable<RuleDefinition> definitions)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(definitions);

            HashSet<string> seen = new(StringComparer.Ordinal);
            ImmutableArray<RuleDefinition>.Builder accepted = ImmutableArray.CreateBuilder<RuleDefinition>();
            foreach (RuleDefinition rule in definitions)
            {
                ProblemCode code = rule.Entry.Code;
                if (!seen.Add(code.Value))
                {
                    throw new RuleRegistrationException(code, RuleRegistrationFault.DuplicateCode);
                }

                if (!catalog.TryGet(code, out ProblemCatalogEntry declared) || declared != rule.Entry)
                {
                    throw new RuleRegistrationException(code, RuleRegistrationFault.NoCatalogueEntry);
                }

                if (LocalFault(rule) is { } fault)
                {
                    throw new RuleRegistrationException(code, fault);
                }

                accepted.Add(rule);
            }

            return new RuleSet(accepted.OrderBy(r => r.Entry.Code.Value, StringComparer.Ordinal).ToImmutableArray());
        }

        /// <summary>
        /// The faults a rule can carry on its OWN, without seeing the rest of the set. Shared with the authoring
        /// builder so a rule fails at the point it is written rather than at the point it is collected, and
        /// stated once so the two cannot drift into disagreeing about what a valid rule is.
        /// </summary>
        /// <param name="rule">The rule to check.</param>
        internal static RuleRegistrationFault? LocalFault(RuleDefinition rule)
        {
            if (rule.Entry.Status != ProblemCodeStatus.Active)
            {
                return RuleRegistrationFault.CodeNotActive;
            }

            if (rule.Entry.Faces == RuleFaces.None)
            {
                return RuleRegistrationFault.NoFaceDeclared;
            }

            if ((rule.Constraints is null) == (rule.Inspection is null))
            {
                return RuleRegistrationFault.BodyCount;
            }

            if (rule.Inspection is not null && rule.Entry.Faces != RuleFaces.WholeProject)
            {
                return RuleRegistrationFault.TraversalCannotServeFace;
            }

            // The half of the shape contract that IS decidable without running anything: a declarative rule
            // reports through Report and has no way to name a related site, so declaring a group is a
            // contradiction the composition can be failed on. The other half — a TRAVERSAL that declares a group
            // and then emits singletons — cannot be seen from a delegate, and is enforced at the emission itself.
            if (rule.Constraints is not null && rule.Entry.Shape == FindingShape.PrimaryWithRelated)
            {
                return RuleRegistrationFault.ShapeContradictsDeclaration;
            }

            return TargetIsKnown(rule.Entry.Target) ? null : RuleRegistrationFault.UnknownTarget;
        }

        private static bool TargetIsKnown(RuleTarget target)
        {
            if (target.IsWholeProject)
            {
                return true;
            }

            if (target.Tag is null)
            {
                // A null tag with an attribute is the WILDCARD: "this attribute, on whatever element the rule
                // reports". Both members null is the project as a whole, and was answered above — so an
                // attribute is present here.
                //
                // It is still checked, and that is the point of this branch: with no tag to look the attribute
                // up on, the wildcard used to be the one target shape registration accepted unread, so a typo in
                // it registered cleanly and surfaced only as a route that never fired. The answerable question
                // is whether ANY declared element has the attribute.
                return target.Attribute is { } wildcard && DeclaredByAnyTag(wildcard);
            }

            if (ProjectSchemaView.RegistryOnly.TryGet(target.Tag) is not { } schema)
            {
                // The registry does not know the tag. A project's inline DTD may still declare it, so this is not
                // evidence of a mistake and the target is accepted.
                return true;
            }

            return target.Attribute is not { } attribute || schema.FindAttr(attribute) is not null;
        }

        /// <summary>Whether any element the registry declares carries this attribute — the wildcard's tag test.</summary>
        private static bool DeclaredByAnyTag(string attribute)
        {
            foreach (ElementSchema schema in ProjectSchemaRegistry.AllSchemas)
            {
                if (schema.FindAttr(attribute) is not null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
