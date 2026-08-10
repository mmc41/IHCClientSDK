using ihc_openvisual.ViewModels;

namespace safe_unit_tests;

/// <summary>
/// T018 / US-068 / D07 — the tree-node context-menu gate predicates keyed by node type: Cut/Copy on the structural
/// nodes (a scene container adds Copy only), Move up/down (reorder) on the structural nodes only (never a pin,
/// section or scene container), and IsLocality as the single insert host.
/// </summary>
public class ContextMenuGateTests
{
    private static TreeNodeViewModel Node(TreeNodeKind kind) => new("n", "icon") { Kind = kind };

    [Test]
    public void ContextMenu_CutCopyReorder_OnStructuralNodesOnly()
    {
        Assert.Multiple(() =>
        {
            foreach (TreeNodeKind kind in new[] { TreeNodeKind.Locality, TreeNodeKind.Product, TreeNodeKind.FunctionBlock })
            {
                Assert.That(Node(kind).CanCut, Is.True, $"{kind} is cuttable");
                Assert.That(Node(kind).CanCopy, Is.True, $"{kind} is copyable");
                Assert.That(Node(kind).CanReorder, Is.True, $"{kind} is reorderable");
            }
        });
    }

    // Fix 3: a scene container offers Copy but NOT Cut, and is not reorderable.
    [Test]
    public void ContextMenu_SceneContainer_CopyableButNotCuttableOrReorderable()
    {
        TreeNodeViewModel scenes = Node(TreeNodeKind.Scenes);
        Assert.Multiple(() =>
        {
            Assert.That(scenes.CanCopy, Is.True, "a scene container offers Copy (US-068)");
            Assert.That(scenes.CanCut, Is.False, "a scene container has no Cut");
            Assert.That(scenes.CanReorder, Is.False, "a scene container is not reorderable");
        });
    }

    // Catalog/product pins and sections are neither cut/copy nor reorderable — no Move up/down on them (D07).
    [Test]
    public void ContextMenu_CatalogPinAndSection_NoCutCopyOrReorder()
    {
        var catalogPin = new TreeNodeViewModel("n", "icon")
        {
            Kind = TreeNodeKind.Pin,
            IsCatalogPin = true,
        };
        Assert.Multiple(() =>
        {
            foreach (TreeNodeViewModel node in new[] { catalogPin, Node(TreeNodeKind.Section) })
            {
                Assert.That(node.CanCut, Is.False, $"{node.Kind} is not cuttable");
                Assert.That(node.CanCopy, Is.False, $"{node.Kind} is not copyable");
                Assert.That(node.CanReorder, Is.False, $"{node.Kind} is not reorderable — no Move up/down");
            }
        });
    }

    [Test]
    public void ContextMenu_FunctionBlockVariablePin_CanCutAndCopyButCannotReorder()
    {
        TreeNodeViewModel variable = Node(TreeNodeKind.Pin);

        Assert.Multiple(() =>
        {
            Assert.That(variable.CanCut, Is.True, "S2-18: the vendor variable-row flyout offers Cut");
            Assert.That(variable.CanCopy, Is.True, "S2-18: the vendor variable-row flyout offers Copy");
            Assert.That(variable.CanReorder, Is.False, "the vendor variable-row flyout has no Move commands");
        });
    }

    // Fix 1 (node side): only a locality is the Insert product / Insert function block host.
    [Test]
    public void ContextMenu_IsLocality_OnlyForLocality()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Node(TreeNodeKind.Locality).IsLocality, Is.True);
            Assert.That(Node(TreeNodeKind.Product).IsLocality, Is.False);
            Assert.That(Node(TreeNodeKind.FunctionBlock).IsLocality, Is.False);
        });
    }
}
