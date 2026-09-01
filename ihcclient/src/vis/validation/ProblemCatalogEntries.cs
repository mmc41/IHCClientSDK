
using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The assembly point for the catalogue's declarations. Internal on purpose: <see cref="ProblemCatalog"/> is
    /// the one public door, so nothing outside the SDK can hold a half-built entry set or a section in isolation.
    /// <para>
    /// The OPERATION-OUTCOMES section is deliberately near-empty at this point. Its members are the dotted family
    /// heads — <c>io.load</c>, <c>io.save</c>, <c>import.catalog</c>, <c>bridge.download</c>,
    /// <c>bridge.upload</c> — and each is introduced by the work that gives that operation a coded outcome, so
    /// that a code and its refusal site arrive together rather than a code arriving first with nothing behind it.
    /// The <c>internal.*</c> family used to sit here too; it has a file of its own now, because a fault in the
    /// tool is not the disposition of an operation the user asked for.
    /// </para>
    /// </summary>
    internal static partial class ProblemCatalogEntries
    {
        /// <summary>
        /// A CATALOG DEFINITION FILE that will not be taken in. The head over the cause that says why — one head
        /// for both the runtime import and the install-directory scan, because both are the same act (reading a
        /// definition file into the catalogue) and a user told "the catalogue could not be read" does not care
        /// which one asked.
        /// PREDICATE: none — it is the disposition of an operation.
        /// </summary>
        private static ProblemCatalogEntry ImportCatalogOperation =>
            new ProblemCatalogEntry(
                OperationCodes.ImportCatalog,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                OperationCodes.ImportCatalogLabel)
            {
                Diagnostic = "A catalog definition file could not be read; nothing was taken from it.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A DOWNLOAD from the controller that will not complete.
        /// PREDICATE: none — it is the disposition of an operation.
        /// </summary>
        private static ProblemCatalogEntry BridgeDownloadOperation =>
            new ProblemCatalogEntry(
                OperationCodes.BridgeDownload,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                OperationCodes.BridgeDownloadLabel)
            {
                Diagnostic = "The project could not be fetched from the controller.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An UPLOAD to the controller that will not complete. The controller is the one sink with no <c>.BAK</c>
        /// to roll back to, which is why its refusal is an outcome of its own rather than a save that failed.
        /// PREDICATE: none — it is the disposition of an operation.
        /// </summary>
        private static ProblemCatalogEntry BridgeUploadOperation =>
            new ProblemCatalogEntry(
                OperationCodes.BridgeUpload,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                OperationCodes.BridgeUploadLabel)
            {
                Diagnostic = "The controller did not store the uploaded project.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An EDIT SESSION that will not open because the document carries something an edit could not survive —
        /// today, an attribute no schema declares. The head of a cause/detail pair; the cause keeps its own
        /// published id.
        /// PREDICATE: none — it is the disposition of an operation, not a condition detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditOpenRefused =>
            new ProblemCatalogEntry(
                OperationCodes.EditOpen,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                OperationCodes.EditOpenLabel)
            {
                Diagnostic = "The document cannot be opened for editing; the cause carries which condition stopped it.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A SAVE that will not proceed because the project failed validation. The head of an aggregate: the
        /// errors are its items, and each keeps its own catalogue id.
        /// PREDICATE: none — it is the disposition of an operation, not a condition detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry IoSaveValidationFailed =>
            new ProblemCatalogEntry(
                OperationCodes.Save,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("count", ProblemArgumentType.Integer),
                ]),
                OperationCodes.SaveLabel)
            {
                Diagnostic = "The save was abandoned because the project failed validation; nothing was written.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A CATALOG DEFINITION that will not be produced because it failed its own checks. Its family is the
        /// import one because a definition file is what that family is about — authoring one and importing one
        /// are the same subject seen from two sides.
        /// PREDICATE: none — it is the disposition of an operation.
        /// </summary>
        private static ProblemCatalogEntry ImportDefinitionInvalid =>
            new ProblemCatalogEntry(
                new ProblemCode("import.definition-invalid"),
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Definitionen kunne ikke bygges")
            {
                Diagnostic = "The definition was not produced because it failed its own consistency checks.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An OPEN that will not proceed. The head of a cause/detail pair: the one condition that stopped it is
        /// its cause, and that cause keeps the bare catalogue id it was published under.
        /// PREDICATE: none — it is the disposition of an operation.
        /// </summary>
        private static ProblemCatalogEntry IoLoad =>
            new ProblemCatalogEntry(
                Ihc.Vis.Io.LoadRefusalCodes.Operation,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Projektet kunne ikke åbnes")
            {
                Diagnostic = "The open was refused; nothing was opened.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>Every operation-outcome declaration, in code order.</summary>
        private static ProblemCatalogEntry[] OperationOutcomes =>
        [
            BridgeDownloadOperation,
            BridgeUploadOperation,
            ImportCatalogOperation,
            ImportDefinitionInvalid,
            IoLoad,
            IoSaveValidationFailed,
            EditOpenRefused,
        ];

        /// <summary>Every declaration in every section — what <see cref="ProblemCatalog.Current"/> is built from.</summary>
        internal static EquatableArray<ProblemCatalogEntry> All =>
            [.. ProjectFindings, .. CatalogDefinitionFindings, .. OperationOutcomes, .. EditRefusals,
             .. InternalFaults];
    }
}
