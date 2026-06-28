using System.IO;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// The Stage-2 byte-identity gate: a project loaded from a vendor <c>.vis</c> file and re-serialized with its
    /// metadata preserved must be <strong>byte-for-byte</strong> what IHC Visual wrote. Covers both the empty
    /// project (13 element types) and the complex sample <c>Project1.vis</c> (38 element types) — together the
    /// byte-verifiable element set — through both the pure <see cref="ProjectSerializer"/> and the
    /// <see cref="ProjectAppService"/> save path. A separate test pins the clock and verifies the default
    /// (vendor-like) save re-stamps only <c>id2</c>/<c>modified</c>.
    /// </summary>
    public class ProjectByteFidelityTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static Project Load(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            return new ProjectAppService(Settings).Load(ms).GetAwaiter().GetResult();
        }

        [TestCase("ProjectEmpty.vis")]
        [TestCase("Project1.vis")]
        public void Serialize_RoundTrip_IsByteIdentical(string file)
        {
            byte[] original = TestData.ReadBytes(file);

            byte[] reserialized = ProjectSerializer.Serialize(Load(original));

            TestData.AssertBytesIdentical(original, reserialized, $"ProjectSerializer round-trip of {file}");
        }

        [TestCase("ProjectEmpty.vis")]
        [TestCase("Project1.vis")]
        public async Task Save_PreserveExistingMetadata_IsByteIdentical(string file)
        {
            byte[] original = TestData.ReadBytes(file);
            Project project = Load(original);
            var app = new ProjectAppService(Settings);

            using var ms = new MemoryStream();
            await app.Save(project, ms, ProjectSaveOptions.PreserveExistingMetadata);

            TestData.AssertBytesIdentical(original, ms.ToArray(), $"Save(PreserveExistingMetadata) of {file}");
        }

        [Test]
        public async Task Save_Default_ReStampsId2AndModified_LeavesIdentityUntouched()
        {
            byte[] original = TestData.ReadBytes("Project1.vis");
            Project before = Load(original);

            // Pin the save clock to the 28th, 09:07:53 → id2 packs day/hour/minute/second.
            var clock = new FakeTimeProvider(new System.DateTimeOffset(2026, 6, 28, 9, 7, 53, System.TimeSpan.Zero));
            var app = new ProjectAppService(Settings, A.Fake<ICatalog>(), clock);

            using var ms = new MemoryStream();
            await app.Save(before, ms, ProjectSaveOptions.Default);
            Project after = Load(ms.ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(after.Id2, Is.EqualTo(PackedStamp.FromDateTime(clock.GetLocalNow()).ToToken()),
                    "default save re-stamps id2 from the clock");
                ProjectElement modified = after.Child("modified")!;
                Assert.That(modified.GetAttribute("day"), Is.EqualTo("28"));
                Assert.That(modified.GetAttribute("hour"), Is.EqualTo("9"));
                Assert.That(modified.GetAttribute("minute"), Is.EqualTo("7"));

                Assert.That(after.Id1, Is.EqualTo(before.Id1), "id1 (creation stamp) is left untouched");
                Assert.That(after.LastUniqueId, Is.EqualTo(before.LastUniqueId), "no allocation → last_unique_id unchanged");
                Assert.That(after.Groups.Count, Is.EqualTo(before.Groups.Count));
                Assert.That(after.Child("groups")!.GetAttribute("id"),
                    Is.EqualTo(before.Child("groups")!.GetAttribute("id")), "existing element ids are immutable");
            });
        }
    }
}
