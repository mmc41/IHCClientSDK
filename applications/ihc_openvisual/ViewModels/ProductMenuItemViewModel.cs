using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// One node of the catalog product-insertion menu (US-010): either a category (a <see cref="Children"/>-bearing
/// submenu built from a product's <c>CategoryPath</c>) or a leaf product (carrying <see cref="ProductIdentifier"/>
/// and a <see cref="Command"/> that inserts it under the selected locality). Avalonia-free so the catalog→menu
/// projection is unit-testable.
/// </summary>
public sealed class ProductMenuItemViewModel
{
    /// <summary>A category submenu node.</summary>
    public ProductMenuItemViewModel(string header) => Header = header;

    /// <summary>A leaf product node that inserts <paramref name="productIdentifier"/> when invoked.</summary>
    public ProductMenuItemViewModel(string header, string productIdentifier, ICommand command)
    {
        Header = header;
        ProductIdentifier = productIdentifier;
        Command = command;
    }

    public string Header { get; }

    /// <summary>The catalog product this leaf inserts, or null for a category node.</summary>
    public string? ProductIdentifier { get; }

    /// <summary>The insert command for a leaf, or null for a category node.</summary>
    public ICommand? Command { get; }

    /// <summary>The stable id this item publishes to UI Automation. A leaf is identified by its catalog product
    /// identifier — the one part of a generated menu item that is neither localized chrome nor user data — and a
    /// category node by its category name, which comes from the catalog's <c>CategoryPath</c> for the same reason.
    /// Without this the whole product/function-block/program-command forest is addressable only by display text.</summary>
    public string AutomationId => ProductIdentifier ?? Header;

    /// <summary>Whether this is a product leaf (has a product identifier) rather than a category node. Intentional
    /// test-only seam (D02): the menu templates route on ItemsSource/Command; this predicate is read only by the
    /// catalog-menu tests, kept as a readable classifier.</summary>
    public bool IsLeaf => ProductIdentifier is not null;

    public ObservableCollection<ProductMenuItemViewModel> Children { get; } = new();
}
