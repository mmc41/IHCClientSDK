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
    public sealed record CatalogItem(string Identifier, string DisplayName, string? CategoryPath);
}
