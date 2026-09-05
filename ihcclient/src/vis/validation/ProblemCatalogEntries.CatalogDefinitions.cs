using Ihc.Vis.Problems;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The CATALOG-DEFINITION section: conditions about a <c>.def</c> / <c>.ifb</c> definition file rather than
    /// about a project.
    /// <para>
    /// Ten of these codes were already shipping, emitted by the definition builders and the grammar advisor, with
    /// no catalogue row behind any of them — so "no code exists without an entry" was false before this catalogue
    /// existed. They are governed here rather than excluded: same schema, same uniqueness, arity and status rules.
    /// </para>
    /// <para>
    /// The eleventh, <c>block-identity-missing</c>, is the only one MINTED here rather than adopted: it is the
    /// function-block half of a split, and it exists because one code cannot carry two conditions whose Danish
    /// sentences differ. See its declaration and <see cref="IdentityMissing"/>'s.
    /// </para>
    /// <para>
    /// They are END-USER text, so their labels are Danish. An installer meets them through catalog import when a
    /// definition file will not load, which is a GUI action with a Danish outcome — not the internal tooling the
    /// English convention covers. Each row's original English sentence survives as its diagnostic, so nothing is
    /// lost for whoever is hand-authoring a definition file.
    /// </para>
    /// </summary>
    internal static partial class ProblemCatalogEntries
    {
        /// <summary>
        /// The product declares no product identifier, no display name, or a root tag of no known family.
        /// PREDICATE: implemented today by the product definition builder, over the builder's own accumulated
        /// state at Build time.
        /// SPLIT: this code once served the function-block builder too, for a block missing its master name.
        /// One template cannot be true of both — "Mangler produktidentitet" is false of a block — so that
        /// condition became <see cref="BlockIdentityMissing"/> and this row is now the PRODUCT one only. The id
        /// is not re-pointed: it kept the condition it always described, and the narrower one was minted.
        /// </summary>
        private static ProblemCatalogEntry IdentityMissing =>
            new ProblemCatalogEntry(
                new ProblemCode("identity-missing"),
                ProblemCatalogSection.CatalogDefinitionFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                default,
                "Mangler produktidentitet")
            {
                Diagnostic = "The product needs a product_identifier, a display name and a known family root tag.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The function block declares no master name, so nothing can address it.
        /// PREDICATE: implemented today by the function-block definition builder, over its own accumulated state
        /// at Build time — a block that is not an empty template and whose <c>master_name</c> is blank. The type
        /// and the version are deliberately NOT part of it: many stock blocks carry no version, and a keyless
        /// user block carries no type and is addressed by name alone.
        /// SPLIT: minted from <see cref="IdentityMissing"/>, which described the product condition and was
        /// raised here too for want of a second code. The raiser could not bind that entry's template without
        /// telling a user a block was missing a PRODUCT identity, so it carried English while every sibling
        /// carried Danish; this row is what ends that exception.
        /// </summary>
        private static ProblemCatalogEntry BlockIdentityMissing =>
            new ProblemCatalogEntry(
                new ProblemCode("block-identity-missing"),
                ProblemCatalogSection.CatalogDefinitionFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                default,
                "Mangler blokidentitet")
            {
                Diagnostic =
                    "The block needs a master_name (or AsEmptyTemplate for a Tom blok). "
                    + "master_type/master_version are optional — many stock blocks carry no version, and a "
                    + "keyless user block carries no type (it is then addressable only by name).",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A scenes container was added with no preceding resource for its scene resource to bind to.
        /// PREDICATE: implemented today by the product definition builder.
        /// </summary>
        private static ProblemCatalogEntry ScenesWithoutOutput =>
            new ProblemCatalogEntry(
                new ProblemCode("scenes-without-output"),
                ProblemCatalogSection.CatalogDefinitionFindings,
                ValidationCategory.Scenes,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                default,
                "Scener uden udgang")
            {
                Diagnostic = "AddScenes needs a preceding resource (typically an output) to bind its scene_resource to.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An enumerated resource carries no typedef, so it has no value domain.
        /// PREDICATE: implemented today by the product definition builder, per <c>resource_enum</c> child.
        /// </summary>
        private static ProblemCatalogEntry ResourceEnumUnwired =>
            new ProblemCatalogEntry(
                new ProblemCode("resource-enum-unwired"),
                ProblemCatalogSection.CatalogDefinitionFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("resource_enum", "typedef"),
                FindingShape.OnePerOccurrence,
                default,
                "Enumerator ikke forbundet")
            {
                Diagnostic = "A resource_enum has no typedef wired to an enum_definition.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A program in a function-block definition declares no events, so nothing ever starts it.
        /// PREDICATE: implemented today by the function-block definition builder, per program.
        /// </summary>
        private static ProblemCatalogEntry ProgramEmpty =>
            new ProblemCatalogEntry(
                new ProblemCode("program-empty"),
                ProblemCatalogSection.CatalogDefinitionFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Program uden hændelser")
            {
                Diagnostic = "A program has no events, so it will never run.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Two or more elements of the definition share an id token, so every reference to it is ambiguous.
        /// PREDICATE: implemented today by the grammar advisor, over the whole definition body; the insert
        /// transform re-mints the ids, so the file still loads.
        /// </summary>
        private static ProblemCatalogEntry GrammarDuplicateId =>
            new ProblemCatalogEntry(
                new ProblemCode("grammar-duplicate-id"),
                ProblemCatalogSection.CatalogDefinitionFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Warning,
                RuleKind.SchemaSerializationGuard,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                default,
                "Dobbelt id")
            {
                Diagnostic = "Two or more elements share an id (XML VC: ID); IDREFs to the token become ambiguous.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The body uses an element type the effective grammar does not declare, so the type carries no catalog
        /// defaults. PREDICATE: implemented today by the grammar advisor, per element.
        /// </summary>
        private static ProblemCatalogEntry GrammarUndeclaredType =>
            new ProblemCatalogEntry(
                new ProblemCode("grammar-undeclared-type"),
                ProblemCatalogSection.CatalogDefinitionFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Warning,
                RuleKind.SchemaSerializationGuard,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Ukendt elementtype")
            {
                Diagnostic = "The body uses an element type the effective grammar does not declare "
                    + "(an authentic subset-DTD shape; the written file stays loadable, but the type carries no "
                    + "catalog defaults).",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An IDREF attribute references an id no element in the definition carries.
        /// PREDICATE: implemented today by the grammar advisor, per attribute, reading the schema view so a
        /// registry family is checked even when the grammar omits the declaration.
        /// </summary>
        private static ProblemCatalogEntry GrammarDanglingIdref =>
            new ProblemCatalogEntry(
                new ProblemCode("grammar-dangling-idref"),
                ProblemCatalogSection.CatalogDefinitionFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Warning,
                RuleKind.SchemaSerializationGuard,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Reference uden mål")
            {
                Diagnostic = "An IDREF attribute references an id that is not carried by any element in this definition.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An attribute is not declared by the effective grammar for its element.
        /// PREDICATE: implemented today by the grammar advisor, per attribute.
        /// </summary>
        private static ProblemCatalogEntry GrammarUndeclaredAttribute =>
            new ProblemCatalogEntry(
                new ProblemCode("grammar-undeclared-attribute"),
                ProblemCatalogSection.CatalogDefinitionFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Warning,
                RuleKind.SchemaSerializationGuard,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Ukendt attribut")
            {
                Diagnostic = "The attribute is not declared by the effective grammar for its element.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An enumerated attribute holds a value outside its declared enumeration.
        /// PREDICATE: implemented today by the grammar advisor, per attribute whose declared type is enumerated.
        /// </summary>
        private static ProblemCatalogEntry GrammarEnumValue =>
            new ProblemCatalogEntry(
                new ProblemCode("grammar-enum-value"),
                ProblemCatalogSection.CatalogDefinitionFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Warning,
                RuleKind.SchemaSerializationGuard,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Værdi uden for listen")
            {
                Diagnostic = "The value is outside the attribute's declared enumeration.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A <c>#REQUIRED</c> attribute is missing on an element of the definition.
        /// PREDICATE: implemented today by the grammar advisor, per declared required attribute.
        /// </summary>
        private static ProblemCatalogEntry GrammarMissingRequired =>
            new ProblemCatalogEntry(
                new ProblemCode("grammar-missing-required"),
                ProblemCatalogSection.CatalogDefinitionFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Warning,
                RuleKind.SchemaSerializationGuard,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Manglende påkrævet attribut")
            {
                Diagnostic = "A #REQUIRED attribute is missing on the element.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The definition declares a numeric bound whose text is not a whole number, so the engine reads it as no
        /// bound at all.
        /// <para>
        /// A DEFINITION defect rather than a project one, which is why it sits here: the <c>minimum</c> and
        /// <c>maximum</c> the reader could not parse are the catalog's own, and a project placing the product
        /// inherits them. What made it worth a row is that the two readings had the same shape — the read view
        /// answered <c>null</c> both for "the catalog declares no bound" and for "the catalog declares one I
        /// cannot read", so a limit the catalog STATES disappeared silently on the path that writes a value into
        /// a <c>.vis</c>. The dialog now declines to offer such a field at all; this row is what says why.
        /// </para>
        /// PREDICATE: an element's EFFECTIVE <c>minimum</c> or <c>maximum</c> — its own value when it carries
        /// one, else the grammar's declared default for that attribute — is non-blank and does not parse as an
        /// integer. Effective rather than carried, because that is how <c>ElementView.DeclaredBounds</c> reads
        /// it: a grammar defaulting a bound to something unreadable reaches every element of the tag, so a
        /// carried-only check would leave the dialog refusing a field this row never reported.
        /// SUBJECT: every element of the definition body. EXCLUSION: an absent or blank bound, which declares
        /// nothing.
        /// </summary>
        private static ProblemCatalogEntry CatalogBoundUnreadable =>
            new ProblemCatalogEntry(
                new ProblemCode("catalog-bound-unreadable"),
                ProblemCatalogSection.CatalogDefinitionFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Warning,
                RuleKind.SchemaSerializationGuard,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Grænseværdi kan ikke læses")
            {
                Diagnostic = "A declared minimum or maximum is not a whole number, so the engine reads it as no "
                    + "bound at all; a dialog will not offer the field rather than offer it unbounded.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>Every catalog-definition declaration, in code order.</summary>
        private static ProblemCatalogEntry[] CatalogDefinitionFindings =>
        [
            IdentityMissing,
            BlockIdentityMissing,
            ScenesWithoutOutput,
            ResourceEnumUnwired,
            ProgramEmpty,
            GrammarDuplicateId,
            GrammarUndeclaredType,
            GrammarDanglingIdref,
            GrammarUndeclaredAttribute,
            GrammarEnumValue,
            GrammarMissingRequired,
            CatalogBoundUnreadable,
        ];
    }
}
