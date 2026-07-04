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

        // G1 (step 3.7): a resource authored by hand into an empty block must arrive fully materialized — the
        // per-type GUI icon IHC Visual stamps on creation and, for the value types whose value attributes are
        // #REQUIRED (never omittable), the vendor's initial values — exactly as a catalog insert already gets from
        // the component .def's DTD defaults. Without this the authoring path emits only id+name and diverges from
        // project2-CustomBlock.vis. Blackbox: assert the SAVED (canonicalized) resource.
        private static ProjectElement AuthorResource(ICatalog catalog, Action<FunctionBlockRef> author, string tag, string name)
        {
            var app = new ProjectAppService(Settings, catalog, Clock());
            ProjectEditor editor = app.CreateNew(new ProjectDetails("P", "I", "DK")).Edit();
            FunctionBlockRef fb = editor.Group("Stue")
                .AddEmptyFunctionBlock(catalog.EmptyFunctionBlockTemplate, new DateOnly(2026, 6, 29), "Custom blok");
            author(fb);
            Project built = editor.ToProject();
            return built.Root.Descendants().First(e => e.Tag == tag && e.GetAttribute("name") == name);
        }

        [Test]
        public void AddInput_StampsCanonicalPinIcon()
        {
            ICatalog catalog = RequireCatalog();
            ProjectElement input = AuthorResource(catalog, fb => fb.AddInput("Indgang"), "resource_input", "Indgang");

            Assert.That(input.GetAttribute("icon"), Is.EqualTo("_0x36"));
        }

        [Test]
        public void AddSetting_DateType_StampsIconAndRequiredValueInitials()
        {
            ICatalog catalog = RequireCatalog();
            ProjectElement date = AuthorResource(catalog, fb => fb.AddSetting("resource_date", "Dato"), "resource_date", "Dato");

            Assert.Multiple(() =>
            {
                Assert.That(date.GetAttribute("icon"), Is.EqualTo("_0x29"));
                Assert.That(date.GetAttribute("year"), Is.EqualTo("2000"));
                Assert.That(date.GetAttribute("month"), Is.EqualTo("1"));
                Assert.That(date.GetAttribute("day"), Is.EqualTo("1"));
            });
        }

        [Test]
        public void AddSetting_TimerType_StampsIconAndAllFourRequiredValueInitials()
        {
            ICatalog catalog = RequireCatalog();
            ProjectElement timer = AuthorResource(catalog, fb => fb.AddSetting("resource_timer", "Timer"), "resource_timer", "Timer");

            Assert.Multiple(() =>
            {
                Assert.That(timer.GetAttribute("icon"), Is.EqualTo("_0x43"));
                Assert.That(timer.GetAttribute("hour"), Is.EqualTo("0"));
                Assert.That(timer.GetAttribute("minute"), Is.EqualTo("0"));
                Assert.That(timer.GetAttribute("second"), Is.EqualTo("0"));
                Assert.That(timer.GetAttribute("millisecond"), Is.EqualTo("0"));
            });
        }

        [Test]
        public void AddSetting_TypeWithNoCanonicalIcon_LeavesIconUnstamped()
        {
            ICatalog catalog = RequireCatalog();
            ProjectElement tal = AuthorResource(catalog, fb => fb.AddSetting("resource_integer", "Tal"), "resource_integer", "Tal");

            // resource_integer carries no canonical icon in any oracle → icon stays the DTD default _0x0, elided on save.
            Assert.That(tal.GetAttribute("icon"), Is.Null);
        }

        // G2 (step 3.7): a custom block's inputs/outputs containers hold value types too (project2's Custom blok has a
        // resource_enum at inputs-162 and outputs-181, a resource_date at inputs-160/outputs-180, etc.), so AddInput/
        // AddOutput must accept an explicit type + configure — not only the resource_input/resource_output shorthands.
        [Test]
        public void AddInput_TypedEnum_LandsInInputsWithTypedefInivalueAndIcon()
        {
            ICatalog catalog = RequireCatalog();
            var app = new ProjectAppService(Settings, catalog, Clock());
            ProjectEditor editor = app.CreateNew(new ProjectDetails("P", "I", "DK")).Edit();
            FunctionBlockRef fb = editor.Group("Stue")
                .AddEmptyFunctionBlock(catalog.EmptyFunctionBlockTemplate, new DateOnly(2026, 6, 29), "Custom blok");
            EnumDefinitionRef def = editor.AddEnumDefinition("NyType", "Værdi1", "Værdi2");
            fb.AddInput("resource_enum", "NyEnumInput",
                e => e.SetAttribute("typedef", def.Typedef).SetAttribute("inivalue", def.InitialValue("Værdi1")));

            ProjectElement inputs = editor.ToProject().Root.Descendants().First(e => e.Tag == "functionblock").FindChild("inputs")!;
            ProjectElement enumInput = inputs.Children.First(c => c.GetAttribute("name") == "NyEnumInput");
            Assert.Multiple(() =>
            {
                Assert.That(enumInput.Tag, Is.EqualTo("resource_enum"));
                Assert.That(enumInput.GetAttribute("typedef"), Is.EqualTo(def.Typedef));
                Assert.That(enumInput.GetAttribute("inivalue"), Is.EqualTo(def.InitialValue("Værdi1")));
                Assert.That(enumInput.GetAttribute("icon"), Is.EqualTo("_0x22"));
            });
        }

        // G3 (step 3.7): unlike a product (whose catalog deep-copy pre-populates its I/O, so AddInput upserts a
        // same-named child in place), a hand-authored function-block resource is always a NEW node — project2's Custom
        // blok legitimately holds two "Kommatal" outputs (185/186) and two "Scenarie" outputs (192/193). Two adds must
        // allocate two distinct ids, never merge.
        [Test]
        public void AddOutput_SameNameTwice_CreatesTwoDistinctResources_NeverUpserts()
        {
            ICatalog catalog = RequireCatalog();
            var app = new ProjectAppService(Settings, catalog, Clock());
            ProjectEditor editor = app.CreateNew(new ProjectDetails("P", "I", "DK")).Edit();
            FunctionBlockRef fb = editor.Group("Stue")
                .AddEmptyFunctionBlock(catalog.EmptyFunctionBlockTemplate, new DateOnly(2026, 6, 29), "Custom blok");
            ResourceRef first = fb.AddOutput("Udgang");
            ResourceRef second = fb.AddOutput("Udgang");

            ProjectElement outputs = editor.ToProject().Root.Descendants().First(e => e.Tag == "functionblock").FindChild("outputs")!;
            ProjectElement[] udgange = outputs.Children.Where(c => c.GetAttribute("name") == "Udgang").ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(udgange.Length, Is.EqualTo(2), "hand-authored FB resources never upsert — two adds = two nodes");
                Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            });
        }
    }
}
