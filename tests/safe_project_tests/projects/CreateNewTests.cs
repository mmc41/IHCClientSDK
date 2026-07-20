using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The CREATE byte-identity gate (install-free, spec ch. 10 §10.7): <see cref="ProjectAppService.CreateNew"/>
    /// takes the code-authored <c>NewProjectSkeleton</c> + <c>BuiltInEnumerators</c> templates from
    /// <see cref="BuiltInCatalog"/> (no IHC Visual install required) and, with a pinned clock and the testdata field
    /// values, reproduces <c>Project0-Tomt.vis</c> byte-for-byte after a default save re-stamps <c>id2</c>/<c>modified</c>.
    /// The install-dir differential in <see cref="BuiltInCatalogTemplateDifferentialTests"/> proves those templates
    /// equal the vendor files; this suite proves they drive a byte-identical create.
    /// </summary>
    public class CreateNewTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static ICatalog Catalog() => new BuiltInCatalog();

        [Test]
        public async Task CreateNew_ThenDefaultSave_ReproducesProjectEmpty_ByteIdentical()
        {
            ICatalog catalog = Catalog();

            // Creation: 27th 16:05:51 → id1 = id2 = _0x1b100533, modified minute 5.
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 27, 16, 5, 51, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, catalog, clock);

            Project project = app.CreateNew(new ProjectDetails(
                Programmer: "Morten Christensen", InstallerName: "Morten", InstallerCountry: "Danmark"));

            // The first save (vendor-like) advances the clock 14s to 16:06:05 → id2 = _0x1b100605, modified minute 6.
            clock.SetUtcNow(new DateTimeOffset(2026, 6, 27, 16, 6, 5, TimeSpan.Zero));
            using var ms = new MemoryStream();
            await app.Save(project, ms, ProjectSaveOptions.Default);

            TestData.AssertBytesIdentical(TestData.ReadBytes("projects/Project0-Tomt.vis"), ms.ToArray(), "CreateNew + default save");
        }

        [Test]
        public void CreateNew_SeedsTenRooms_TwoBuiltInEnums_AndDocumentationModules()
        {
            ICatalog catalog = Catalog();
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 27, 16, 5, 51, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, catalog, clock);

            Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));

            Assert.Multiple(() =>
            {
                Assert.That(project.Version, Is.EqualTo("4.0"));
                Assert.That(project.Groups.Count, Is.EqualTo(10), "ten default rooms");
                Assert.That(project.LastUniqueId, Is.EqualTo("_0x50"), "counter ends at 0x50");
                Assert.That(project.Child("enum_definitions")!.Children.Length, Is.EqualTo(2), "two built-in enums");
                Assert.That(project.Child("documentation_modules"), Is.Not.Null);
            });
        }

        // T017: NewProjectBuilder.Node routes through ProjectElement.Create. The enum_definitions container id is
        // the one id fed to Node as a raw skeleton-derived string (every other id is an allocator .ToToken()); it
        // must survive CreateNew as the canonical "_0x3046" (Create re-renders it via ToToken byte-identically).
        [Test]
        public void CreateNew_EnumDefinitionsContainer_KeepsCanonicalId()
        {
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 27, 16, 5, 51, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, Catalog(), clock);

            Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));

            ProjectElement enums = project.Child("enum_definitions")!;
            Assert.Multiple(() =>
            {
                Assert.That(enums.GetAttribute("id"), Is.EqualTo("_0x3046"), "container id attribute preserved");
                Assert.That(enums.Id, Is.EqualTo(new ElementId(0x30, 0x46)), "and its strongly-typed id agrees");
            });
        }

        // M3 / 3.2 — seed-layout knob. project2-CustomBlock seeds the three documentation *_modules FIRST
        // (counters 65-67) and the two built-in enums AFTER (68-80) — the reverse of Project0/Project1 (anomaly A-1).
        // Document emission order is unchanged; only the seed ids differ.
        [Test]
        public void CreateNew_ModulesFirstLayout_MatchesProject2SeedAllocation()
        {
            ICatalog catalog = Catalog();
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 3, 7, 23, 34, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, catalog, clock);

            Project project = app.CreateNew(new ProjectDetails("Morten Christensen", "Morten", "Danmark"),
                SeedIdLayout.ModulesFirst);

            ProjectElement modules = project.Child("documentation_modules")!;
            ProjectElement enums = project.Child("enum_definitions")!;

            Assert.Multiple(() =>
            {
                Assert.That(modules.Id!.Value.Counter, Is.EqualTo(0x41), "documentation_modules allocated first (65)");
                Assert.That(modules.Children[0].Id!.Value.Counter, Is.EqualTo(0x42), "dataline_input_modules (66)");
                Assert.That(modules.Children[1].Id!.Value.Counter, Is.EqualTo(0x43), "dataline_output_modules (67)");
                Assert.That(enums.Children[0].Id!.Value.Counter, Is.EqualTo(0x44), "Persienne tilstand def allocated after modules (68)");
                Assert.That(enums.Children[1].Id!.Value.Counter, Is.EqualTo(0x4a), "Logning def (74)");
                Assert.That(project.LastUniqueId, Is.EqualTo("_0x50"), "counter still ends at 0x50 (80)");
            });
        }

        // The default (EnumsFirst) allocates the built-in enums first (65-77) then the modules (78-80) — Project0/Project1.
        [Test]
        public void CreateNew_EnumsFirstLayout_IsTheDefault_EnumsBeforeModules()
        {
            ICatalog catalog = Catalog();
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 27, 16, 5, 51, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, catalog, clock);

            Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));   // default = EnumsFirst

            ProjectElement modules = project.Child("documentation_modules")!;
            ProjectElement enums = project.Child("enum_definitions")!;

            Assert.Multiple(() =>
            {
                Assert.That(enums.Children[0].Id!.Value.Counter, Is.EqualTo(0x41), "Persienne def allocated first (65)");
                Assert.That(enums.Children[1].Id!.Value.Counter, Is.EqualTo(0x47), "Logning def (71)");
                Assert.That(modules.Id!.Value.Counter, Is.EqualTo(0x4e), "documentation_modules allocated after enums (78)");
                Assert.That(project.LastUniqueId, Is.EqualTo("_0x50"), "counter still ends at 0x50 (80)");
            });
        }
    }
}
