#nullable enable
using System;

using Ihc.Vis.Model;

namespace Ihc.Vis.Problems
{
    /// <summary>
    /// What KIND of value an argument slot holds.
    /// <para>
    /// The vocabulary is deliberately data-only: there is no <c>Sentence</c>, <c>Phrase</c> or <c>Label</c>
    /// member, because an argument carrying words of the source language makes the message untranslatable — the
    /// fragment would need translating too, and nothing would know to. The enum makes that defect unrepresentable
    /// rather than merely discouraged. It also says how a value is formatted when a template binds it: a raw
    /// attribute value is quoted, an integer is not.
    /// </para>
    /// </summary>
    public enum ProblemArgumentType
    {
        /// <summary>A <c>.vis</c> element id token (<c>_0x2a</c>) or a parsed <see cref="ElementId"/>.</summary>
        ElementIdentity,

        /// <summary>An XML tag or attribute name — a schema identifier, not prose.</summary>
        SchemaName,

        /// <summary>A user-authored name exactly as it stands in the project.</summary>
        AuthoredName,

        /// <summary>A whole number: a count, a bound, an address, a channel.</summary>
        Integer,

        /// <summary>A non-integral number: a delay in seconds, a fade time.</summary>
        Number,

        /// <summary>A raw attribute value as it stands in the file — <c>readonly</c>, <c>on</c>, a stray glyph.</summary>
        AttributeValue,

        /// <summary>A file-system path or stream name, as given.</summary>
        Path,

        /// <summary>
        /// The identity of a rule or code in this engine — <c>name-empty</c>, <c>io.save</c>. An ENGINE
        /// identifier, not a project one: it names part of the tool, where
        /// <see cref="ElementIdentity"/> names part of the document and <see cref="SchemaName"/> names part of
        /// the file format. Neither of those fits, which is why this is its own kind rather than a reuse.
        /// <para>
        /// It is admitted into a Danish sentence only where the sentence is ABOUT the tool — a rule that failed
        /// — and that is a named exception rather than a precedent. A finding about a project never names the
        /// rule that found it.
        /// </para>
        /// </summary>
        ProblemIdentity,
    }

    /// <summary>One declared argument slot on a message template: what it is called, and what it must hold.</summary>
    /// <param name="Name">The slot name as it appears in the Danish template.</param>
    /// <param name="Type">The kind of value the slot accepts.</param>
    public readonly record struct ProblemArgumentSlot(string Name, ProblemArgumentType Type);

    /// <summary>
    /// One argument VALUE bound to a slot. Held as <see cref="object"/> deliberately: a closed generic hierarchy
    /// would force every construction site through a type switch for no gain, and the slot beside it already
    /// declares the kind.
    /// </summary>
    /// <param name="Name">The slot this value fills.</param>
    /// <param name="Value">The datum — never a word or sentence fragment of the source language.</param>
    public readonly record struct ProblemArgument(string Name, object Value);

