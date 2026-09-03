using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Model;

namespace Ihc.Vis.Tests;

/// <summary>
/// The one characterization retained from the original drag/drop proof of concept: that the payload the drag
/// side builds is the payload the drop side reads back. Behavioral routing, legality, highlighting and move
/// outcomes belong to DragDropInfrastructureTests and DragMoveTests.
///
/// <para>It touches an Avalonia TYPE — the payload is a <c>DataTransfer</c> — but needs no application, window
/// or dispatcher, which is why it sits here rather than in the toolkit suite: the routing rule excludes what
/// needs EXECUTION in Avalonia, not what names one of its types.</para>
/// </summary>
public class DragDropPocTests
{
    [Test]
    public void DataTransferPayload_RoundTripsTheAddressableNodeId()
    {
        var id = new ElementId(0x10, 1);
        var node = new TreeNodeViewModel("Product", "icon", elementId: id);

        var payload = TreeDragData.BuildDragData(node);

        Assert.Multiple(() =>
        {
            Assert.That(payload, Is.Not.Null, "an addressable tree node creates an Avalonia DataTransfer payload");
            Assert.That(TreeDragData.TryGetElementId(payload), Is.EqualTo(id),
                "the drop side recovers the exact stable element id from that payload");
        });
    }
}
