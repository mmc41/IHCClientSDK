using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Ihc.Vis;

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
    /// <summary>
    /// Builds the menu subtree for the products whose <c>CategoryPath</c> begins with <paramref name="topCategory"/>,
    /// dropping that top segment (its label is the hosting menu item).
    /// </summary>
    public static IReadOnlyList<ProductMenuItemViewModel> Build(
        IEnumerable<CatalogItem> products, string topCategory, Func<CatalogItem, ICommand> leafCommand)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(leafCommand);
        return BuildSubtree(products, topCategory, leafCommand);
    }

    /// <summary>
    /// Builds the FULL product insertion menu (US-010, H2/D08): the top-level categories are DERIVED from the
    /// catalog products' own <c>CategoryPath</c> — never a hardcoded set — so a product whose top category is
    /// unknown or empty (an imported <c>.def</c> has none) stays reachable, under an "Importeret/Ukategoriseret"
    /// bucket. The vendor top categories keep their declared menu order (<see cref="TopCategories"/>) and show their
    /// own names; any other named category appears by its own (stripped) name; the empty bucket comes last.
    /// Taxonomy is catalog data; the order is app presentation (D08).
    /// </summary>
    public static IReadOnlyList<ProductMenuItemViewModel> BuildProductForest(
        IEnumerable<CatalogItem> products, Func<CatalogItem, ICommand> leafCommand)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(leafCommand);
        var list = products as IReadOnlyCollection<CatalogItem> ?? products.ToList();
        var present = list.Select(p => Segments(p.CategoryPath).FirstOrDefault()).Distinct().ToList();
        var forest = new List<ProductMenuItemViewModel>();
        foreach (string? top in OrderTopCategories(present))
        {
            var folder = new ProductMenuItemViewModel(TopCategoryLabel(top));
            foreach (ProductMenuItemViewModel child in BuildSubtree(list, top, leafCommand))
                folder.Children.Add(child);
            forest.Add(folder);
        }
        return forest;
    }

    // The subtree under one top category (null = the empty/imported top): the products whose CategoryPath begins
    // with it, nested by their remaining segments, that top segment dropped (it labels the hosting menu item).
    private static IReadOnlyList<ProductMenuItemViewModel> BuildSubtree(
        IEnumerable<CatalogItem> products, string? topCategory, Func<CatalogItem, ICommand> leafCommand) =>
        BuildForest(
            products.Where(p => Segments(p.CategoryPath).FirstOrDefault() == topCategory),
            p => Segments(p.CategoryPath).Skip(1).ToArray(),   // drop the top category itself
            p => p.DisplayName, leafCommand, p => p.Identifier,
            // The catalog's own category names are already the UI language, so a folder shows its name verbatim.
            raw => Strip(raw),
            // Leaves order by the catalog's own name, which carries the same NN# prefix the folders order by.
            p => p.OrderName);

    /// <summary>The label of the bucket that holds imported / empty-category products (H2/D08), so an imported
    /// <c>.def</c> with no <c>CategoryPath</c> stays reachable in the insert menu.</summary>
    public const string ImportedCategoryLabel = "Importeret/Ukategoriseret";

    /// <summary>The vendor top-level product category names, spelled as the catalog itself spells them. They are
    /// <i>catalog data</i>, not app wording — <see cref="BuildProductForest"/> matches products on them and shows
    /// them verbatim — so they are declared here for every caller that needs to address one category, rather than
    /// re-typed at each site where the exact spelling would be an unchecked guess.</summary>
    public const string WiredProductsCategory = "Datalinie produkter";
    public const string WirelessProductsCategory = "LK IHC Wireless produkter";
    public const string BusProductsCategory = "Bus Produkter";
    public const string SpecialProductsCategory = "Specielle produkter";

    /// <summary>The vendor top-level product categories in MENU ORDER (D08: the taxonomy is catalog data, the order
    /// is app presentation). The order is the vendor's own Indsæt ▸ Produkter order — Bus, Datalinie, Wireless,
    /// Specielle — measured 2026-08-09 (alignment F-12a, armed bar dump). Their names are already in the UI
    /// language, so they are displayed verbatim — this list carries only the order. A product whose top category is
    /// none of these — an imported <c>.def</c> with an empty <c>CategoryPath</c> — falls into
    /// <see cref="ImportedCategoryLabel"/>, appended last.</summary>
    public static readonly IReadOnlyList<string> TopCategories =
    [
        BusProductsCategory,
        WiredProductsCategory,
        WirelessProductsCategory,
        SpecialProductsCategory,
    ];

    // The present top categories in menu order: the known vendor categories first (their declared order), then any
    // other named category (ordinal), then the empty/imported bucket (null) last.
    private static IEnumerable<string?> OrderTopCategories(IReadOnlyCollection<string?> present)
    {
        foreach (string category in TopCategories)
            if (present.Contains(category))
                yield return category;
        foreach (string? other in present.Where(c => c is not null && !IsKnownTopCategory(c)).OrderBy(c => c, DisplayOrder.Danish))
            yield return other;
        if (present.Contains(null))
            yield return null;
    }

    private static bool IsKnownTopCategory(string category) => TopCategories.Contains(category, StringComparer.Ordinal);

    // A top category's menu label: its own stripped name, or ImportedCategoryLabel for the empty bucket.
    private static string TopCategoryLabel(string? category) =>
        category is null ? ImportedCategoryLabel : Strip(category);

    /// <summary>Builds the full library-folder tree for the catalog function blocks (US-018), keyed by their
    /// <see cref="CatalogItem.Identifier"/> (the function block's <c>master_type</c>).</summary>
    public static IReadOnlyList<ProductMenuItemViewModel> BuildFunctionBlocks(
        IEnumerable<CatalogItem> blocks, Func<CatalogItem, ICommand> leafCommand)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(leafCommand);
        return BuildForest(
            blocks,
            b => Segments(b.CategoryPath),   // the whole path is the folder tree
            b => b.DisplayName, leafCommand, b => b.Identifier,
            Strip,   // FB library category names stay Danish verbatim (US-018)
            b => b.OrderName);
    }

    private static string[] Segments(string? categoryPath) =>
        (categoryPath ?? string.Empty).Split('\\', StringSplitOptions.RemoveEmptyEntries);

    private static IReadOnlyList<ProductMenuItemViewModel> BuildForest<T>(
        IEnumerable<T> items, Func<T, string[]> segments, Func<T, string> displayName,
        Func<T, ICommand> leafCommand, Func<T, string> key, Func<string, string> folderLabel,
        Func<T, string?> orderName)
    {
        var root = new Node(string.Empty);
        foreach (T item in items)
        {
            Node node = root;
            foreach (string segment in segments(item))
                node = node.Child(segment);
            node.Leaves.Add(item!);
        }
        return root.ToMenu(displayName, leafCommand, key, folderLabel, orderName);
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
            Func<string, string> folderLabel, Func<T, string?> orderName)
        {
            // Folders and leaves share ONE ordering sequence, keyed by the catalog's own NN# prefix — the reference
            // application's Datalinie ▸ Input runs 01#LK FUGA, 02#LK OPUS, 03#PIR, 04#…, 05#…, 06#IR
            // fjernbetjeninger, 07#Mini Modul, 08#Ringetryk: subfolders and products interleaved, numbered together
            // (measured 2026-08-11, alignment F-9). Emitting all folders and then all leaves — which is the natural
            // shape and what this did — moves three PIR products four places, and no per-list sort can put them back
            // because the information lives in the numbering the two kinds SHARE.
            //
            // A leaf orders by the catalog's own name, not by its display label: the label has had that prefix
            // stripped. A component declaring no catalog name falls back to its label, which keeps it deterministic.
            // Unnumbered entries sort after numbered ones (letters after digits), which is where the original puts
            // them too.
            var entries = new List<(string Key, Func<ProductMenuItemViewModel> Make)>();
            foreach (Node child in Ordered)
            {
                Node captured = child;
                entries.Add((SortKey(captured.RawSegment), () =>
                {
                    var folder = new ProductMenuItemViewModel(folderLabel(captured.RawSegment));
                    foreach (ProductMenuItemViewModel sub in
                             captured.ToMenu(displayName, leafCommand, key, folderLabel, orderName))
                        folder.Children.Add(sub);
                    return folder;
                }
                ));
            }
            foreach (T leaf in Leaves.Cast<T>())
            {
                T captured = leaf;
                entries.Add((SortKey(orderName(captured) ?? displayName(captured)),
                    () => new ProductMenuItemViewModel(displayName(captured), key(captured), leafCommand(captured))));
            }

            // OrderBy is stable, so entries sharing a key keep the order they were added in — folders before leaves,
            // which is what this method did before and what the catalog's own numbering never actually ties on.
            return [.. entries.OrderBy(e => e.Key, DisplayOrder.Danish).Select(e => e.Make())];
        }
    }

    // Zero-pads a leading numeric prefix so "01#/02#/10#" and "01./02." order by NUMBER rather than by digit
    // sequence ("10" before "2"). Padding, not parsing, because the prefix is only part of the segment: what
    // follows it still decides ties, and it is ordered by DisplayOrder.Danish like everything else on screen.
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
