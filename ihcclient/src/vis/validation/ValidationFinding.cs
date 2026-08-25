#nullable enable
using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// WHERE a finding is. Two anchors, because <see cref="ElementId"/> cannot be the universal one: a MALFORMED
    /// id cannot be parsed and the finding is about the malformation; a DUPLICATE id resolves to two elements, so
    /// it does not identify a site; a WHOLE-TREE finding has several sites or none.
    /// <para>
    /// So <see cref="Locator"/> — the raw token or tag, exactly as the existing collector produces it — is the
    /// always-available anchor, and <see cref="Element"/> is the parsed convenience for when the id is well-formed
    /// and unambiguous.
    /// </para>
    /// <para>
    /// There is no source-position anchor. Every pre-parse fault — byte-order mark, declared encoding, not-XML,
    /// truncation — is a REFUSAL, and a refusal produces a <see cref="Problem"/> rather than a finding, so a
    /// position could never reach this type. Where a byte offset must reach a user it is a declared
    /// <see cref="ProblemArgument"/> on the refusal.
    /// </para>
    /// </summary>
    /// <param name="Locator">The raw <c>_0x</c> id token, else the element tag. Always present.</param>
    /// <param name="Element">
    /// The parsed id, when the token is well-formed — and NOT a promise that one element answers to it. A
    /// duplicate token parses like any other, so every site of a collision carries the same non-null id here and
    /// a consumer that resolves one has to decide what to do about the second. (It cannot be decided here: how
    /// many elements carry a token is a fact about the tree, and this type is about the finding.)
    /// </param>
    /// <param name="Message">
    /// This location's OWN Danish text, for a related location that needs to say why it is listed. Null when the
    /// finding's own message says everything. This slot is what makes a duplicate-id group ONE navigable finding
    /// instead of N.
    /// </param>
    public sealed record FindingLocation(string? Locator, ElementId? Element, string? Message);

    /// <summary>
    /// One finding, as the engine produces it: a <see cref="Problem"/> plus the classification and location that
    /// make it actionable.
    /// <para>
    /// Note the COMPOSITION: it does not restate the problem's code, message or arguments — it CARRIES the
    /// problem. That is what lets one problem value travel from a dialog refusal to a report row without being
    /// rebuilt, and why the problem contract is a namespace of its own rather than a member of the validation
    /// types. A finding IS its problem plus where and how bad.
    /// </para>
    /// <para>
    /// There is no producer marker. One existed so a GUI could present a commit-time refusal differently from a
    /// report row — but a commit-time refusal is a different type off a different method, so the caller already
    /// knows which it asked for. Its only test asserted that nothing branched on it; not having it makes that
    /// property structural instead of asserted.
    /// </para>
    /// </summary>
    /// <param name="Problem">The coded problem — identity, Danish message, arguments, English diagnostic.</param>
    /// <param name="Severity">
    /// Error, Warning or Info. One blocking tier and two advisory ones: the third value widens the ADVISORY end
    /// of the scale — for a host that presents findings and must tell "you should fix this" from "you may care
    /// about this" — and never the blocking end. A refusal is still not a finding: it is a
    /// <see cref="Problem"/> off a different method, so no severity here ever means "refused".
    /// </param>
    /// <param name="Category">
    /// The eight-category classification. NON-NULLABLE, unlike on a catalogue entry: a finding only ever comes
    /// from content the eight categories do classify, so the type the report groups by never has to answer "what
    /// if there is no category?".
    /// </param>
    /// <param name="Primary">The main site, or null for a finding about the project as a whole.</param>
    /// <param name="Related">
    /// Further sites, each with its own locator and message slot. Non-empty only for
    /// <see cref="FindingShape.PrimaryWithRelated"/>.
    /// </param>
    public sealed record ValidationFinding(
        Problem Problem,
        ValidationSeverity Severity,
        ValidationCategory Category,
        FindingLocation? Primary,
        EquatableArray<FindingLocation> Related)
    {
        /// <summary>
        /// The finding's identity, read from the problem it carries rather than stored beside it — so the code a
        /// consumer filters by and the code the message belongs to cannot become two different things.
        /// </summary>
        public ProblemCode Code => Problem.Code;
    }
}
