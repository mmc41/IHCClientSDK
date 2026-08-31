#nullable enable
using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The <c>internal.*</c> family: faults in the TOOL, governed like any other code.
    ///
    /// <para><b>Why these have a file of their own.</b> They sit in the operation-outcomes SECTION, because a row
    /// with no category must, but they are not operation outcomes: an operation outcome is the disposition of
    /// something the user asked for, and these are the disposition of the software failing to do it. Leaving them
    /// among <c>io.load</c> and <c>edit.open</c> put a differently-kinded row in a list a reader scans for one
    /// kind, which is the thing a partial split exists to prevent.</para>
    ///
    /// <para><b>Every row here declares <see cref="RuleKind.InternalFault"/> and
    /// <see cref="CatalogDisposition.NotApplicable"/>, and the invariants hold it to BOTH.</b> A fault row makes
    /// no statement about the project: no category, no face, no refused operation, no severity. It is not a
    /// refusal either — a refusal says an operation did not happen, while a crashed rule carries the validation
    /// pass through, minus whatever that rule would have added.</para>
    ///
    /// <para><b>A NAMED EXCEPTION, not a precedent: these Danish sentences admit an ENGINE IDENTIFIER.</b> Every
    /// other user-facing sentence in this catalogue names things in the installer's world — a locality, a
    /// terminal, a file. These name a rule and an operation, which are parts of the tool. That is admissible here
    /// for one reason only: the sentence is ABOUT the tool, and a reader who is being told the tool failed can
    /// act on which part failed. A finding about a project must never name the rule that found it, and nothing
    /// here licenses that.</para>
    /// </summary>
    internal static partial class ProblemCatalogEntries
    {
        /// <summary>
        /// A validation rule threw. The findings list is INCOMPLETE by exactly what that rule would have added,
        /// and the sentence says so as well as naming the rule — the hedge is load-bearing, because a rule may
        /// have emitted findings before it threw, so "the list may be missing errors" is the strongest true
        /// statement available.
        /// PREDICATE: none — it is raised by the executor that caught the throw, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry InternalRuleFailed =>
            new ProblemCatalogEntry(
                new ProblemCode("internal.rule-failed"),
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.NotApplicable,
                RuleKind.InternalFault,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("rule", ProblemArgumentType.ProblemIdentity)]),
                "Valideringsreglen '{rule}' fejlede. Listen kan mangle fejl.")
            {
                Diagnostic = "Rule '{rule}' threw during a validation pass; its findings are missing from the run.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The SDK catch-all. Whatever failed, this is what the installer is told when the engine has nothing
        /// more specific to say, with the English diagnostic going to the log.
        /// <para>
        /// It names the OPERATION it was raised under, because the catch-all is the one code that cannot say what
        /// went wrong: without the operation it is a sentence that reports a failure and identifies nothing.
        /// </para>
        /// PREDICATE: none — it is raised, never detected.
        /// </summary>
        private static ProblemCatalogEntry InternalUnexpected =>
            new ProblemCatalogEntry(
                new ProblemCode("internal.unexpected"),
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.NotApplicable,
                RuleKind.InternalFault,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("operation", ProblemArgumentType.ProblemIdentity)]),
                "Uventet fejl under '{operation}'.")
            {
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// An edit that PASSED its preconditions faulted inside the engine, so nothing was committed. Distinct
        /// from every <c>edit.*</c> refusal: those are the rules working, and this is the engine breaking.
        /// PREDICATE: none — it is raised by the session's final catch, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry InternalEditFailed =>
            new ProblemCatalogEntry(
                new ProblemCode("internal.edit-failed"),
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.NotApplicable,
                RuleKind.InternalFault,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Redigeringen kunne ikke gennemføres på grund af en intern fejl. Ændringen blev ikke gemt.")
            {
                Diagnostic = "An accepted edit ended in an engine fault; nothing was committed.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// PREVIEWING a command faulted inside the engine. Its own code rather than the edit's: a preview
        /// commits nothing, so the two say different things about what the project is now — "the change was not
        /// saved" is false here, because there was never going to be a change to save.
        /// PREDICATE: none — it is raised by the session's preview catch, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry InternalPreviewFailed =>
            new ProblemCatalogEntry(
                new ProblemCode("internal.preview-failed"),
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.NotApplicable,
                RuleKind.InternalFault,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Handlingen kunne ikke vurderes på grund af en intern fejl. Projektet er uændret.")
            {
                Diagnostic = "Previewing a command ended in an engine fault; nothing was previewed or committed.",
                Evidence = EvidenceMark.Unknown,
            };

        private static ProblemCatalogEntry[] InternalFaults =>
        [
            InternalEditFailed,
            InternalPreviewFailed,
            InternalRuleFailed,
            InternalUnexpected,
        ];
    }
}
