using System;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis
{
    /// <summary>
    /// Thrown when the controller declines to store an uploaded project (the SOAP operation returned
    /// <c>false</c> after change mode was already entered). The controller's project state is uncertain at
    /// that point, so the failure must surface as an exception rather than an easily-ignored return value.
    /// <para>
    /// It keeps its own type rather than becoming a generic refusal: a declined upload is the one failure whose
    /// aftermath is a controller in an unknown state, and callers that already catch this type are catching
    /// exactly that. The coded identity was added to it, not instead of it.
    /// </para>
    /// </summary>
    public sealed class ProjectUploadException : InvalidOperationException, IProblemCarrier
    {
        public ProjectUploadException(string message) : base(message)
        {
        }

        /// <summary>Refuses the upload, naming the operation and its cause.</summary>
        /// <param name="identity">The operation and cause, with the Danish words for each.</param>
        /// <param name="diagnostic">The English sentence for the log.</param>
        public ProjectUploadException(RefusalIdentity identity, string diagnostic)
            : base(diagnostic)
        {
            Problems = new ProblemChain(
                new Problem(identity.Operation, identity.OperationLabel,
                    EquatableArray<ProblemArgument>.Empty, diagnostic),
                new Problem(identity.Cause, identity.CauseLabel,
                    EquatableArray<ProblemArgument>.Empty, diagnostic));
        }

        /// <inheritdoc/>
        public ProblemChain? Problems { get; }
    }
}
