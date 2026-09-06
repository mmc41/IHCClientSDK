using System;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// An exception that cannot describe itself: reading its <see cref="Exception.Message"/> throws.
    /// </summary>
    /// <remarks>
    /// <para>The measurement device for one specific claim — that a reporting path does not become the failure
    /// it was reporting. <see cref="Exception.Message"/> is virtual, and every recording path reads it: a span's
    /// status description, an exception event's tags, a fault row's captured detail, and
    /// <see cref="Exception.ToString"/> through all three. Each of those reads happens inside a catch that has
    /// already contained something, so a throw there does not merely lose a row — it replaces the contained
    /// fault with the reporting one, out of a frame whose whole purpose was to stop exactly that.</para>
    /// <para>Shared rather than copied per suite: the same claim is made about the SDK's instrumentation core,
    /// the validator's report-and-continue policy and the shell's contained bookkeeping, and a per-suite copy is
    /// how those three come to be testing subtly different exceptions.</para>
    /// </remarks>
    internal sealed class UnreadableException : Exception
    {
        /// <inheritdoc/>
        public override string Message => throw new InvalidOperationException("the message cannot be read");
    }
}
