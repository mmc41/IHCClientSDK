using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>
/// Focused Avalonia-headless characterization retained from the original drag/drop proof of concept. Behavioral
/// routing, legality, highlighting, and move outcomes belong to DragDropInfrastructureTests and DragMoveTests.
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
