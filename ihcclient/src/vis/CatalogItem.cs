#nullable enable
namespace Ihc.Vis
{
    /// <summary>
    /// A slim, display-only projection of a catalog component (product or function block) for the insert menus: the
    /// identifier the authoring gateway inserts by (<c>product_identifier</c> for a product, <c>master_type</c> for a
    /// function block), the display name, and the library <c>CategoryPath</c>. It deliberately carries none of the
    /// full <see cref="Products.ProductDefinition"/> / <see cref="FunctionBlocks.FunctionBlockDefinition"/> authoring
    /// surface (resources, grammar, programs), so menu-building code depends only on what a menu actually needs.
    /// Obtained from <see cref="ProjectAppService.GetProductCatalogItems"/> /
    /// <see cref="ProjectAppService.GetFunctionBlockCatalogItems"/>.
    /// </summary>
    /// <param name="Identifier">The token the authoring gateway inserts by: <c>product_identifier</c> for a
    /// product, <c>master_type</c> for a function block.</param>
    /// <param name="DisplayName">The label shown in the menu, with any catalog sort prefix already stripped.</param>
    /// <param name="CategoryPath">The library category path the component was discovered under.</param>
    /// <param name="OrderName">
    /// The component's catalog <c>name</c> — the ORDERING form of its label, carrying the same <c>NN#</c> sort
    /// prefix the <c>CategoryPath</c> segments use (<c>01#Lampeudtag</c>, <c>05#Diode</c>), which
    /// <c>DisplayName</c> has already had stripped. It is what puts an insert menu's leaves in the catalog's own
    /// order instead of an alphabetical one, and the two genuinely differ: the catalog lists
    /// <i>Lampeudtag, Stikkontakt, Output 1-10V…</i>, grouped by function. Null when the component declares no
    /// name of its own, in which case the display name orders it.
    /// </param>
    public sealed record CatalogItem(string Identifier, string DisplayName, string? CategoryPath,
        string? OrderName = null);
}
