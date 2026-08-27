#nullable enable
using System;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// Thrown when an operation will not proceed because the project failed validation — before a controller
    /// upload, or before a save that opted into validating first.
    /// <para>
    /// It carries its findings as an AGGREGATE: one head naming the operation that stopped, and N independent
    /// items. That is the second of the two child relationships and NOT the first, and the distinction is
    /// load-bearing. A chain is one failure restated more precisely, so a renderer walks it to the most specific
    /// level; an aggregate is N different failures about N different things, so walking it would show the user
    /// one finding and silently discard the rest. There is deliberately no most-specific member here and no
    /// singular rule id on the head, because either would make that mistake writable.
    /// </para>
    /// <para>
    /// <see cref="Result"/> stays exactly as it was, so every existing caller is unaffected: the aggregate is
    /// beside it, not instead of it.
    /// </para>
    /// </summary>
    public sealed class ProjectValidationException : InvalidOperationException, IProblemCarrier
    {
        /// <summary>Builds the exception for an operation that stopped because validation failed.</summary>
        /// <param name="operation">The operation-level code — which operation will not proceed.</param>
        /// <param name="result">The validation outcome that caused the throw.</param>
        public ProjectValidationException(ProblemCode operation, ProjectValidationResult result)
            : base(BuildMessage(result ?? throw new ArgumentNullException(nameof(result))))
        {
            Result = result;
            Problems = Aggregate(operation, result);
        }

        /// <summary>The complete validation outcome that caused the throw.</summary>
        public ProjectValidationResult Result { get; }

        /// <summary>
        /// The operation and its findings, as one head plus N independent items. Every item is rendered; none is
        /// ever collapsed into the head.
        /// </summary>
        public ProblemAggregate Problems { get; }

        /// <summary>The operation-level code: which operation refused.</summary>
        public ProblemCode Operation => Problems.Head.Code;

        /// <summary>
        /// Null: this refusal is an aggregate, not a chain. Implemented EXPLICITLY because the class already
        /// publishes a <see cref="Problems"/> of the aggregate shape, and two members of that name cannot
        /// coexist — a caller holding the concrete type keeps the aggregate it always had.
        /// </summary>
        ProblemChain? IProblemCarrier.Problems => null;

        /// <inheritdoc/>
        ProblemAggregate? IProblemCarrier.Aggregate => Problems;

        private static ProblemAggregate Aggregate(ProblemCode operation, ProjectValidationResult result)
        {
            ProblemCatalog.Current.TryGet(operation, out ProblemCatalogEntry entry);
            Problem head = new(
                operation,
                entry?.MessageTemplate ?? string.Empty,
                EquatableArray.Create<ProblemArgument>([new ProblemArgument("count", result.Errors.Length)]),
                $"The project failed validation with {result.Errors.Length} error(s).");

            // BOUND, not raw. The count was already computed and already attached; taking the template verbatim
            // discarded it one line later, so a reader was told the save failed and never how much was wrong.
            head = head with { Message = entry?.BindTemplate(head) ?? head.Message };

            // The items are the ERRORS. Warnings never block an operation, so an aggregate explaining why one
            // stopped must not list them: a reader would be given repairs that change nothing about the refusal.
            EquatableArray<Problem> items =
            [
                .. result.Findings
                    .Where(f => f.Severity == ValidationSeverity.Error)
                    // Both sentences travel: the Danish one a user reads, and the English detail beside it. The
                    // finding already carries the pair, so dropping the diagnostic here would lose the only
                    // text that says WHICH attribute or WHICH tag — a Danish label is deliberately short.
                    .Select(f => new Problem(
                        new ProblemCode(f.RuleId), f.Message, EquatableArray<ProblemArgument>.Empty, f.Diagnostic)),
            ];

            return new ProblemAggregate(head, items);
        }

        private static string BuildMessage(ProjectValidationResult result)
        {
            int count = result.Errors.Length;
            string preview = count == 0
                ? string.Empty
                : ": " + string.Join(" | ", result.Errors.Take(5)) + (count > 5 ? $" | … ({count - 5} more)" : string.Empty);
            return $"The project failed validation with {count} error(s){preview}";
        }
    }
}
