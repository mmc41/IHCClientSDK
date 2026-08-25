#nullable enable
using System;
using System.Collections.Generic;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The catalogue's three-letter category codes, as data beside the enum rather than as member names.
    /// <para>
    /// A call site reads <c>category.ShortCode</c> and <c>ValidationCategory.TryParseShortCode(…)</c>, so nothing
    /// has to know this class exists. That is also why the codes are not member names: a reader of the code meets
    /// <c>Addressing</c>, and only the rendered table meets <c>ADR</c>.
    /// </para>
    /// </summary>
    public static class CategoryExtensions
    {
        private static readonly Dictionary<ValidationCategory, string> Codes = new()
        {
            [ValidationCategory.FileIntegrity] = "INT",
            [ValidationCategory.Wiring] = "WIR",
            [ValidationCategory.Logic] = "LOG",
            [ValidationCategory.Scenes] = "SCN",
            [ValidationCategory.Addressing] = "ADR",
            [ValidationCategory.DeviceSettings] = "DEV",
            [ValidationCategory.Documentation] = "DOC",
            [ValidationCategory.ProjectStructure] = "PRJ",
        };

        extension(ValidationCategory category)
        {
            /// <summary>The catalogue short code for this category (<c>INT</c>, <c>WIR</c>, …).</summary>
            public string ShortCode => Codes.TryGetValue(category, out string? code) ? code : string.Empty;
        }

        extension(ValidationCategory)
        {
            /// <summary>The reverse lookup, for reading a stored or rendered short code.</summary>
            /// <param name="shortCode">The three-letter code, compared ordinally.</param>
            /// <param name="category">The category it names, when it names one.</param>
            public static bool TryParseShortCode(string shortCode, out ValidationCategory category)
            {
                foreach ((ValidationCategory candidate, string code) in Codes)
                {
                    if (string.Equals(code, shortCode, StringComparison.Ordinal))
                    {
                        category = candidate;
                        return true;
                    }
                }

                category = default;
                return false;
            }
        }
    }

    /// <summary>
    /// What a catalogue row COSTS — one axis, three values, stated once.
    /// <para>
    /// The insight that keeps this to three: "fatal" was carrying two unrelated meanings. One is <b>the operation
    /// cannot proceed</b> — that is <see cref="Refusal"/>. The other is <b>this is catastrophic in effect</b> — a
    /// dangling IDREF, a 24-bit id wrap — which is still an ordinary <see cref="Error"/> finding, because the file
    /// opens and the user must be able to repair it.
    /// </para>
    /// <para>
    /// A row that BOTH refuses an operation and reports a finding — an undeclared attribute refuses the save and
    /// is an Error at validate — is <see cref="Error"/> here. Its refusal comes from the operation's own entry,
    /// with this row as the cause, which is why the disposition axis needs no fourth value.
    /// </para>
    /// </summary>
    public enum CatalogDisposition
    {
        /// <summary>
        /// Wrong regardless of what the author intended. Reported as an <see cref="ValidationSeverity.Error"/>
        /// finding. This is also where a catastrophic-but-openable condition lands.
        /// </summary>
        Error,

        /// <summary>May or may not be a mistake — only the author can judge. Reported as a Warning finding.</summary>
        Warning,

        /// <summary>
        /// The operation cannot be carried through: nothing is opened, written or overwritten. Realised as a coded
        /// refusal and NEVER as a finding — so a refusal has no severity, and no severity means "refused".
        /// <para>
        /// This axis has three values while <see cref="ValidationSeverity"/> now has three FINDING tiers
        /// (Error, Warning, Info). They are not the same three and do not line up: Info is a severity a finding
        /// can carry, and until a row here can DECLARE it, no catalogue entry produces one.
        /// </para>
        /// </summary>
        Refusal,
    }

    /// <summary>
    /// The catalogue's READINESS mark — metadata, never a gate: unmarked rows are implemented like any other.
    /// </summary>
    public enum EvidenceMark
    {
        /// <summary>Nothing is recorded about how this row was established.</summary>
        Unknown,

        /// <summary>
        /// The condition is evidenced as REACHABLE — either produced against the live vendor tool, or observed in
        /// a real installation by an outside report. The catalogue distinguishes those two marks in its prose; the
        /// distinction changes nothing a rule does, so it is not an axis here.
        /// </summary>
        Authored,

        /// <summary>The vendor tool would not let the condition be authored, so the state arrives only by import or by hand.</summary>
        Refused,
    }

    /// <summary>Which section of the catalogue an entry belongs to.</summary>
    public enum ProblemCatalogSection
    {
        /// <summary>A condition about a PROJECT (<c>.vis</c>).</summary>
        ProjectFindings,

        /// <summary>A condition about a CATALOG DEFINITION file (<c>.def</c>/<c>.ifb</c>).</summary>
        CatalogDefinitionFindings,

        /// <summary>
        /// An OPERATION OUTCOME — the dotted family heads (<c>io.load</c>, <c>bridge.download</c>,
        /// <c>internal.unexpected</c>). Every one is a code, and a code with no entry cannot be governed, so
        /// without this section the operation-plus-cause composition could not be built at all.
        /// <para>
        /// Also the section where <see cref="ProblemCatalogEntry.Category"/> is null: the eight categories classify
        /// project CONTENT and a firmware-too-old outcome has no honest one. A ninth category was rejected — it
        /// would put a non-content value on the axis the report groups by.
        /// </para>
        /// </summary>
        OperationOutcomes,
    }

    /// <summary>How well-founded a threshold is. The three answers genuinely differ, and the difference is read.</summary>
    public enum ThresholdConfidence
    {
        /// <summary>A HARD limit from a vendor datasheet or the tool's own bounds (8 terminals, 128 addresses).</summary>
        VendorDocumented,

        /// <summary>
        /// A vendor RECOMMENDATION, not a hard limit — at most 64 wireless products, stated for response-time
        /// reasons. This is why such a row is a Warning: an Error's consequence must hold whatever the author
        /// intended, and a slow-but-working system does not qualify.
        /// </summary>
        VendorRecommendation,

        /// <summary>
        /// AUTHORED here because no source states a number. Carries its unconfirmed status in
        /// <see cref="DeclaredThreshold.Evidence"/> and as a TODO at the point of use, so the guess is visible in
        /// the code rather than only in a planning document.
        /// </summary>
        Authored,
    }

    /// <summary>
    /// A threshold as DATA, never a literal in a rule body. A number written inline is invisible to review, cannot
    /// be cited, and cannot change without a code change.
    /// <para>
    /// This is the one part of a row's predicate specification that stays a TYPE. The prose parts — the condition,
    /// the scope it walks, the exclusions — are the doc-comment on the declaration, which is already beside the row
    /// and reviewable in the same diff, and cannot be mistaken for behaviour the way an unread string field can.
    /// Thresholds are different because code READS them.
    /// </para>
    /// </summary>
    /// <param name="Name">What the number means, referenced by the entry's predicate comment.</param>
    /// <param name="Value">The number itself.</param>
    /// <param name="Confidence">How well-founded it is.</param>
    /// <param name="Evidence">The citation, or the explicit unconfirmed note for an authored one.</param>
    public sealed record DeclaredThreshold(
        string Name,
        double Value,
        ThresholdConfidence Confidence,
        string Evidence);
}
