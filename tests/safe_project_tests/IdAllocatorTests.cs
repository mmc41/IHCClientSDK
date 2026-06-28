using System.Collections.Immutable;
using Ihc.Projects;

namespace Ihc.Projects.Tests
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
