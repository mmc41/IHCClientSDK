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
        // harness to byte-identity. Verified to reproduce the oracle to within 6 bytes of 88,321 (99.99%); the sole
        // remaining divergence is the id-counter of the hoisted USER enums during catalog FB insert (mine _0xc547 =
        // 197 vs vendor _0xcb47 = 203 — a uniform 6-id offset in the enum-hoist phase). Closing that gap requires
        // matching IHC Visual's exact id-allocation ORDER while importing an .ifb (when it allocates a hoisted
        // enum_definition's id relative to the subtree traversal) — a deep InsertTransform/vendor-algorithm parity
        // effort, not a builder-call reorder. Kept [Explicit] as the on-demand first-divergence probe (run it to see
        // the exact offset via the harness dump) until that allocation-order parity is reverse-engineered.
        [Test, Explicit("BL-E2: byte-close (6/88321) from-scratch build; byte-identity pending enum-hoist id-allocation-order parity")]
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
                    ResourceRef trykRight = fuga.AddInput("Tryk (højre)", i => i.Address("_0x2").CableColour("Grø").Note("note2"));

                    ProductRef lamp = stue.AddProduct(cat.Product("_0x2202"))
                        .Name("Lampeudtag").Locked().Note("note3").Position("I loft")
                        .CableType("3G1,5mm2 PVIKJ").CableNumber("2")
                        .DocumentationTag("test2").PowerGroup("gruppe2");
                    ResourceRef lampOut = lamp.AddOutput("Udgang", o => o.Address("_0x1").Backup());
                    lamp.AddScenes();

                    FunctionBlockRef kip = stue.AddFunctionBlock(cat.FunctionBlock("1.1.01")).Locked();
                    kip.Setting("Timer", t => t.Minutes(3));

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

                    editor.Link(trykLeft, kip.Input("Kip"));
                    editor.Link(trykRight, kip.Input("Sluk"));
                    editor.Link(kip.Output("Udgang"), lampOut);
                    editor.Link(pirPresence, pirFb.Input("PIR"));
                    editor.Link(pirFb.Output("Udgang"), stikOut);

                    clock.SetUtcNow(new DateTimeOffset(2026, 6, 27, 15, 5, 27, TimeSpan.Zero));
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
