using System.Collections.Immutable;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// The save/upload postcondition (<see cref="ProjectSaveOptions.VerifyRoundTrip"/>): the just-serialized
    /// bytes are re-parsed and compared semantically with the written model, so state the format cannot
    /// represent is revealed at the write instead of surfacing as a silently different file. UploadTo always
    /// verifies — controller EPROM has no <c>.BAK</c> to roll back to.
    /// </summary>
    public class RoundTripVerificationTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static ProjectElement Node(string tag, string? id, (string, string)[] attrs, params ProjectElement[] children)
        {
            ElementId? parsed = id is not null && ElementId.TryParse(id, out ElementId p) ? p : null;
            var bag = ImmutableArray.CreateBuilder<(string, string)>();
            if (id is not null)
            {
                bag.Add(("id", id));
            }
            bag.AddRange(attrs);
            return new ProjectElement(tag, parsed, bag.ToImmutable(), children.ToImmutableArray());
        }

        // group@icon defaults to "_0x0": physically present and equal to the default, it is dropped on write
        // (omit-if-default), so the re-parsed model differs — exactly the state the postcondition must reveal.
        private static Project UnrepresentableProject() => new(Node("utcs_project", null,
            new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x40") },
            Node("groups", "_0x2031", new[] { ("name", "L") },
                Node("group", "_0x2132", new[] { ("name", "Stue"), ("icon", "_0x0") }))));

        [Test]
        public async Task Save_WithVerifyRoundTrip_OnAnAuthenticFile_Succeeds()
        {
            var app = new ProjectAppService(Settings);
            Project project = await app.Load("testdata/Project1-SimpelWired.vis");
            using var ms = new MemoryStream();

            var options = new ProjectSaveOptions { WriteMetadataVerbatim = true, VerifyRoundTrip = true };
            await app.Save(project, ms, options);

            Assert.That(ms.Length, Is.GreaterThan(0));
        }

        [Test]
        public void Save_WithVerifyRoundTrip_RevealsUnrepresentableModelState()
        {
            var app = new ProjectAppService(Settings);
            using var ms = new MemoryStream();
            var options = new ProjectSaveOptions { WriteMetadataVerbatim = true, VerifyRoundTrip = true };

            Assert.That(async () => await app.Save(UnrepresentableProject(), ms, options),
                Throws.InvalidOperationException.With.Message.Contains("re-parse").And.Message.Contains("icon"),
                "the divergence names the offending attribute");
        }

        [Test]
        public void UploadTo_AlwaysVerifiesTheWrite()
        {
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._)).Returns(true);
            var app = new ProjectAppService(Settings, A.Fake<ICatalog>(),
                new FakeTimeProvider(new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero)), controller);

            Assert.That(async () => await app.UploadTo(UnrepresentableProject(), ProjectSaveOptions.PreserveExistingMetadata),
                Throws.InvalidOperationException.With.Message.Contains("re-parse"),
                "a model the format cannot faithfully persist must never reach controller EPROM");
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._)).MustNotHaveHappened();
        }
    }
}
