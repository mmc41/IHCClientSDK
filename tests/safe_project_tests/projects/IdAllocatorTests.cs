using System.Collections.Immutable;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Unit tests for the project-wide id allocator: pre-increment, type-suffix packing, and high-water-mark
    /// seeding that never trusts a too-low <c>last_unique_id</c> (spec ch. 02 §2.5).
    /// </summary>
    public class IdAllocatorTests
    {
        [Test]
        public void Allocate_PreIncrements_AndPacksTypeCode()
        {
            var alloc = new IdAllocator(0x40);

            ElementId first = alloc.Allocate(0x47);   // enum_definition
            ElementId second = alloc.Allocate(0x48);  // enum_value

            Assert.Multiple(() =>
            {
                Assert.That(first.ToToken(), Is.EqualTo("_0x4147"));
                Assert.That(second.ToToken(), Is.EqualTo("_0x4248"));
                Assert.That(alloc.LastUniqueIdToken, Is.EqualTo("_0x42"));
            });
        }

        [Test]
        public void MintMissingIds_PresentButUnparseableIdToken_IsPreserved_NoBurn()
        {
            // Finding 16: a node whose id attribute is present but unparseable (the vendor typo "_05", missing the
            // "0x") must keep its token verbatim. Minting off the null Id would burn a counter id AND leave the
            // attribute disagreeing with the minted Id (the old rewrite guard never touched a present attribute).
            var alloc = new IdAllocator(0x40);
            ProjectElement node = Tree.Node("group", "_05", new[] { ("name", "X") });

            ProjectElement result = alloc.MintMissingIds(node);

            Assert.Multiple(() =>
            {
                Assert.That(result.GetAttribute("id"), Is.EqualTo("_05"), "the unparseable token is preserved verbatim");
                Assert.That(result.Id, Is.Null, "no parseable id is fabricated");
                Assert.That(alloc.LastUniqueIdToken, Is.EqualTo("_0x40"), "no counter id is burned");
            });
        }

        [Test]
        public void ForProject_SeedsFromHighWaterMark_NotTooLowAttribute()
        {
            // last_unique_id says _0x05 but a child counter is already 0x21 -> seed must be 0x21, not 0x05.
            var child = new ProjectElement("group", new ElementId(0x21, 0x32),
                ImmutableArray<(string, string)>.Empty, ImmutableArray<ProjectElement>.Empty);
            var root = new ProjectElement("utcs_project", null,
                ImmutableArray.Create(("last_unique_id", "_0x05")),
                ImmutableArray.Create(child));

            IdAllocator alloc = IdAllocator.ForProject(new Project(root));
            ElementId next = alloc.Allocate(0x32);

            Assert.That(next.ToToken(), Is.EqualTo("_0x2232"));   // 0x21 + 1 = 0x22
        }
    }
}
