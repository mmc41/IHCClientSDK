#nullable enable
using Ihc.Vis.Problems;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// The identity of a catalog definition file that will not be read, ready to raise.
    /// <para>
    /// ONE identity for both callers of the shared wrap — the runtime single-file import and the
    /// install-directory scan. Unlike the save guards, where naming one operation would have been wrong at two
    /// of three callers, both of these ARE the same act: taking a definition file into the catalogue. The user
    /// is told the catalogue file could not be read, and which of the two asked changes nothing about that.
    /// </para>
    /// </summary>
    public static class ImportRefusalCodes
    {
        /// <summary>The file cannot be parsed at all — malformed, truncated, or unreadable.</summary>
        public static RefusalIdentity CatalogUnparsable { get; } = new(
            OperationCodes.ImportCatalog, OperationCodes.ImportCatalogLabel,
            new ProblemCode("import-catalog-unparsable"), "Ugyldig katalogfil");
    }
}
