using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// BL-6 — from-scratch empty function block ("Tom blok"). <see cref="GroupRef.AddEmptyFunctionBlock"/> scaffolds
    /// the mandatory five containers in fixed order (<c>inputs, outputs, settings, internalsettings, programs</c>)
    /// plus one empty <c>program_simple(events, actions)</c>, with vendor icon <c>_0xf</c> and only the
    /// <c>master_date_*</c> attributes among <c>master_*</c> — sourced from the install dir's <c>Data\fb.def</c>
    /// template via <see cref="ICatalog.EmptyFunctionBlockTemplate"/>. Oracle: the three empty <c>Tom blok</c> blocks
    /// in <c>project3-KompleksWired.vis</c> (lines 2253/2399/2423). Install-dir gated.
    /// </summary>
    public class FunctionBlockAuthoringTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static ICatalog RequireCatalog()
        {
            string dir = Settings.IhcVisualInstallDir;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Assert.Ignore($"No IHC Visual install dir configured ('{dir}'); skipping install-dir-gated test.");
            }
            return CatalogDiscovery.FromInstallDir(dir);
        }

        private static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero));

        private static ProjectElement AddBlock(ICatalog catalog, string name, out Project built)
        {
            var app = new ProjectAppService(Settings, catalog, Clock());
            ProjectEditor editor = app.CreateNew(new ProjectDetails("P", "I", "DK")).Edit();
            editor.Group("Stue").AddEmptyFunctionBlock(catalog.EmptyFunctionBlockTemplate, new DateOnly(2026, 6, 29), name);
            built = editor.ToProject();
            return built.Root.Descendants().First(e => e.Tag == "functionblock");
        }

        [Test]
        public void AddEmptyFunctionBlock_ScaffoldsFiveContainersInFixedOrder()
        {
            ICatalog catalog = RequireCatalog();
            ProjectElement fb = AddBlock(catalog, "Tom blok", out _);

            string[] containers = fb.Children.Select(c => c.Tag).ToArray();
            Assert.That(containers, Is.EqualTo(new[] { "inputs", "outputs", "settings", "internalsettings", "programs" }),
                "exactly the five containers, in the fixed vendor order");
        }

        [Test]
        public void AddEmptyFunctionBlock_HasEmptyProgramSimpleWithEventsAndActions()
        {
            ICatalog catalog = RequireCatalog();
            ProjectElement fb = AddBlock(catalog, "Tom blok", out _);

            ProjectElement programs = fb.FindChild("programs")!;
            ProjectElement programSimple = programs.FindChild("program_simple")!;

            Assert.Multiple(() =>
            {
                Assert.That(programs.Children.Select(c => c.Tag), Is.EqualTo(new[] { "program_simple" }));
                Assert.That(programSimple.Children.Select(c => c.Tag), Is.EqualTo(new[] { "events", "actions" }));
                Assert.That(programSimple.FindChild("events")!.Children, Is.Empty, "events is empty");
                Assert.That(programSimple.FindChild("actions")!.Children, Is.Empty, "actions is empty");
            });
        }

        [Test]
        public void AddEmptyFunctionBlock_HasVendorIconAndOnlyDateMasterAttributes()
        {
            ICatalog catalog = RequireCatalog();
            ProjectElement fb = AddBlock(catalog, "Tom blok", out _);

            Assert.Multiple(() =>
            {
                Assert.That(fb.GetAttribute("icon"), Is.EqualTo("_0xf"));
                Assert.That(fb.GetAttribute("name"), Is.EqualTo("Tom blok"));
                Assert.That(fb.GetAttribute("master_date_year"), Is.EqualTo("2026"));
                Assert.That(fb.GetAttribute("master_date_month"), Is.EqualTo("6"));
                Assert.That(fb.GetAttribute("master_date_day"), Is.EqualTo("29"));
                Assert.That(fb.GetAttribute("master_schneider_electric"), Is.Null, "no LK master identity");
                Assert.That(fb.GetAttribute("master_type"), Is.Null);
                Assert.That(fb.GetAttribute("master_name"), Is.Null);
                Assert.That(fb.GetAttribute("locked"), Is.Null, "an authored block is not locked");
            });
        }

        [Test]
        public void AddEmptyFunctionBlock_ContainersCarryVendorNames_AndFunctionblockSuffix()
        {
            ICatalog catalog = RequireCatalog();
            ProjectElement fb = AddBlock(catalog, "Tom blok", out Project built);

            Assert.Multiple(() =>
            {
                Assert.That(ElementId.TryParse(fb.GetAttribute("id"), out ElementId id) && id.TypeCode == 0x28,
                    Is.True, "functionblock keeps type-code suffix 0x28");
                Assert.That(fb.FindChild("inputs")!.GetAttribute("name"), Is.EqualTo("Input"));
                Assert.That(fb.FindChild("internalsettings")!.GetAttribute("name"), Is.EqualTo("Interne variable"));
                Assert.That(new ProjectAppService(Settings).Validate(built).IsValid, Is.True);
            });
        }

        [Test]
        public void AddEmptyFunctionBlock_HonorsCustomName()
        {
            ICatalog catalog = RequireCatalog();
            ProjectElement fb = AddBlock(catalog, "Custom blok", out _);

            Assert.That(fb.GetAttribute("name"), Is.EqualTo("Custom blok"));
        }
    }
}
