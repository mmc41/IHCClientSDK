using System.Collections.Immutable;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Id tokens beyond the 32-bit packed range (24-bit counter + 8-bit type code, spec ch. 02) must be
    /// rejected as opaque — the previous unchecked <c>int</c> cast let two distinct on-disk tokens alias to
    /// one <see cref="ElementId"/>, misdirecting id-addressed edits and deletes. The allocator must refuse to
    /// pass its 24-bit ceiling without leaving a poisoned counter behind, and refuse corrupt seeds up front.
    /// </summary>
    public class IdentifierRangeTests
    {
        [Test]
        public void TryParse_OversizedTokens_AreRejected_NotAliased()
        {
            // 0x23456789ab >> 8 and (int)(0x123456789ab >> 8) both truncate to 0x23456789 — the aliasing pair.
            Assert.Multiple(() =>
            {
                Assert.That(ElementId.TryParse("_0x23456789ab", out _), Is.False);
                Assert.That(ElementId.TryParse("_0x123456789ab", out _), Is.False);
            });
        }

        [Test]
        public void TryParse_MaxLegalPackedValue_StillParses()
        {
            Assert.That(ElementId.TryParse("_0xffffffff", out ElementId id), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(id.Counter, Is.EqualTo(0xFFFFFF));
                Assert.That(id.TypeCode, Is.EqualTo(0xFF));
            });
        }

        [Test]
        public void Allocate_AtCeiling_Throws_WithoutAdvancingTheCounter()
        {
            var allocator = new IdAllocator(0xFFFFFE);

            ElementId last = allocator.Allocate(0x28);
            Assert.That(last.Counter, Is.EqualTo(0xFFFFFF), "the last legal counter is still mintable");

            Assert.That(() => allocator.Allocate(0x28), Throws.InvalidOperationException);
            Assert.That(allocator.LastUniqueIdToken, Is.EqualTo("_0xffffff"),
                "a failed allocation must not leave an out-of-range counter for a later save to persist");
        }

        [Test]
        public void Edit_CorruptLastUniqueId_ThrowsUpFront()
        {
            var root = new ProjectElement("utcs_project", null,
                ImmutableArray.Create(("version_major", "4"), ("last_unique_id", "_0x1ffffffffff")),
                ImmutableArray<ProjectElement>.Empty);

            Assert.That(() => new Project(root).Edit(),
                Throws.InstanceOf<System.IO.InvalidDataException>().With.Message.Contains("last_unique_id"));
        }

        [Test]
        public void Edit_DanglingIdRefAboveHighWaterMark_SeedsTheAllocatorPastIt()
        {
            // scenes@scene_resource is a schema-declared IDREF. Its target _0x7052 (counter 0x70) does not
            // exist and exceeds both last_unique_id (0x60) and every physical id — a fresh allocation must
            // not re-mint counter 0x70 and silently resurrect the dead reference.
            ProjectElement scenes = new("scenes",
                new ElementId(0x53, 0x49),
                ImmutableArray.Create(("id", "_0x5349"), ("name", "Scenarier"), ("scene_resource", "_0x7052")),
                ImmutableArray<ProjectElement>.Empty);
            ProjectElement root = new("utcs_project", null,
                ImmutableArray.Create(("version_major", "4"), ("last_unique_id", "_0x60")),
                ImmutableArray.Create(scenes));

            ProjectEditor editor = new Project(root).Edit();

            Assert.That(editor.Allocator.Counter, Is.EqualTo(0x70),
                "the seed folds in referenced counters, not just physical ids and last_unique_id");
        }
    }
}
