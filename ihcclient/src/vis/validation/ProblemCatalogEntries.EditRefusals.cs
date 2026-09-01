using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Session;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The <c>edit.*</c> family: every refusal the session layer can give, governed like any other code.
    /// <para>
    /// Session refusals are NOT forced onto catalogue rows, and most of these have none: "the target no longer
    /// exists" is a precondition on an edit, not a statement about a file. Where a refusal DOES encode the same
    /// constraint a row states, the entry records the cross-reference in its doc-comment rather than restating
    /// the predicate — the row keeps the predicate, and the two say the same thing from opposite ends: one
    /// refuses a state being authored, the other reports one that arrived.
    /// </para>
    /// <para>
    /// Each entry is built from the code MEMBER the refusal site uses, so the identity is declared once. A family
    /// governed nowhere is exactly the defect this catalogue was created after finding: ten codes were already
    /// shipping with no entry behind any of them.
    /// </para>
    /// <para>
    /// The templates are the sentences the sites already produce, with interpolated values as declared slots.
    /// Not one word of user-facing text changed in giving these codes; the pinned language tests are the proof.
    /// </para>
    /// </summary>
    internal static partial class ProblemCatalogEntries
    {
        /// <summary>
        /// The element the command targets is not in the project any more.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditTargetMissing =>
            new ProblemCatalogEntry(
                EditRefusalCodes.TargetMissing,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("noun", ProblemArgumentType.AuthoredName)]),
                "{noun} findes ikke længere.")
            {
                Diagnostic = "The element the command targets is not in the project any more.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The target exists but is not the kind of element this command edits.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditTargetWrongKind =>
            new ProblemCatalogEntry(
                EditRefusalCodes.TargetWrongKind,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("noun", ProblemArgumentType.AuthoredName)]),
                "Målet er ikke {noun}.")
            {
                Diagnostic = "The target exists but is not the kind of element this command edits.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The target lies at or inside a locked function block.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditTargetLocked =>
            new ProblemCatalogEntry(
                EditRefusalCodes.TargetLocked,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Funktionsblokken er låst og kan ikke redigeres.")
            {
                Diagnostic = "The target lies at or inside a locked function block.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// There is no open project to edit.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditNoProjectOpen =>
            new ProblemCatalogEntry(
                EditRefusalCodes.NoProjectOpen,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Der er ikke åbnet et projekt.")
            {
                Diagnostic = "There is no open project to edit.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The edit was prepared against an older version than the one it is being applied to.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditStaleBaseVersion =>
            new ProblemCatalogEntry(
                EditRefusalCodes.StaleBaseVersion,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Projektet er ændret, siden denne redigering blev forberedt.")
            {
                Diagnostic = "The edit was prepared against an older version than the one it is being applied to.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A link needs a source that produces and a target that consumes.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditLinkDirection =>
            new ProblemCatalogEntry(
                EditRefusalCodes.LinkDirection,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "De to klemmer kan ikke linkes i den retning.")
            {
                Diagnostic = "A link needs a source that produces and a target that consumes.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// One end of the scene row is not in the project any more.
        /// <para>
        /// CROSS-REFERENCE: the catalogue row <c>scene-bijection</c> states the same constraint about a
        /// file that already carries the state. This refuses one being AUTHORED; that reports one that
        /// arrived. The predicate is stated once, on the row.
        /// </para>
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditSceneEndpointMissing =>
            new ProblemCatalogEntry(
                EditRefusalCodes.SceneEndpointMissing,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Et endepunkt i scenariet findes ikke længere.")
            {
                Diagnostic = "One end of the scene row is not in the project any more.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The scene container is pinned to one member value kind.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditSceneMemberKind =>
            new ProblemCatalogEntry(
                EditRefusalCodes.SceneMemberKind,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("pinned", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("produced", ProblemArgumentType.SchemaName),
                ]),
                "Denne scenarie-beholder rummer {pinned}-medlemmer; en {produced}-værdi kan ikke tilknyttes her.")
            {
                Diagnostic = "The scene container is pinned to one member value kind.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The named section is not one of the function block variable containers.
        /// <para>
        /// CROSS-REFERENCE: the catalogue row <c>fb-pin-container</c> states the same constraint about a
        /// file that already carries the state. This refuses one being AUTHORED; that reports one that
        /// arrived. The predicate is stated once, on the row.
        /// </para>
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditSectionNotVariables =>
            new ProblemCatalogEntry(
                EditRefusalCodes.SectionNotVariables,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("section", ProblemArgumentType.SchemaName)]),
                "<{section}> er ikke en variabelsektion i en funktionsblok.")
            {
                Diagnostic = "The named section is not one of the function block variable containers.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The section cannot hold an enumerated variable.
        /// <para>
        /// CROSS-REFERENCE: the catalogue row <c>fb-pin-container</c> states the same constraint about a
        /// file that already carries the state. This refuses one being AUTHORED; that reports one that
        /// arrived. The predicate is stated once, on the row.
        /// </para>
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditSectionRejectsEnum =>
            new ProblemCatalogEntry(
                EditRefusalCodes.SectionRejectsEnum,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("section", ProblemArgumentType.SchemaName)]),
                "<{section}> kan ikke rumme en enumerator-variabel.")
            {
                Diagnostic = "The section cannot hold an enumerated variable.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The variable was not added; the edit produced no new element.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditVariableNotAdded =>
            new ProblemCatalogEntry(
                EditRefusalCodes.VariableNotAdded,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Variablen blev ikke tilføjet.")
            {
                Diagnostic = "The variable was not added; the edit produced no new element.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The project has no enumerator type of that name.
        /// <para>
        /// CROSS-REFERENCE: the catalogue row <c>enum-typedef</c> states the same constraint about a
        /// file that already carries the state. This refuses one being AUTHORED; that reports one that
        /// arrived. The predicate is stated once, on the row.
        /// </para>
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditEnumTypeMissing =>
            new ProblemCatalogEntry(
                EditRefusalCodes.EnumTypeMissing,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName)]),
                "Projektet har ingen enumeratortype ved navn {name}.")
            {
                Diagnostic = "The project has no enumerator type of that name.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The enumerator type is built in and cannot be edited.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditEnumTypeReadOnly =>
            new ProblemCatalogEntry(
                EditRefusalCodes.EnumTypeReadOnly,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName)]),
                "Enumeratortypen {name} er en indbygget [read only]-type og kan ikke redigeres.")
            {
                Diagnostic = "The enumerator type is built in and cannot be edited.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The enumerator type is still referenced by resources.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditEnumTypeInUse =>
            new ProblemCatalogEntry(
                EditRefusalCodes.EnumTypeInUse,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("users", ProblemArgumentType.Integer),
                ]),
                "Enumeratortypen {name} bruges stadig af {users} ressource(r) og kan ikke slettes.")
            {
                Diagnostic = "The enumerator type is still referenced by resources.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The enumerator type has no value at that position.
        /// <para>
        /// CROSS-REFERENCE: the catalogue row <c>enum-inivalue</c> states the same constraint about a
        /// file that already carries the state. This refuses one being AUTHORED; that reports one that
        /// arrived. The predicate is stated once, on the row.
        /// </para>
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditEnumValueMissing =>
            new ProblemCatalogEntry(
                EditRefusalCodes.EnumValueMissing,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("index", ProblemArgumentType.Integer),
                ]),
                "Enumeratortypen {name} har ingen værdi nummer {index}.")
            {
                Diagnostic = "The enumerator type has no value at that position.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The terminal is not in the project any more.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditTerminalMissing =>
            new ProblemCatalogEntry(
                EditRefusalCodes.TerminalMissing,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Klemmen findes ikke længere.")
            {
                Diagnostic = "The terminal is not in the project any more.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The terminal address is outside the legal module range.
        /// <para>
        /// CROSS-REFERENCE: the catalogue row <c>dataline-address-range</c> states the same constraint about a
        /// file that already carries the state. This refuses one being AUTHORED; that reports one that
        /// arrived. The predicate is stated once, on the row.
        /// </para>
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditTerminalAddressRange =>
            new ProblemCatalogEntry(
                EditRefusalCodes.TerminalAddressRange,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Klemmenummeret ligger uden for datalinjens område.")
            {
                Diagnostic = "The terminal address is outside the legal module range.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A submitted field points at an element that no longer exists.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditFieldTargetMissing =>
            new ProblemCatalogEntry(
                EditRefusalCodes.FieldTargetMissing,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Et af felterne peger på et element, der ikke findes længere.")
            {
                Diagnostic = "A submitted field points at an element that no longer exists.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A submitted field points at an element outside the product subtree.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditFieldOutsideProduct =>
            new ProblemCatalogEntry(
                EditRefusalCodes.FieldOutsideProduct,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Et af felterne peger på et element uden for produktet.")
            {
                Diagnostic = "A submitted field points at an element outside the product subtree.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The re-composed dialog descriptor offers no such field.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditFieldNotOffered =>
            new ProblemCatalogEntry(
                EditRefusalCodes.FieldNotOffered,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("field", ProblemArgumentType.SchemaName)]),
                "Produktets dialog har ikke feltet {field}.")
            {
                Diagnostic = "The re-composed dialog descriptor offers no such field.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The field is read-only in the re-composed descriptor.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditFieldReadOnly =>
            new ProblemCatalogEntry(
                EditRefusalCodes.FieldReadOnly,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("field", ProblemArgumentType.AuthoredName)]),
                "Feltet {field} kan ikke redigeres.")
            {
                Diagnostic = "The field is read-only in the re-composed descriptor.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The submitted value does not satisfy the field value rule.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditFieldValueRule =>
            new ProblemCatalogEntry(
                EditRefusalCodes.FieldValueRule,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("field", ProblemArgumentType.AuthoredName)]),
                "Feltet {field} har en ugyldig værdi.")
            {
                Diagnostic = "The submitted value does not satisfy the field value rule.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The submitted telephone number is not one the modem can dial.
        /// <para>
        /// WHY IT IS NOT <c>edit.field-value-rule</c>: that entry's template is <i>"Feltet {field} har en ugyldig
        /// værdi."</i> and its only slot is <c>{field}</c>. The commit site does not show that sentence — it shows
        /// the phone rule's own specific guidance — so raising the generic code there anchored a sentence to an
        /// entry that did not govern it, and left the generic template unrendered and unmatched. A dedicated code
        /// with a <c>{value}</c> slot is what closes that, rather than relocating it.
        /// </para>
        /// <para>
        /// CROSS-REFERENCE: <c>addr-modem-phonenumber-malformed</c> states the same constraint about a file that
        /// already carries the number. This refuses one being AUTHORED; that reports one that arrived. Both read
        /// ONE predicate — <c>DialogValueRule.PhoneNumber</c> — so the two sentences are deliberately identical.
        /// </para>
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditFieldPhonenumberMalformed =>
            new ProblemCatalogEntry(
                EditRefusalCodes.FieldPhonenumberMalformed,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue)]),
                "Telefonnummeret '{value}' skal være på 3-20 tegn uden mellemrum og begynde med en landekode, "
                + "f.eks. +45.")
            {
                Diagnostic = "The submitted phonenumber is not 3-20 non-whitespace characters beginning with "
                    + "+<digit>.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The submitted value is outside the bounds the catalog element declares.
        /// <para>
        /// CROSS-REFERENCE: the catalogue row <c>dev-setting-default</c> states the same constraint about a
        /// file that already carries the state. This refuses one being AUTHORED; that reports one that
        /// arrived. The predicate is stated once, on the row.
        /// </para>
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditFieldOutOfRange =>
            new ProblemCatalogEntry(
                EditRefusalCodes.FieldOutOfRange,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("field", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("minimum", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("maximum", ProblemArgumentType.Integer),
                ]),
                EditRefusalProblems.FieldOutOfRangeRefusal)
            {
                Diagnostic = "The submitted value is outside the two bounds the catalog element declares.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The submitted value is below the only bound the field declares.
        /// <para>
        /// D05's second shape. It exists so the row can declare exactly the slot it can bind: a field with a
        /// minimum and no maximum has no number for a <c>{maximum}</c> slot, and a template carrying one would
        /// either render a placeholder or force the site to write its own sentence.
        /// </para>
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditFieldBelowMinimum =>
            new ProblemCatalogEntry(
                EditRefusalCodes.FieldBelowMinimum,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("field", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("minimum", ProblemArgumentType.Integer),
                ]),
                EditRefusalProblems.FieldBelowMinimumRefusal)
            {
                Diagnostic = "The submitted value is below the only bound the catalog element declares.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The submitted value is above the only bound the field declares. D05's third shape; the mirror of
        /// <see cref="EditFieldBelowMinimum"/>.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditFieldAboveMaximum =>
            new ProblemCatalogEntry(
                EditRefusalCodes.FieldAboveMaximum,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("field", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("maximum", ProblemArgumentType.Integer),
                ]),
                EditRefusalProblems.FieldAboveMaximumRefusal)
            {
                Diagnostic = "The submitted value is above the only bound the catalog element declares.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The criterion is not a state of the switch enumerator type.
        /// <para>
        /// CROSS-REFERENCE: the catalogue row <c>logic-case-duplicate-value</c> states the same constraint about a
        /// file that already carries the state. This refuses one being AUTHORED; that reports one that
        /// arrived. The predicate is stated once, on the row.
        /// </para>
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditCaseBranchInvalid =>
            new ProblemCatalogEntry(
                EditRefusalCodes.CaseBranchInvalid,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Ikke en gyldig case-forgrening på en kommandogruppe.")
            {
                Diagnostic = "The criterion is not a state of the switch enumerator type.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The target row is not a logging row.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditNotALogRow =>
            new ProblemCatalogEntry(
                EditRefusalCodes.NotALogRow,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Ikke en Logning-række.")
            {
                Diagnostic = "The target row is not a logging row.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The target is not a command group.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditNotACommandGroup =>
            new ProblemCatalogEntry(
                EditRefusalCodes.NotACommandGroup,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Målet er ikke en kommandogruppe.")
            {
                Diagnostic = "The target is not a command group.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The move is outside the modeled placement rules.
        /// <para>
        /// CROSS-REFERENCE: the catalogue row <c>containment</c> states the same constraint about a
        /// file that already carries the state. This refuses one being AUTHORED; that reports one that
        /// arrived. The predicate is stated once, on the row.
        /// </para>
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditMoveNotAllowed =>
            new ProblemCatalogEntry(
                EditRefusalCodes.MoveNotAllowed,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Den flytning er ikke tilladt.")
            {
                Diagnostic = "The move is outside the modeled placement rules.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The container cannot hold a node of that kind.
        /// <para>
        /// CROSS-REFERENCE: the catalogue row <c>containment</c> states the same constraint about a
        /// file that already carries the state. This refuses one being AUTHORED; that reports one that
        /// arrived. The predicate is stated once, on the row.
        /// </para>
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditContainerRejectsNode =>
            new ProblemCatalogEntry(
                EditRefusalCodes.ContainerRejectsNode,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Den beholder kan ikke rumme denne node.")
            {
                Diagnostic = "The container cannot hold a node of that kind.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// RETIRED (D5). One code for three refusals, declaring the fixed sentence
        /// <i>"Dette element kan ikke slettes."</i> — which no user ever read: both raise sites forwarded the
        /// engine's own reason instead, and the site register recorded that forwarding as deliberate because the
        /// sentence names WHICH rule refused and the shell cannot know that. So the entry published a sentence the
        /// product never shows, which is a catalogue entry that has stopped being the truth about its own row. It
        /// SPLIT into <c>edit.deletion-refused-catalog-pin</c>, <c>edit.deletion-refused-locked-block</c> and
        /// <c>edit.deletion-refused-structural</c>, each declaring the sentence it actually renders.
        /// <para>
        /// It stays here, and is never re-pointed at a successor, for the reason <c>dataline-address</c> states.
        /// </para>
        /// PREDICATE: none. Nothing implements a retired code.
        /// </summary>
        private static ProblemCatalogEntry EditDeletionRefused =>
            new ProblemCatalogEntry(
                new ProblemCode("edit.deletion-refused"),
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "",
                ProblemCodeStatus.Retired)
            {
                Diagnostic = "Split into edit.deletion-refused-catalog-pin, edit.deletion-refused-locked-block and "
                    + "edit.deletion-refused-structural; this id is reserved and never re-used.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A product's catalog-declared pin cannot be deleted on its own: the catalog type declares it, so the
        /// product owns it and removing the product is the way to remove the pin.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan. The rule itself is
        /// <c>ProjectEditor.ClassifyDeletionRefusal</c>'s, which is also what the menu gate and the preview read,
        /// so one owner decides deletability on every surface.
        /// ARGUMENTS: the pin's authored name — the one thing the reader needs and the shell cannot supply.
        /// </summary>
        private static ProblemCatalogEntry EditDeletionRefusedCatalogPin =>
            new ProblemCatalogEntry(
                EditRefusalCodes.DeletionRefusedCatalogPin,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("pin", ProblemArgumentType.AuthoredName)]),
                "Klemmen '{pin}' er katalogdefineret på sit produkt og kan ikke slettes alene — "
                + "slet produktet for at fjerne den.")
            {
                Diagnostic = "A product's catalog-declared pin cannot be deleted on its own.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A node inside a LOCKED function block cannot be deleted: the library owns that subtree until the block
        /// is unlocked, which is the remedy the sentence names.
        /// PREDICATE: none — raised by the command that refuses. See <c>edit.deletion-refused-catalog-pin</c> for
        /// where the rule lives.
        /// </summary>
        private static ProblemCatalogEntry EditDeletionRefusedLockedBlock =>
            new ProblemCatalogEntry(
                EditRefusalCodes.DeletionRefusedLockedBlock,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Denne node er inde i en låst funktionsblok og kan ikke slettes — lås blokken op først.")
            {
                Diagnostic = "A node inside a locked function block cannot be deleted.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A node that is project STRUCTURE rather than content — a section, an event container, an enum
        /// definitions holder — has nothing for the installer to remove.
        /// PREDICATE: none — raised by the command that refuses, when the delete classifier says the node is not
        /// deletable and neither ownership rule is the reason.
        /// THE SENTENCE IS THE GUI'S OLD ONE, not the retired entry's: "Dette element kan ikke slettes." said
        /// nothing about why, and the sentence a user actually met on this path already said it.
        /// </summary>
        private static ProblemCatalogEntry EditDeletionRefusedStructural =>
            new ProblemCatalogEntry(
                EditRefusalCodes.DeletionRefusedStructural,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Denne node er en del af projektets struktur og kan ikke slettes.")
            {
                Diagnostic = "The node is project structure rather than content, so there is nothing to delete.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A field that must carry a value was submitted blank.
        /// PREDICATE: none as a project row — it is raised by the dialog or command that refuses. The blank
        /// DECISION is <c>RequiredFieldConstraint</c>'s, whose policy states whether whitespace counts.
        /// </summary>
        private static ProblemCatalogEntry EditValueRequired =>
            new ProblemCatalogEntry(
                EditRefusalCodes.ValueRequired,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Feltet skal udfyldes.")
            {
                Diagnostic = "A field that must carry a value was submitted blank.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The case criterion is not a state of the switch's enumerator type.
        /// PREDICATE: none — it is raised where the command cannot be minted, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditCaseValueNotAState =>
            new ProblemCatalogEntry(
                EditRefusalCodes.CaseValueNotAState,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("value", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("type", ProblemArgumentType.AuthoredName),
                ]),
                "Værdien '{value}' er ikke en tilstand i enumeratortypen '{type}'.")
            {
                Diagnostic = "The case criterion is not a state of the switch's enumerator type.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The catalog carries no library function block with that master type, so no insert command exists to
        /// judge (US-018).
        /// PREDICATE: none — it is raised where the command cannot be minted, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditLibraryBlockMissing =>
            new ProblemCatalogEntry(
                EditRefusalCodes.LibraryBlockMissing,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("masterType", ProblemArgumentType.AttributeValue)]),
                "Ingen biblioteks-funktionsblok med master type '{masterType}'.")
            {
                Diagnostic = "No library function block in the catalog declares that master type.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The catalog carries no product with that identifier — or carries it more than once and the display name
        /// did not decide, which the resolver refuses rather than guessing (D22).
        /// PREDICATE: none — it is raised where the command cannot be minted, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditCatalogProductMissing =>
            new ProblemCatalogEntry(
                EditRefusalCodes.CatalogProductMissing,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                    [new ProblemArgumentSlot("identifier", ProblemArgumentType.AttributeValue)]),
                "Intet katalogprodukt med identifikator '{identifier}'.")
            {
                Diagnostic = "The catalog carries no product with that identifier, or carries it more than once "
                    + "and the display name did not decide which.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The project may hold at most one modem and already holds one (US-013).
        /// <para>
        /// CROSS-REFERENCE: the catalogue row <c>capacity-modem-multiple</c> states the same constraint about a
        /// file that already carries two modems. This refuses the second one being AUTHORED; that reports one that
        /// arrived. The predicate is stated once, on the row.
        /// </para>
        /// PREDICATE: none — it is raised by the pre-check that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditModemLimit =>
            new ProblemCatalogEntry(
                EditRefusalCodes.ModemLimit,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Et projekt må højst indeholde ét modem. Fjern det eksisterende modem, før du tilføjer et nyt.")
            {
                Diagnostic = "The project already contains a modem and may hold at most one.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A guard inside execution refused after the verdict allowed; the deep guard carries its own sentence.
        /// PREDICATE: none — it is raised by the command that refuses, never detected by a scan.
        /// </summary>
        private static ProblemCatalogEntry EditDeepGuard =>
            new ProblemCatalogEntry(
                EditRefusalCodes.DeepGuard,
                ProblemCatalogSection.OperationOutcomes,
                null,
                CatalogDisposition.Refusal,
                RuleKind.EditPrecondition,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Redigeringen kunne ikke gennemføres.")
            {
                Diagnostic = "A guard inside execution refused after the verdict allowed; the deep guard carries its own sentence.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>Every edit-family declaration, in code order.</summary>
        private static ProblemCatalogEntry[] EditRefusals =>
        [
            EditTargetMissing,
            EditTargetWrongKind,
            EditTargetLocked,
            EditNoProjectOpen,
            EditStaleBaseVersion,
            EditLinkDirection,
            EditSceneEndpointMissing,
            EditSceneMemberKind,
            EditSectionNotVariables,
            EditSectionRejectsEnum,
            EditVariableNotAdded,
            EditEnumTypeMissing,
            EditEnumTypeReadOnly,
            EditEnumTypeInUse,
            EditEnumValueMissing,
            EditTerminalMissing,
            EditTerminalAddressRange,
            EditFieldTargetMissing,
            EditFieldOutsideProduct,
            EditFieldNotOffered,
            EditFieldReadOnly,
            EditFieldValueRule,
            EditFieldPhonenumberMalformed,
            EditFieldOutOfRange,
            EditFieldBelowMinimum,
            EditFieldAboveMaximum,
            EditCaseBranchInvalid,
            EditNotALogRow,
            EditNotACommandGroup,
            EditMoveNotAllowed,
            EditContainerRejectsNode,
            EditDeletionRefused,
            EditDeletionRefusedCatalogPin,
            EditDeletionRefusedLockedBlock,
            EditDeletionRefusedStructural,
            EditValueRequired,
            EditCaseValueNotAState,
            EditLibraryBlockMissing,
            EditCatalogProductMissing,
            EditModemLimit,
            EditDeepGuard,
        ];
    }
}