    /// <summary>
    /// The uniform coded problem — the ONE value every SDK failure, refusal and finding carries.
    /// <list type="bullet">
    /// <item><description><see cref="Code"/> — stable identity, for filtering, grouping and lookup.</description></item>
    /// <item><description><see cref="Message"/> — the user-facing DANISH text.</description></item>
    /// <item><description><see cref="Arguments"/> — the data the message needs, bound in declared slot order.</description></item>
    /// <item><description><see cref="Diagnostic"/> / <see cref="Cause"/> — the ENGLISH engine detail, never user-facing.</description></item>
    /// </list>
    /// <para>
    /// PUBLIC and host-constructible, because a host must be able to build one for its own <c>app.*</c> codes.
    /// Nothing gates this constructor — not a catalogue, not a registry — or the open vocabulary closes by the
    /// back door.
    /// </para>
    /// <para>
    /// Composition of several problems is a separate concern with its own types, and is deliberately NOT a
    /// nullable child field here: with a cause on this record, "walk to the most detailed child" would compile
    /// against a set of independent problems and silently discard all but one of them.
    /// </para>
    /// <para>
    /// <see cref="Diagnostic"/> and <see cref="Cause"/> sit beside the Danish <see cref="Message"/> rather than
    /// inside a wrapper: that one signature is the language separation this contract exists to enforce, and
    /// translating an English engine sentence is exactly a move from <see cref="Message"/> into
    /// <see cref="Diagnostic"/>, losing nothing.
    /// </para>
    /// </summary>
    /// <param name="Code">The problem's identity.</param>
    /// <param name="Message">
    /// The user-facing Danish text: a SHORT FIXED LABEL following the catalogue convention — <i>Mangler
    /// Id-kode</i>, <i>Ikke forbundet</i> — never a sentence assembled from fragments at render time.
    /// </param>
    /// <param name="Arguments">The values the template's declared slots are bound to, in declared order.</param>
    /// <param name="Diagnostic">The English engine sentence, or null when the label says everything there is.</param>
    /// <param name="Cause">An originating exception, when one exists.</param>
    /// <remarks>
    /// <b>RENDERING RULE, case 1 of 3 — a bare problem.</b> <see cref="Message"/> is shown WHOLE and as it
    /// stands. It is one complete entry on its own, so nothing prefixes it, appends to it, or assembles it from
    /// parts at render time — that is the fragment assembly the fixed-label convention exists to prevent, and it
    /// is what makes a message translatable as one unit. Identity is SUBORDINATE where it is shown at all: a
    /// bracketed suffix or a footnote after the message, never a prefix that displaces it, and per the report
    /// convention never in the report at all. <see cref="Diagnostic"/> is not user-facing and is never rendered
    /// beside <see cref="Message"/>; it goes to the log.
    /// <para>
    /// The two composition types state cases 2 and 3, each beside the shape it is about.
    /// </para>
    /// </remarks>
    public sealed record Problem(
        ProblemCode Code,
        string Message,
        EquatableArray<ProblemArgument> Arguments,
        string? Diagnostic = null,
        Exception? Cause = null)
    {
        /// <summary>The SDK catch-all's code.</summary>
        private const string UnexpectedCode = "internal.unexpected";

        /// <summary>
        /// The catch-all's Danish template, copied here because this layer may not read the catalogue. It names
        /// the OPERATION: the catch-all is the one code that cannot say what went wrong, so without it the
        /// sentence reports a failure and identifies nothing.
        /// </summary>
        private const string UnexpectedTemplate = "Uventet fejl under '{operation}'.";

        /// <summary>The slot <see cref="UnexpectedTemplate"/> declares.</summary>
        private const string OperationSlot = "operation";

        /// <summary>
        /// The named SDK catch-all, <c>internal.unexpected</c>: the Danish sentence with the English engine text
        /// as <see cref="Diagnostic"/>. THE only factory on this type — every other code's factory is generated
        /// from its catalogue entry, so a hand-written one here would be a second way to spell one code.
        /// <para>
        /// The template is bound through the SHARED binder, not by a substitution written here, so this sentence
        /// cannot come to be assembled by different rules from the one the catalogue entry uses.
        /// </para>
        /// </summary>
        /// <param name="operation">The operation the fault was raised under — an engine identifier.</param>
        /// <param name="diagnostic">The English engine sentence describing what went wrong.</param>
        /// <param name="cause">The originating exception, when one exists.</param>
        public static Problem Unexpected(string operation, string diagnostic, Exception? cause = null)
        {
            EquatableArray<ProblemArgument> arguments =
                EquatableArray.Create<ProblemArgument>([new ProblemArgument(OperationSlot, operation)]);
            return new Problem(
                new ProblemCode(UnexpectedCode), ProblemTemplate.Bind(UnexpectedTemplate, arguments), arguments,
                diagnostic, cause);
        }

        /// <inheritdoc/>
        public override string ToString() =>
            Diagnostic is null ? $"{Code}: {Message}" : $"{Code}: {Message} ({Diagnostic})";
    }

