using System;
using System.Collections.Generic;
using System.Linq;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;

namespace Ihc.Tests;

/// <summary>Recursive tree-node lookup shared across the visual suite.</summary>
internal static class TreeNodes
{
    /// <summary>The first node in <paramref name="roots"/> (depth-first) whose element id equals
    /// <paramref name="id"/>, or null.</summary>
    public static TreeNodeViewModel? FindById(IEnumerable<TreeNodeViewModel> roots, ElementId id) =>
        FindFirst(roots, n => n.ElementId == id);
    /// <summary>
    /// A row's VARIABLE NAME — its label with any rendered value suffix removed. A typed variable row reads
    /// <c>"Tal = 0"</c> (uxparity2 W8/T027: the value is rendered per type, in every section), so a test looking for
    /// the variable itself must not match on the whole label.
    /// </summary>
    public static string NameOf(TreeNodeViewModel node) =>
        node.DisplayName.Split(" = ", 2, StringSplitOptions.None)[0];
    /// <summary>The first PIN in <paramref name="roots"/> whose variable name is <paramref name="name"/>, ignoring
    /// any rendered value suffix.</summary>
    public static TreeNodeViewModel? FindPin(IEnumerable<TreeNodeViewModel> roots, string name) =>
        FindFirst(roots, n => n.IsPin && NameOf(n) == name);
    /// <summary>The first node in <paramref name="roots"/> (depth-first) matching <paramref name="match"/>, or null.</summary>
    public static TreeNodeViewModel? FindFirst(IEnumerable<TreeNodeViewModel> roots, Func<TreeNodeViewModel, bool> match)
    {
        foreach (TreeNodeViewModel node in roots)
        {
            if (match(node))
                return node;
            if (FindFirst(node.Children, match) is { } found)
                return found;
        }
        return null;
    }
}
