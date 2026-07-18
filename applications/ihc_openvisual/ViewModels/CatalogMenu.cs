using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Products;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// Projects the flat catalog product / function-block lists into their nested insertion menus. A component's library
/// location is a <c>\</c>-separated <c>CategoryPath</c> whose segments carry a numeric sort prefix — <c>NN#</c> for
/// products (<c>Datalinie produkter\01#Input\01#LK FUGA</c>) and <c>NN.</c> for function-block folders
/// (<c>01. Lysstyring\1.1 Generelt</c>). This groups the components into a tree of
/// <see cref="ProductMenuItemViewModel"/>: folder nodes ordered by their numeric prefix, product leaves ordered by
/// name and wired to an insert command. Avalonia-free and unit-tested.
/// </summary>
public static class CatalogMenu
{
    /// <summary>The vendor top category for wired (data-line) products — shown as "Wired products" (E3 scope).</summary>
    public const string WiredProductsCategory = "Datalinie produkter";

    /// <summary>Builds the category subtree for the wired (data-line) products (US-010).</summary>
    public static IReadOnlyList<ProductMenuItemViewModel> BuildWiredProducts(
        IEnumerable<ProductDefinition> products, Func<ProductDefinition, ICommand> leafCommand) =>
        Build(products, WiredProductsCategory, leafCommand);

    /// <summary>
    /// Builds the menu subtree for the products whose <c>CategoryPath</c> begins with <paramref name="topCategory"/>,
    /// dropping that top segment (its label is the hosting menu item).
    /// </summary>
    public static IReadOnlyList<ProductMenuItemViewModel> Build(
        IEnumerable<ProductDefinition> products, string topCategory, Func<ProductDefinition, ICommand> leafCommand)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(leafCommand);
        return BuildForest(
            products.Where(p => Segments(p.CategoryPath).FirstOrDefault() == topCategory),
            p => Segments(p.CategoryPath).Skip(1).ToArray(),   // drop the top category itself
            p => p.DisplayName, leafCommand, p => p.ProductIdentifier,
            // Product-catalog subcategories render in English (A-29/R-1); the FB library folders stay verbatim.
            raw => TranslateSubcategory(Strip(raw)));
    }

    // The product-catalog STRUCTURAL subcategories the vendor left Danish, mapped to English (R-1). Family/brand names
    // (LK FUGA, Vinduer, IR fjernbetjeninger…) are vendor data and stay as-is, like the FB library categories.
    private static readonly Dictionary<string, string> SubcategoryEnglish = new(StringComparer.Ordinal)
    {
        ["Generelle"] = "General",
        ["Indgang"] = "Input",
        ["Udgang"] = "Output",
    };

    private static string TranslateSubcategory(string label) =>
        SubcategoryEnglish.TryGetValue(label, out string? english) ? english : label;

    /// <summary>Builds a flat, name-ordered list of product leaves (no category nesting) — e.g. the Special products
    /// modem list (US-013).</summary>
    public static IReadOnlyList<ProductMenuItemViewModel> BuildLeaves(
        IEnumerable<ProductDefinition> products, Func<ProductDefinition, ICommand> leafCommand)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(leafCommand);
        return products
            .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(p => new ProductMenuItemViewModel(p.DisplayName, p.ProductIdentifier, leafCommand(p)))
            .ToList();
    }

    /// <summary>Builds the full library-folder tree for the catalog function blocks (US-018), keyed by
    /// <see cref="FunctionBlockDefinition.MasterType"/>.</summary>
    public static IReadOnlyList<ProductMenuItemViewModel> BuildFunctionBlocks(
        IEnumerable<FunctionBlockDefinition> blocks, Func<FunctionBlockDefinition, ICommand> leafCommand)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(leafCommand);
        return BuildForest(
            blocks,
            b => Segments(b.CategoryPath),   // the whole path is the folder tree
            b => b.DisplayName, leafCommand, b => b.MasterType,
            Strip);   // FB library category names stay Danish verbatim (US-018)
    }

    private static string[] Segments(string? categoryPath) =>
        (categoryPath ?? string.Empty).Split('\\', StringSplitOptions.RemoveEmptyEntries);

    private static IReadOnlyList<ProductMenuItemViewModel> BuildForest<T>(
        IEnumerable<T> items, Func<T, string[]> segments, Func<T, string> displayName,
        Func<T, ICommand> leafCommand, Func<T, string> key, Func<string, string> folderLabel)
    {
        var root = new Node(string.Empty);
        foreach (T item in items)
        {
            Node node = root;
            foreach (string segment in segments(item))
                node = node.Child(segment);
            node.Leaves.Add(item!);
        }
        return root.ToMenu(displayName, leafCommand, key, folderLabel);
    }

    // A mutable folder node keyed by the raw path segment (prefix kept for ordering; stripped for display).
    private sealed class Node
    {
        private readonly Dictionary<string, Node> _children = new(StringComparer.Ordinal);

        public Node(string rawSegment) => RawSegment = rawSegment;

        public string RawSegment { get; }
        public List<Node> Ordered { get; } = new();
        public List<object> Leaves { get; } = new();

        public Node Child(string rawSegment)
        {
            if (!_children.TryGetValue(rawSegment, out Node? child))
            {
                child = new Node(rawSegment);
                _children[rawSegment] = child;
                Ordered.Add(child);
            }
            return child;
        }

        public IReadOnlyList<ProductMenuItemViewModel> ToMenu<T>(
            Func<T, string> displayName, Func<T, ICommand> leafCommand, Func<T, string> key,
            Func<string, string> folderLabel)
        {
            var items = new List<ProductMenuItemViewModel>();
            foreach (Node child in Ordered.OrderBy(c => SortKey(c.RawSegment), StringComparer.Ordinal))
            {
                var folder = new ProductMenuItemViewModel(folderLabel(child.RawSegment));
                foreach (ProductMenuItemViewModel sub in child.ToMenu(displayName, leafCommand, key, folderLabel))
                    folder.Children.Add(sub);
                items.Add(folder);
            }
            foreach (T leaf in Leaves.Cast<T>().OrderBy(displayName, StringComparer.OrdinalIgnoreCase))
                items.Add(new ProductMenuItemViewModel(displayName(leaf), key(leaf), leafCommand(leaf)));
            return items;
        }
    }

    // Zero-pads a leading numeric prefix so ordinal sort orders "01#/02#/10#" and "01./02." correctly.
    private static string SortKey(string segment)
    {
        int i = 0;
        while (i < segment.Length && char.IsDigit(segment[i]))
            i++;
        return i > 0 ? segment[..i].PadLeft(6, '0') + segment[i..] : segment;
    }

    // "01#Input" → "Input"; "01. Lysstyring" and "1.1 Generelt" are kept verbatim (their number is part of the
    // vendor folder name). A segment without a "NN#" prefix is shown as-is.
    private static string Strip(string segment)
    {
        int hash = segment.IndexOf('#');
        return hash > 0 && int.TryParse(segment.AsSpan(0, hash), out _) ? segment[(hash + 1)..] : segment;
    }
}
