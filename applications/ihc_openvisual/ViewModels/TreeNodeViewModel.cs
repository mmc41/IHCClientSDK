using System.Collections.ObjectModel;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// A node in one of the two tree panes. For epic E1 the trees are the locality skeleton (a <c>Localities</c>
/// root over the project's rooms); products, function blocks and pins are added by later epics, so children
/// are exposed generically here.
/// </summary>
public sealed class TreeNodeViewModel
{
    public TreeNodeViewModel(string displayName, string iconAsset, bool isExpanded = false)
    {
        DisplayName = displayName;
        IconAsset = iconAsset;
        IsExpanded = isExpanded;
    }

    public string DisplayName { get; }

    /// <summary>The <c>/Assets/*.svg</c> glyph rendered beside the label (per the icon-mapping doc).</summary>
    public string IconAsset { get; }

    /// <summary>Whether the node is expanded by default (the <c>Localities</c> root is; rooms are collapsed).</summary>
    public bool IsExpanded { get; }

    public ObservableCollection<TreeNodeViewModel> Children { get; } = new();
}
