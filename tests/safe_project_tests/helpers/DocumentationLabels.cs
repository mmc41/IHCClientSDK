#nullable enable

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The DOCUMENTATION rows and the exact Danish label each carries — the labels the full report prints verbatim
    /// in its appendix, where the report oracles pin them byte for byte.
    /// </summary>
    /// <remarks>
    /// One table, read by every gate that holds the catalogue to these labels. Written out per fixture it was the
    /// same rows twice, so a row added or a label corrected in one place left the other gate still asserting the
    /// old text — and passing, because a gate that names its own expectations cannot notice the ones it does not
    /// name.
    /// </remarks>
    internal static class DocumentationLabels
    {
        internal static readonly (string Code, string Label)[] Expected =
        [
            ("doc-documentation-tag", "Mangler Id-kode"),
            ("doc-power-group", "Mangler Lysgruppe"),
            ("doc-cabletype", "Mangler Kabeltype"),
            ("doc-cablenumber", "Mangler Kabelnummer"),
            ("doc-position", "Mangler Placering"),
            ("doc-not-linked", "Ikke forbundet"),
            ("doc-cable-colour", "Mangler Ledningsfarve"),
            ("doc-address", "Mangler Adresse"),
        ];
    }
}