    /// <summary>
    /// Where a fault was OBSERVED. Declared rather than derived: <see cref="ProblemCode.Family"/> separates an
    /// SDK code from a host's, but a fault in the machine underneath — a graphics driver, a clipboard, a GLib
    /// main loop — reaches the application through a HOST code, so reading the origin back off the code would
    /// collapse <see cref="Platform"/> into <see cref="Host"/> and lose the one distinction a support query
    /// starts from.
    /// </summary>
    public enum InternalErrorOrigin
    {
        /// <summary>The SDK itself: a rule that threw, an edit that broke, an operation that faulted.</summary>
        Sdk,

        /// <summary>The application above it.</summary>
        Host,

        /// <summary>The machine underneath — the OS, a driver, a toolkit's native boundary.</summary>
        Platform,
    }

    /// <summary>
    /// A fault in the TOOL, as distinct from a finding about the project. Declared here, in the SDK, so the SDK's
    /// own rule-crash path and a host's catch-all produce the SAME value rather than the host converting one into
    /// the other.
    ///
    /// <para>
    /// <b>It carries no category, no severity, no refused operations and no location</b>, and that absence is the
    /// entire point of the type. Every one of those is a statement about project CONTENT, and a crashed rule says
    /// nothing about the project it failed to examine. Making this a <c>ValidationFinding</c> with a special
    /// category would have forced each of them to be answered with a lie.
    /// </para>
    /// </summary>
    /// <param name="Code">The problem's identity, from the catalogue.</param>
    /// <param name="Message">The user-facing DANISH text, whole, as the catalogue entry words it.</param>
    /// <param name="Diagnostic">The ENGLISH engine sentence for the log, or null when the label says it all.</param>
    /// <param name="Origin">Where the fault was observed — see <see cref="InternalErrorOrigin"/>.</param>
    /// <param name="Detail">
    /// The captured exception text — message and stack — as an immutable STRING, never the
    /// <see cref="Exception"/> itself. This is the load-bearing choice: an exception on this record would put
    /// <c>Message</c>, <c>StackTrace</c> and <c>ToString</c> within reach of the presentation layer, which is
    /// exactly the leak the exception-message scan exists to pin — and the scan would have to grow an exemption
    /// for a PRESENTATION site to allow it. Capturing once, at the raise site, keeps the read count at one and
    /// hands presentation an opaque string it cannot misuse.
    /// </param>
    /// <param name="Observed">
    /// When this fault was observed. Internal errors accumulate across a session rather than belonging to a
    /// validation run, so they need a stamp of their own.
    /// <para>
    /// For every raise site but one, observed and thrown are the same moment. The exception is the
    /// unobserved-task layer: <c>TaskScheduler.UnobservedTaskException</c> fires on the finalizer thread, an
    /// arbitrary time after the fault, so what this records there is the DISCOVERY time. No timestamp can fix
    /// that layer — the throw time is simply not available to it — which makes a fault discovered after a
    /// different project was loaded a known limitation rather than something a comparison here can detect.
    /// Supervising the tasks that would otherwise go unobserved is the lever that bounds it, not this field.
    /// </para>
    /// </param>
    public sealed record InternalError(
        ProblemCode Code,
        string Message,
        string? Diagnostic,
        InternalErrorOrigin Origin,
        string Detail,
        DateTimeOffset Observed)
    {
        /// <summary>
        /// Projects an already-bound <see cref="Problem"/> onto a fault row. THE door: a raise site that spells
        /// the projection out itself is free to swap <see cref="Message"/> for <see cref="Diagnostic"/> — the
        /// Danish sentence for the English one — and free to forget the stamp, and neither mistake shows at the
        /// call site.
        /// </summary>
        /// <param name="problem">The problem to record, already bound to its Danish sentence.</param>
        /// <param name="origin">Which layer observed it.</param>
        /// <param name="detail">The captured technical text, including where it was observed.</param>
        public static InternalError From(Problem problem, InternalErrorOrigin origin, string detail) =>
            new(problem.Code, problem.Message, problem.Diagnostic, origin, detail, DateTimeOffset.UtcNow);
    }
}
