#nullable enable
using System;
using System.IO;

using Ihc.Vis.Model;

namespace Ihc.Vis.Problems
{
    /// <summary>
    /// An exception that carries a coded refusal. ONE catch shape for a frontend — <c>catch (Exception ex) when
    /// (ex is IProblemCarrier c)</c> — across refusals whose base types differ because their callers' do.
    /// <para>
    /// The base type is not free to choose. A failed write must stay an <see cref="IOException"/> and a schema
    /// guard must stay an <see cref="InvalidOperationException"/>, because that is what every existing caller
    /// already catches; changing either to a single common base would make coded refusals a breaking change
    /// dressed as an improvement. The identity therefore travels on an interface, which any base can carry.
    /// </para>
    /// </summary>
    public interface IProblemCarrier
    {
        /// <summary>
        /// The operation and its one cause, when the refusal was raised with an identity; <c>null</c> on the
        /// diagnostic-only paths that have not been given codes yet.
        /// </summary>
        ProblemChain? Problems { get; }

        /// <summary>
        /// The head and its N independent items, when the refusal is an AGGREGATE rather than a chain; <c>null</c>
        /// for every refusal that is one failure with one cause — which is all of them but the validation one.
        /// <para>
        /// It is a DEFAULTED member and the interface was widened rather than split. A sibling interface would
        /// have meant two type tests at every catch site, and forgetting the second reproduces the very bug this
        /// fixes: a validation refusal falling through the coded path to its English exception message. Defaulting
        /// keeps every existing implementer — the five sealed ones here and any outside this assembly — compiling
        /// untouched, so widening the contract costs nobody a change.
        /// </para>
        /// <para>
        /// A carrier answers ONE of the two. The shapes are not interchangeable: rendering an aggregate as a chain
        /// discards all but one of N findings, and rendering a chain as an aggregate shows one failure twice.
        /// </para>
        /// </summary>
        ProblemAggregate? Aggregate => null;
    }

    /// <summary>
    /// A refused operation whose callers catch <see cref="InvalidOperationException"/>: the serializer's schema
    /// guards and the write self-check. The <see cref="Exception.Message"/> stays the ENGLISH diagnostic it has
    /// always been — the Danish sentence is the cause problem's, and a frontend renders that one.
    /// </summary>
    public sealed class RefusedOperationException : InvalidOperationException, IProblemCarrier
    {
        /// <summary>Refuses an operation, naming which operation and what caused it.</summary>
        /// <param name="identity">The operation and cause, with the Danish words for each.</param>
        /// <param name="diagnostic">The English sentence for the log.</param>
        /// <param name="innerException">An originating exception, when one exists.</param>
        public RefusedOperationException(RefusalIdentity identity, string diagnostic, Exception? innerException = null)
            : base(diagnostic, innerException)
        {
            Problems = RefusalChain.Build(identity, diagnostic, innerException);
        }

        /// <inheritdoc/>
        public ProblemChain? Problems { get; }
    }

    /// <summary>
    /// A refused write whose callers catch <see cref="IOException"/>: the atomic save's destination checks. It
    /// stays an <see cref="IOException"/> deliberately — a caller that already handles "the file could not be
    /// written" keeps working, and gains the code only if it looks for one.
    /// </summary>
    public sealed class RefusedWriteException : IOException, IProblemCarrier
    {
        /// <summary>Refuses a write, naming which operation and what caused it.</summary>
        /// <param name="identity">The operation and cause, with the Danish words for each.</param>
        /// <param name="diagnostic">The English sentence for the log.</param>
        /// <param name="innerException">An originating exception, when one exists.</param>
        public RefusedWriteException(RefusalIdentity identity, string diagnostic, Exception? innerException = null)
            : base(diagnostic, innerException)
        {
            Problems = RefusalChain.Build(identity, diagnostic, innerException);
        }

        /// <inheritdoc/>
        public ProblemChain? Problems { get; }
    }

    /// <summary>
    /// A refused read of a catalog definition file: the bytes are not a definition this tool can take in.
    /// <para>
    /// THE ONE PLACE THE BASE TYPE HAD TO CHANGE. This condition used to throw <see cref="InvalidDataException"/>,
    /// which the BCL SEALS — so it cannot be specialized, and an identity could not be attached to it at all
    /// without changing what is thrown. <see cref="FormatException"/> is the base chosen because it is what the
    /// same condition already uses on the project side (a malformed <c>.vis</c> is a
    /// <c>ProjectFormatException : FormatException</c>), so the two file kinds now fail alike instead of
    /// differing for no reason a caller could name.
    /// </para>
    /// </summary>
    public sealed class RefusedImportException : FormatException, IProblemCarrier
    {
        /// <summary>Refuses a read, naming which operation and what caused it.</summary>
        /// <param name="identity">The operation and cause, with the Danish words for each.</param>
        /// <param name="diagnostic">The English sentence for the log.</param>
        /// <param name="innerException">An originating exception, when one exists.</param>
        public RefusedImportException(RefusalIdentity identity, string diagnostic, Exception? innerException = null)
            : base(diagnostic, innerException)
        {
            Problems = RefusalChain.Build(identity, diagnostic, innerException);
        }

        /// <inheritdoc/>
        public ProblemChain? Problems { get; }
    }

    /// <summary>The one place a refusal identity becomes a chain, so every carrier composes it identically.</summary>
    internal static class RefusalChain
    {
        public static ProblemChain Build(RefusalIdentity identity, string diagnostic, Exception? innerException) =>
            new(new Problem(identity.Operation, identity.OperationLabel,
                    EquatableArray<ProblemArgument>.Empty, diagnostic),
                new Problem(identity.Cause, identity.CauseLabel,
                    EquatableArray<ProblemArgument>.Empty, diagnostic, innerException));
    }
}
