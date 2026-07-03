using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// The E2E authoring byte-fidelity track (E13) — reconstruct each authentic oracle from scratch through the
    /// public builder API and assert byte-identity via the <see cref="BuildFidelity"/> harness (BL-E0). BL-E1
    /// (Project0-Tomt via template <see cref="ProjectAppService.CreateNew"/>) is the reference pattern that
    /// establishes the harness; the content builds (BL-E2…E4) follow. Install-dir gated.
    /// </summary>
    public class AuthoringByteFidelityTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        // BL-E2: reconstructs Project1-SimpelWired.vis from scratch through the public builders and drives the
        // harness to byte-identity (88,321 = 88,321). The former "6-byte / enum-hoist id-allocation-order" gap was
        // reverse-engineered (tmp/experiments/out/A1..A3) to be neither an enum-hoist nor an InsertTransform issue,
        // but pure USER-ACTION ORDER: IHC Visual allocates ids at action time, and the vendor wired the three Kip
        // links (6 ids) right after the Kip block — before starting the Entré room — whereas the original test
        // created all links last, shifting every later element by −6. Placing the Kip links immediately after the
        // Kip block (output link first, matching the vendor's 193–198 order) closes the gap with zero engine changes.
        // Install-dir gated via RequireCatalog.
        [Test]
        public async Task BL_E2_ReproducesProject1SimpelWired_FromFluentBuilders()
        {
            ICatalog cat = BuildFidelity.RequireCatalog(Settings);
            // Creation 27th 14:58:31 → id1 = _0x1b0e3a1f; the default save re-stamps at 15:05:27 → id2/modified.
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 27, 14, 58, 31, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, cat, clock);

            await BuildFidelity.AssertByteIdentical(app, "Project1-SimpelWired.vis",
                build: () =>
                {
                    ProjectEditor editor = app
                        .CreateNew(new ProjectDetails("Morten Christensen", "Morten", "Danmark"))
                        .Edit();

                    GroupRef stue = editor.Group("Stue");
                    ProductRef fuga = stue.AddProduct(cat.Product("_0x2101"))
                        .Name("LK FUGA Tryk 2 tast").Locked().EnduserReport()
                        .Note("Tryk med 2 SL").Position("Ved dør")
                        .CableType("3x1,5mm2 NOIKJ").CableNumber("1")
                        .DocumentationTag("test1").PowerGroup("grupp1");
                    ResourceRef trykLeft = fuga.AddInput("Tryk (venstre)", i => i.Address("_0x1").CableColour("Rød").Note("note1"));
                    ResourceRef trykRight = fuga.AddInput("Tryk (højre)", i => i.Address("_0x2").CableColour("Grå").Note("note2"));

                    ProductRef lamp = stue.AddProduct(cat.Product("_0x2202"))
                        .Name("Lampeudtag").Locked().Note("note3").Position("I loft")
                        .CableType("3G1,5mm2 PVIKJ").CableNumber("2")
                        .DocumentationTag("test2").PowerGroup("gruppe2");
                    ResourceRef lampOut = lamp.AddOutput("Udgang", o => o.Address("_0x1").Backup());
                    lamp.AddScenes();

                    FunctionBlockRef kip = stue.AddFunctionBlock(cat.FunctionBlock("1.1.01")).Locked();
                    kip.Setting("Timer", t => t.Minutes(3));
                    // Vendor wired the Kip block before starting the Entré room — allocation order 193–198
                    // (output link first, then the two input links). See tmp/experiments/out/A1/findings.md.
                    editor.Link(kip.Output("Udgang"), lampOut);   // pair ① Kip.Udgang → Lamp.Udgang (193/194)
                    editor.Link(trykLeft, kip.Input("Kip"));      // pair ② Tryk (venstre) → Kip.Kip (195/196)
                    editor.Link(trykRight, kip.Input("Sluk"));    // pair ③ Tryk (højre) → Kip.Sluk (197/198)

                    GroupRef entre = editor.Group("Entré");
                    ProductRef stik = entre.AddProduct(cat.Product("_0x2201"))
                        .Name("Stikkontakt").Locked().Note("note4")
                        .Position("ved hoveddør og udestuedør - 2 stk, fælles tilslutning")
                        .CableType("5x1,5mm2 NOIKJ").CableNumber("3")
                        .DocumentationTag("test3").PowerGroup("gruppe3");
                    ResourceRef stikOut = stik.AddOutput("Udgang", o => o.Address("_0x9").Backup());
                    stik.AddScenes();

                    FunctionBlockRef pirFb = entre.AddFunctionBlock(cat.FunctionBlock("1.4.02")).Locked();
                    pirFb.Setting("Efterløb", t => t.Minutes(3).Backup());

                    ProductRef pir = entre.AddProduct(cat.Product("_0x210e"))
                        .Name("PIR").Locked().Note("PIR").Position("I loft")
                        .CableType("3x1,5mm2 NOIKJ").CableNumber("4")
                        .DocumentationTag("test5").PowerGroup("gruppe5");
                    ResourceRef pirPresence = pir.AddInput("Tilstedeværelses indikering", i => i.Address("_0x21"));

                    // Vendor wired the PIR block last — allocation order 529–532.
                    editor.Link(pirPresence, pirFb.Input("PIR"));  // pair ④ PIR presence → pirFb.PIR (529/530)
                    editor.Link(pirFb.Output("Udgang"), stikOut);  // pair ⑤ pirFb.Udgang → Stik.Udgang (531/532)

                    clock.SetUtcNow(new DateTimeOffset(2026, 6, 27, 15, 5, 27, TimeSpan.Zero));
                    return editor.ToProject();
                },
                ProjectSaveOptions.Default);
        }

        // M1 / V2 — repeated PIR (1.4.02) insert. IHC Visual allocates the 2nd block's three enum_definitions
        // (+their values) in document order but DISCARDS them because each duplicates the 1st block's enum by NAME
        // (these user enums carry no typeid), rewiring the references to the 1st block's defs and leaving the
        // permanent 9-id hole 407–415 (R-enum; tmp/experiments/out/B3/findings.md Part A). Live-authored in IHC
        // Visual 03.04.72.03 (B3 step02-pir2). Header stamps decode via PackedStamp (Day<<24|Hour<<16|Min<<8|Sec):
        // id1 _0x3101b23 → creation 3rd 16:27:35; id2 _0x3101c22 → save 3rd 16:28:34 (modified 16:28);
        // last_unique_id _0x2da (730). Install-dir gated.
        [Test]
        public async Task EnumDedup_RepeatedPirInsert_MatchesLiveOracle_ByteIdentical()
        {
            ICatalog cat = BuildFidelity.RequireCatalog(Settings);
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 3, 16, 27, 35, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, cat, clock);

            await BuildFidelity.AssertByteIdentical(app, "LiveAuthored/step02-pir2.vis",
                build: () =>
                {
                    ProjectEditor editor = app
                        .CreateNew(new ProjectDetails("Morten Christensen", "Morten", "Danmark"))
                        .Edit();

                    editor.Group("Stue").AddFunctionBlock(cat.FunctionBlock("1.4.02"));    // 1st PIR: enums 82/85/88 hoisted fresh
                    editor.Group("Entré").AddFunctionBlock(cat.FunctionBlock("1.4.02"));   // 2nd PIR: enums dedup → hole 407–415

                    clock.SetUtcNow(new DateTimeOffset(2026, 7, 3, 16, 28, 34, TimeSpan.Zero));
                    return editor.ToProject();
                },
                ProjectSaveOptions.Default);
        }

        // M1 / V3 — "med logning" temperature-sensor products (_0x2125 ×2, _0x2139 ×1). Each product embeds a
        // "Logning" enum that dedups against the seed global "Logning" (_0x4747, ctr 71) — so it is allocated then
        // discarded, leaving one 2-id hole per insert (82/83, 94/95, 106/107) and every product resource_enum
        // pointing back at _0x4747 (R-enum; B3 findings Part B). Live-authored in IHC Visual 03.04.72.03
        // (B3 step06-luxtemp). id1 _0x3101e29 → creation 3rd 16:30:41; id2 _0x3102107 → save 3rd 16:33:07
        // (modified 16:33); last_unique_id _0x73 (115). Install-dir gated.
        [Test]
        public async Task EnumDedup_LogningProducts_MatchesLiveOracle_ByteIdentical()
        {
            ICatalog cat = BuildFidelity.RequireCatalog(Settings);
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 3, 16, 30, 41, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, cat, clock);

            await BuildFidelity.AssertByteIdentical(app, "LiveAuthored/step06-luxtemp.vis",
                build: () =>
                {
                    ProjectEditor editor = app
                        .CreateNew(new ProjectDetails("Morten Christensen", "Morten", "Danmark"))
                        .Edit();

                    editor.Group("Stue").AddProduct(cat.Product("_0x2125"));     // 24809 "Temperatur sensor med logning": hole 82/83
                    editor.Group("Entré").AddProduct(cat.Product("_0x2125"));    // 24809 again: hole 94/95
                    editor.Group("Køkken").AddProduct(cat.Product("_0x2139"));   // 24813 "Lux / Temperatur sensor med logning": hole 106/107

                    clock.SetUtcNow(new DateTimeOffset(2026, 7, 3, 16, 33, 7, TimeSpan.Zero));
                    return editor.ToProject();
                },
                ProjectSaveOptions.Default);
        }

        // M3 / V4 — project2-CustomBlock: a catalog AutoProof FB + a hand-authored "Custom blok" (full resource
        // palette across settings/internalsettings/inputs/outputs + a program with event_power and nested
        // program_sub/conditions/actions + a user enum). Built INCREMENTALLY as M3's surface lands: seed knob (3.2),
        // AutoProof name-lookup (3.3), AddEnumDefinition (3.4), ProgramBuilder (3.5), reorder/delete replay (3.6).
        // id1 _0x3071722 → creation 3rd 07:23:34; id2 _0x3072b2d → save 3rd 07:43:45 (modified 7:43);
        // last_unique_id _0xf7 (247). Install-dir gated. See tmp/e3-divergence-log.md.
        [Test]
        [Explicit("M3 in progress — scaffold replays only today's surface (CreateNew). First divergence = seed sub-order (A-1: project2 seeds modules-then-enums; our builder enums-then-modules). Steps 3.2-3.6 add the rest.")]
        public async Task BL_E3_ReproducesProject2CustomBlock_FromFluentBuilders()
        {
            ICatalog cat = BuildFidelity.RequireCatalog(Settings);
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 3, 7, 23, 34, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, cat, clock);

            await BuildFidelity.AssertByteIdentical(app, "project2-CustomBlock.vis",
                build: () =>
                {
                    ProjectEditor editor = app
                        .CreateNew(new ProjectDetails("Morten Christensen", "Morten", "Danmark"))
                        .Edit();

                    // TODO(M3.3): Group("Stue").AddFunctionBlock(AutoProof-by-name)              — segment 81–102
                    // TODO(M3.4/3.6): empty "Custom blok" + full palette (settings→internal→inputs→outputs) — 103–203
                    // TODO(M3.4): AddEnumDefinition("NyTypeForThisProject","Værdi1","Værdi2","Værdi3") — 134–137
                    // TODO(M3.5): program — event_power, nested program_sub, conditions, actions, embedded enum — 204–247
                    // TODO(M3.6): replay 7 add-then-delete burns + intra-container reorder

                    clock.SetUtcNow(new DateTimeOffset(2026, 7, 3, 7, 43, 45, TimeSpan.Zero));
                    return editor.ToProject();
                },
                ProjectSaveOptions.Default);
        }

        [Test]
        public async Task BL_E0_Harness_ReproducesProject0Tomt_ViaCreateNew()
        {
            ICatalog catalog = BuildFidelity.RequireCatalog(Settings);
            // Creation 27th 16:05:51 → id1; the default save advances 14s to 16:06:05 → id2/modified.
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 27, 16, 5, 51, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, catalog, clock);

            await BuildFidelity.AssertByteIdentical(app, "Project0-Tomt.vis",
                build: () =>
                {
                    Project project = app.CreateNew(new ProjectDetails(
                        Programmer: "Morten Christensen", InstallerName: "Morten", InstallerCountry: "Danmark"));
                    clock.SetUtcNow(new DateTimeOffset(2026, 6, 27, 16, 6, 5, TimeSpan.Zero));
                    return project;
                },
                ProjectSaveOptions.Default);
        }
    }
}
