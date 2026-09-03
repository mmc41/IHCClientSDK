using System.Collections.Generic;
using System.ComponentModel;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;

namespace Ihc.Vis.Tests;

/// <summary>
/// fablerefac W3-5: the fields the W3-4 reconciler re-renders in place (label/icon/state) are observable, so a
/// <c>Changed</c> edit updates the bound row without rebuilding the node — node identity (selection/expansion/
/// scroll) survives by construction. Blackbox: subscribe to <see cref="INotifyPropertyChanged"/> and assert the
/// event fires (state-transition of each re-rendered field), including the computed <c>AccessibleName</c> peer.
/// </summary>
public class TreeNodePartialPropertyTests
{
    private static List<string> Track(TreeNodeViewModel node)
    {
        var raised = new List<string>();
        node.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);
        return raised;
    }

    [Test]
    public void RenameInPlace_RaisesDisplayNameAndAccessibleName_AndUpdatesValue()
    {
        var node = new TreeNodeViewModel("Stue", "/Assets/locality.svg");
        var raised = Track(node);

        node.DisplayName = "Køkken";

        Assert.Multiple(() =>
        {
            Assert.That(node.DisplayName, Is.EqualTo("Køkken"));
            Assert.That(raised, Does.Contain(nameof(TreeNodeViewModel.DisplayName)));
            // the accessible name folds in DisplayName, so it must re-announce when the label changes
            Assert.That(raised, Does.Contain(nameof(TreeNodeViewModel.AccessibleName)));
        });
    }

    [Test]
    public void UnlinkInPlace_RaisesIsUnlinkedAndAccessibleName()
    {
        var node = new TreeNodeViewModel("Trådløs", "/Assets/product.svg", isUnlinked: false);
        var raised = Track(node);

        node.IsUnlinked = true;

        Assert.Multiple(() =>
        {
            Assert.That(node.IsUnlinked, Is.True);
            Assert.That(node.AccessibleName, Does.Contain("ikke linket"));
            Assert.That(raised, Does.Contain(nameof(TreeNodeViewModel.IsUnlinked)));
            Assert.That(raised, Does.Contain(nameof(TreeNodeViewModel.AccessibleName)));
        });
    }

    [Test]
    public void ReRenderInPlace_RaisesIconBoldLockedAndTooltip()
    {
        var node = new TreeNodeViewModel("FB", "/Assets/fb-lk.svg", isBold: false, isLockedFunctionBlock: true)
        {
            Tooltip = null,
        };
        var raised = Track(node);

        node.IconAsset = "/Assets/fb-editable.svg";
        node.IsBold = true;
        node.IsLockedFunctionBlock = false;
        node.Tooltip = "Resource 1234567";

        Assert.Multiple(() =>
        {
            Assert.That(node.IconAsset, Is.EqualTo("/Assets/fb-editable.svg"));
            Assert.That(node.IsBold, Is.True);
            Assert.That(node.IsLockedFunctionBlock, Is.False);
            Assert.That(node.Tooltip, Is.EqualTo("Resource 1234567"));
            Assert.That(raised, Does.Contain(nameof(TreeNodeViewModel.IconAsset)));
            Assert.That(raised, Does.Contain(nameof(TreeNodeViewModel.IsBold)));
            Assert.That(raised, Does.Contain(nameof(TreeNodeViewModel.IsLockedFunctionBlock)));
            Assert.That(raised, Does.Contain(nameof(TreeNodeViewModel.Tooltip)));
        });
    }
}
