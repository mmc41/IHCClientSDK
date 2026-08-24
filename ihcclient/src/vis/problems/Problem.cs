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

        /// <summary>The catch-all's fixed Danish label. The English detail travels in <see cref="Diagnostic"/>.</summary>
        private const string UnexpectedLabel = "Uventet fejl";

        /// <summary>
        /// The named SDK catch-all, <c>internal.unexpected</c>: a fixed Danish label with the English engine text
        /// as <see cref="Diagnostic"/>. THE only factory on this type — every other code's factory is generated
        /// from its catalogue entry, so a hand-written one here would be a second way to spell one code.
        /// </summary>
        /// <param name="diagnostic">The English engine sentence describing what went wrong.</param>
        /// <param name="cause">The originating exception, when one exists.</param>
        public static Problem Unexpected(string diagnostic, Exception? cause = null) =>
            new(new ProblemCode(UnexpectedCode), UnexpectedLabel, EquatableArray<ProblemArgument>.Empty,
                diagnostic, cause);

        /// <inheritdoc/>
        public override string ToString() =>
            Diagnostic is null ? $"{Code}: {Message}" : $"{Code}: {Message} ({Diagnostic})";
    }
}
