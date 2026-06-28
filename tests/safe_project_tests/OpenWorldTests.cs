using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Open-world round-trip: a project containing element types and attributes the SDK registry does <em>not</em>
    /// know — but which the file's own inline DTD declares (e.g. a custom product/function-block authored in IHC
    /// Visual) — must load, edit and save <strong>byte-identically</strong>, sourcing each unknown type's grammar
    /// from the captured DTD rather than the static registry. Also locks encoding transparency (a file declaring
    /// ISO-8859-1 whose body bytes are UTF-8 is preserved verbatim, never "repaired") and the negative guard
    /// (content declared in neither the file's DTD nor the registry is rejected, not silently emitted).
    /// </summary>
    public class OpenWorldTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static Project Load(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            return new ProjectAppService(Settings).Load(ms).GetAwaiter().GetResult();
        }

        [Test]
        public void RoundTrip_CustomComponent_IsByteIdentical()
        {
            byte[] original = TestData.ReadBytes("OpenWorldCustomComponent.vis");

            byte[] reserialized = ProjectSerializer.Serialize(Load(original));

            TestData.AssertBytesIdentical(original, reserialized, "open-world custom-component round-trip");
        }

        [Test]
        public async Task Save_PreserveExistingMetadata_CustomComponent_IsByteIdentical()
        {
            byte[] original = TestData.ReadBytes("OpenWorldCustomComponent.vis");
            Project project = Load(original);

            using var ms = new MemoryStream();
            await new ProjectAppService(Settings).Save(project, ms, ProjectSaveOptions.PreserveExistingMetadata);

            TestData.AssertBytesIdentical(original, ms.ToArray(), "Save(PreserveExistingMetadata) of custom component");
        }

        [Test]
        public async Task MinimalEdit_DefaultSave_PreservesUnknownSubtree()
        {
            byte[] original = TestData.ReadBytes("OpenWorldCustomComponent.vis");
            Project before = Load(original);
            ProjectElement widgetBefore = FindByTag(before.Root, "custom_widget")!;

            // A real edit: the default save re-stamps id2/modified from the clock.
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 28, 9, 7, 53, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, A.Fake<ICatalog>(), clock);

            using var ms = new MemoryStream();
            await app.Save(before, ms, ProjectSaveOptions.Default);
            Project after = Load(ms.ToArray());
            ProjectElement widgetAfter = FindByTag(after.Root, "custom_widget")!;

            Assert.Multiple(() =>
            {
                Assert.That(after.Id2, Is.Not.EqualTo(before.Id2), "default save re-stamps id2 — a real edit happened");
                Assert.That(widgetAfter, Is.Not.Null, "the registry-unknown custom_widget survived the edit");
                Assert.That(widgetAfter, Is.EqualTo(widgetBefore), "the unknown subtree is preserved verbatim across a metadata re-stamp");
            });
        }

        [Test]
        public void RoundTrip_EncodingMismatch_IsByteIdentical()
        {
            byte[] original = TestData.ReadBytes("EncodingMismatchSwedish.vis");

            byte[] reserialized = ProjectSerializer.Serialize(Load(original));

            TestData.AssertBytesIdentical(original, reserialized, "encoding-mismatch (UTF-8 under ISO-8859-1) round-trip");
        }

        [Test]
        public void EncodingMismatch_LogicalValueIsMojibake_NotRepaired()
        {
            Project project = Load(TestData.ReadBytes("EncodingMismatchSwedish.vis"));

            // The name's bytes are UTF-8 (å ä ö é) under an ISO-8859-1 declaration; the SDK decodes per the declared
            // encoding, so the logical value is the Latin-1 mojibake (Ã¥Ã¤Ã¶Ã©), never the intended Swedish.
            string expectedMojibake = "Ã¥Ã¤Ã¶Ã©";
            Assert.That(project.Groups.First().GetAttribute("name"), Is.EqualTo(expectedMojibake));
        }

        [Test]
        public void Serialize_UndeclaredAttribute_Throws()
        {
            // 'bogus' is declared by neither the registry nor the file's own inline DTD → must not be silently emitted.
            Project project = Load(TestData.ReadBytes("OpenWorldUndeclaredAttr.vis"));

            Assert.Throws<InvalidOperationException>(() => ProjectSerializer.Serialize(project));
        }

        private static ProjectElement? FindByTag(ProjectElement element, string tag)
        {
            if (element.Tag == tag)
            {
                return element;
            }
            if (element.Children.IsDefaultOrEmpty)
            {
                return null;
            }
            foreach (ProjectElement child in element.Children)
            {
                ProjectElement? hit = FindByTag(child, tag);
                if (hit is not null)
                {
                    return hit;
                }
            }
            return null;
        }
    }
}
