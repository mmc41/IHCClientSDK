using System;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Io
{
    /// <summary>
    /// Thrown by <see cref="ProjectAppService.Load(System.IO.Stream)"/>/<see cref="ProjectReader"/> when the
    /// input is not a loadable <c>.vis</c>/<c>.ihc</c> project: empty or compressed data, a BOM or wrong
    /// declared encoding, malformed XML, a non-<c>utcs_project</c> root, character data the attribute-only
    /// model cannot represent, or a malformed inline DTD. One typed catch for a GUI's "could not open this
    /// file" path, always carrying enough context (position, element, excerpt) to act on.
    /// </summary>
    public sealed class ProjectFormatException : FormatException, IProblemCarrier
    {
        public ProjectFormatException(string message) : base(message)
        {
        }

        public ProjectFormatException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Refuses an operation, naming WHICH operation and WHAT caused it.
        /// <para>
        /// The two travel as a cause/detail pair rather than as one blended message: the operation carries the
        /// dotted family code and the cause keeps the bare catalogue id it was published under, so a caller can
        /// tell an open apart from a save without reading the sentence, and the row that explains it keeps the id
        /// anyone filtering on it already knows.
        /// </para>
        /// <para>
        /// The exception's own <see cref="Exception.Message"/> stays the ENGLISH diagnostic it has always been —
        /// that is what a developer reads in a log. The Danish sentence is the cause problem's message, and a
        /// frontend shows that one.
        /// </para>
        /// <para>
        /// The identity is taken WHOLE rather than as loose parts. An earlier signature took the operation as a
        /// <see cref="ProblemCode"/> parameter and then paired it with a hard-coded <c>io.load</c> label, so any
        /// caller naming a different operation would have produced a chain whose code said one thing and whose
        /// Danish sentence said another. A <see cref="RefusalIdentity"/> carries both halves of both ends, so the
        /// pairing cannot come apart.
        /// </para>
        /// </summary>
        /// <param name="refusal">The operation being refused and the condition that caused it, with both labels.</param>
        /// <param name="diagnostic">The English sentence for the log.</param>
        /// <param name="innerException">An originating exception, when one exists.</param>
        public ProjectFormatException(
            RefusalIdentity refusal,
            string diagnostic,
            Exception? innerException = null)
            : base(diagnostic, innerException)
        {
            Problems = new ProblemChain(
                new Problem(refusal.Operation, refusal.OperationLabel, EquatableArray<ProblemArgument>.Empty, diagnostic),
                new Problem(refusal.Cause, refusal.CauseLabel, EquatableArray<ProblemArgument>.Empty, diagnostic,
                    innerException));
        }

        /// <inheritdoc/>
        public ProblemChain? Problems { get; }
    }
}
