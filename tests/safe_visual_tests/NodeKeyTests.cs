using System.Collections.Generic;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>
/// fablerefac W3-2: the tree reconciler's row identity. A <see cref="NodeKey"/> must give value equality +
/// hashing so it is a stable, alloc-free dictionary key — one form wrapping an element id (element-backed rows),
/// one synthetic <c>(owner, role[, refId])</c> form for structural rows that own no element of their own
/// (variable sections, "Programmer", Events/Commands containers, link rows, scene members).
/// </summary>
public class NodeKeyTests
{
    private static ElementId Id(int counter, int typeCode = 1) => new(counter, typeCode);

    [Test]
    public void ElementBacked_SameId_AreEqualAndHashEqual()
    {
        NodeKey a = NodeKey.ForElement(Id(7));
        NodeKey b = NodeKey.ForElement(Id(7));
        Assert.Multiple(() =>
        {
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a.IsElementBacked, Is.True);
        });
    }

    [Test]
    public void ElementBacked_DifferentId_AreNotEqual()
    {
        Assert.That(NodeKey.ForElement(Id(7)), Is.Not.EqualTo(NodeKey.ForElement(Id(8))));
    }

    [Test]
    public void Structural_SameOwnerAndRole_AreEqualAndHashEqual()
    {
        NodeKey a = NodeKey.ForStructural(Id(3), "events");
        NodeKey b = NodeKey.ForStructural(Id(3), "events");
        Assert.Multiple(() =>
        {
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a.IsElementBacked, Is.False);
        });
    }

    // Spec-named case: two distinct roles under the same owner must not collide (e.g. the Events container and
    // the Commands container of one program share an owner but are different rows).
    [Test]
    public void Structural_DistinctRolesSameOwner_DoNotCollide()
    {
        NodeKey events = NodeKey.ForStructural(Id(3), "events");
        NodeKey commands = NodeKey.ForStructural(Id(3), "commands");
        Assert.That(events, Is.Not.EqualTo(commands));
    }

    // An element-backed key and a synthetic key whose OWNER shares the element's numeric id must not collide:
    // the row that stands for element X is not the "programs container of X".
    [Test]
    public void ElementBacked_DoesNotCollideWithStructuralOfSameId()
    {
        ElementId id = Id(5);
        Assert.That(NodeKey.ForElement(id), Is.Not.EqualTo(NodeKey.ForStructural(id, "programs")));
    }

    // A refId discriminates rows that share (owner, role) — two link rows under one pin distinguished by partner.
    [Test]
    public void Structural_RefId_Discriminates()
    {
        NodeKey toA = NodeKey.ForStructural(Id(9), "linkTo", Id(100));
        NodeKey toB = NodeKey.ForStructural(Id(9), "linkTo", Id(200));
        NodeKey toADuplicate = NodeKey.ForStructural(Id(9), "linkTo", Id(100));
        Assert.Multiple(() =>
        {
            Assert.That(toA, Is.Not.EqualTo(toB));
            Assert.That(toA, Is.EqualTo(toADuplicate));
            // a refId-less structural key differs from one carrying a refId under the same (owner, role)
            Assert.That(NodeKey.ForStructural(Id(9), "linkTo"), Is.Not.EqualTo(toA));
        });
    }

    // The whole point: usable as a dictionary key via an equal-but-distinct instance, across both forms.
    [Test]
    public void UsableAsDictionaryKey_AcrossBothForms()
    {
        var map = new Dictionary<NodeKey, string>
        {
            [NodeKey.ForElement(Id(1))] = "element",
            [NodeKey.ForStructural(Id(1), "programs")] = "programs",
        };
        Assert.Multiple(() =>
        {
            Assert.That(map[NodeKey.ForElement(Id(1))], Is.EqualTo("element"));
            Assert.That(map[NodeKey.ForStructural(Id(1), "programs")], Is.EqualTo("programs"));
            Assert.That(map, Has.Count.EqualTo(2));
        });
    }
}
