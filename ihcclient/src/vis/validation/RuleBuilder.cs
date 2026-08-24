#nullable enable
using System;

using Ihc.Vis.Model;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The HAND-BUILT fluent authoring surface. Not a wrapper over a validation library: there is no library
    /// object model to adapt to, which is the freedom that choice was made for. A rule names its
    /// <see cref="RuleTarget"/> directly — <c>("product_dataline", "cabletype")</c> — because the element model
    /// has no typed members a property selector could bind to, its rule set being derived from DTD metadata at
    /// runtime.
    /// <para>
    /// It asks only for what the catalogue entry does not already say. Kind, category, disposition, shape, target
    /// and the controller-limits requirement are on the entry, so the builder does not offer them: re-stating a
    /// classification at the rule site would be a third copy of a fact and a way for two copies to disagree.
    /// </para>
    /// <para>
    /// ORDERING AND AGGREGATION ARE NOT HERE, and cannot be. Both live in the executor, so the order of findings
    /// is a property of the run rather than of which rule happened to be written first. A builder that could sort
    /// would make determinism unprovable.
    /// </para>
    /// <para>
    /// THE FLIP CONDITIONS for adopting a library instead, recorded where a reader is standing when the question
    /// arises: (a) the catalogue grows a large population of per-element value predicates whose rule set is known
    /// at COMPILE time rather than derived from the DTD at runtime, or (b) rules need to become asynchronous.
    /// Neither is true; (a) is the one to watch, since the declarative population is about one row in five.
    /// </para>
    /// </summary>
    public sealed class RuleBuilder
    {
        private readonly ProblemCatalogEntry entry;
        private ConstraintSequence? constraints;
        private ProjectInspection? inspection;

        /// <summary>Starts authoring the rule for this catalogue entry.</summary>
        /// <param name="entry">The entry that classifies the rule and owns its Danish template.</param>
        public RuleBuilder(ProblemCatalogEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            this.entry = entry;
        }

        /// <summary>
        /// Gives the rule a DECLARATIVE body — an ordered, mutually exclusive sequence of value constraints,
        /// evaluated in order and stopping at the first failure.
        /// </summary>
        /// <param name="sequence">The constraints, most fundamental first.</param>
        public RuleBuilder Constrain(ConstraintSequence sequence)
        {
            ArgumentNullException.ThrowIfNull(sequence);
            constraints = sequence;
            return this;
        }

        /// <summary>The single-constraint case, which is the common one.</summary>
        /// <param name="constraint">The one constraint this rule is.</param>
        public RuleBuilder Constrain(IValueConstraint constraint)
        {
            ArgumentNullException.ThrowIfNull(constraint);
            return Constrain(new ConstraintSequence(EquatableArray.Create<IValueConstraint>([constraint])));
        }

        /// <summary>
        /// Gives the rule a TRAVERSAL body — whole-project face only, because a traversal has nothing a dialog
        /// could bind to.
        /// </summary>
        /// <param name="body">Walks the project and reports what it finds.</param>
        public RuleBuilder Inspect(ProjectInspection body)
        {
            ArgumentNullException.ThrowIfNull(body);
            inspection = body;
            return this;
        }

        /// <summary>
        /// Completes the declaration, refusing a rule that is inconsistent on its own: no declared face, both
        /// bodies or neither, a traversal claiming a face it cannot serve, an unknown target, or a code that is
        /// retired or ruled out.
        /// <para>
        /// The checks are the same ones <see cref="RuleSet"/> applies, stated once and shared, so the rule fails
        /// where it is WRITTEN rather than where it is collected. What cannot be checked here is anything needing
        /// the rest of the set — a duplicate code, and whether the catalogue declares this entry at all — which is
        /// why registration still checks.
        /// </para>
        /// </summary>
        /// <exception cref="RuleRegistrationException">The rule is inconsistent on its own terms.</exception>
        public RuleDefinition Build()
        {
            RuleDefinition rule = new(entry, constraints, inspection);
            return RuleSet.LocalFault(rule) is { } fault
                ? throw new RuleRegistrationException(entry.Code, fault)
                : rule;
        }
    }
}
