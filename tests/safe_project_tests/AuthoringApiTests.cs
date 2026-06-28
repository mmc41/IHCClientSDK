using System;
using System.Threading.Tasks;
using Ihc.Projects;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Stage 1 authoring-API preview. Shows, in real C#, exactly how a future GUI/console caller drives
    /// the mutable edit session — both building a project from scratch (<see cref="BuildProject1_FromCode_ShowsAuthoringApi"/>)
    /// and editing a loaded one (<see cref="EditLoadedProject_AddLink_ShowsRoundTripEntry"/>). Element
    /// identity is the real <c>_0x</c> id (preserved on load, allocated on add), so loading and re-saving
    /// a project never changes existing ids. A single <c>using Ihc.Projects;</c> covers the whole authoring
    /// surface. Both tests are <c>[Explicit]</c>: the solution builds and they are present but never run (they
    /// exercise stub handles/services); their only purpose is to let the user approve the authoring surface.
    /// </summary>
    public class AuthoringApiTests
    {
        private IhcSettings settings => TestSetup.Settings;

        [Test, Explicit("Stage 1: authoring-API preview against stubs; reconstructs Project1.vis — not run")]
        public void BuildProject1_FromCode_ShowsAuthoringApi()
        {
            var cat   = CatalogDiscovery.FromInstallDir(settings.IhcVisualInstallDir);
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 27, 14, 58, 31, TimeSpan.Zero)); // creation time → id1
            var app   = new ProjectAppService(settings, cat, clock);

            // Live, mutable edit session — the same model a GUI drives. CreateNew reads the catalog's
            // File→New template, so it is an instance op using the service's injected catalog + clock.
            var editor = app
                .CreateNew(new ProjectDetails(Programmer: "Morten Christensen",
                                              InstallerName: "Morten", InstallerCountry: "Danmark"))
                .Edit();                                       // seeds the 10 default rooms (Stue, Entré, … Udendørs)

            // ===== room "Stue": 2 products + 1 catalog function block =====
            var stue = editor.Group("Stue");

            var fuga = stue.AddProduct(cat.Product("_0x2101"))      // LK FUGA Tryk 2 tast
                .Name("LK FUGA Tryk 2 tast").Locked().EnduserReport()
                .Note("Tryk med 2 SL").Position("Ved dør")
                .CableType("3x1,5mm2 NOIKJ").CableNumber("1")
                .DocumentationTag("test1").PowerGroup("grupp1");
            var trykLeft  = fuga.AddInput("Tryk (venstre)", i => i.Address("_0x1").CableColour("Rød").Note("note1"));
            var trykRight = fuga.AddInput("Tryk (højre)",   i => i.Address("_0x2").CableColour("Grø").Note("note2"));

            var lamp = stue.AddProduct(cat.Product("_0x2202"))      // Lampeudtag
                .Name("Lampeudtag").Locked().Note("note3").Position("I loft")
                .CableType("3G1,5mm2 PVIKJ").CableNumber("2")
                .DocumentationTag("test2").PowerGroup("gruppe2");
            var lampOut = lamp.AddOutput("Udgang", o => o.Address("_0x1").Backup());
            lamp.AddScenes();                                       // scenes container bound to "Udgang"

            var kip = stue.AddFunctionBlock(cat.FunctionBlock("1.1.01"))   // Kip tænd sluk (internals from catalog)
                .Locked();                                          // display name "1.1.01.e. Kip tænd sluk" arrives from the deep-copy — not re-authored
            kip.Setting("Timer", t => t.Minutes(3));                // override one catalog default

            // ===== room "Entré": 2 products + 1 catalog function block =====
            var entre = editor.Group("Entré");

            var stik = entre.AddProduct(cat.Product("_0x2201"))     // Stikkontakt
                .Name("Stikkontakt").Locked().Note("note4")
                .Position("ved hoveddør og udestuedør - 2 stk, fælles tilslutning")
                .CableType("5x1,5mm2 NOIKJ").CableNumber("3")
                .DocumentationTag("test3").PowerGroup("gruppe3");
            var stikOut = stik.AddOutput("Udgang", o => o.Address("_0x9").Backup());
            stik.AddScenes();

            var pirFb = entre.AddFunctionBlock(cat.FunctionBlock("1.4.02"))   // PIR styring (internals from catalog)
                .Locked();                                          // display name "1.4.02.a. PIR styring " arrives from the deep-copy — not re-authored
            pirFb.Setting("Efterløb", t => t.Minutes(3).Backup());

            var pir = entre.AddProduct(cat.Product("_0x210e"))      // PIR sensor
                .Name("PIR").Locked().Note("PIR").Position("I loft")
                .CableType("3x1,5mm2 NOIKJ").CableNumber("4")
                .DocumentationTag("test5").PowerGroup("gruppe5");
            var pirPresence = pir.AddInput("Tilstedeværelses indikering", i => i.Address("_0x21"));

            // remaining 8 default rooms (Køkken, Soveværelse, Værelse, Bad, Bryggers,
            // Garage, Kælder, Udendørs) stay empty — seeded by CreateNew, no code needed.

            // ===== reciprocal follow-links by live reference (one call writes both link_from + link_to) =====
            // Product resources are the handles returned above; catalog-sourced FB resources are looked up by name.
            editor.Link(trykLeft,  kip.Input("Kip"));
            editor.Link(trykRight, kip.Input("Sluk"));
            editor.Link(kip.Output("Udgang"), lampOut);
            editor.Link(pirPresence, pirFb.Input("PIR"));
            editor.Link(pirFb.Output("Udgang"), stikOut);

            var project = editor.ToProject();

            // Stage-2 assertion (commented; shown only to convey intent):
            // var bytes = ProjectSerializer.Serialize(project);
            // Assert.That(bytes, Is.EqualTo(File.ReadAllBytes("testdata/Project1.vis")));
            Assert.That(project, Is.Not.Null);
        }

        [Test, Explicit("Stage 1: authoring-API preview against stubs; load → edit → resave entry — not run")]
        public async Task EditLoadedProject_AddLink_ShowsRoundTripEntry()
        {
            var cat   = CatalogDiscovery.FromInstallDir(settings.IhcVisualInstallDir);
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 28, 10, 0, 0, TimeSpan.Zero));
            var app   = new ProjectAppService(settings, cat, clock);

            // Load an existing project, then open the same mutable edit session over it. Every existing
            // _0x id is preserved through edit + resave (identity is the id, not document position).
            var project = await app.Load("testdata/Project1.vis");
            var editor  = project.Edit();

            // Look existing rooms/products/function blocks up by name — no re-creation. AddInput/AddOutput
            // are for new children; Product/FunctionBlock + Input/Output retrieve existing ones.
            var stue = editor.Group("Stue");
            var fuga = stue.Product("LK FUGA Tryk 2 tast");          // existing product handle
            var kip  = stue.FunctionBlock("1.1.01.e. Kip tænd sluk"); // FB name is the composed provenance label (§9.1)

            // Edit an instance field and wire one new reciprocal link between existing resources.
            fuga.Note("Tryk med 2 SL (revideret)");
            editor.Link(fuga.Input("Tryk (venstre)"), kip.Input("Kip"));

            var edited = editor.ToProject();

            // Default Save re-stamps id2/modified from the clock and rewrites last_unique_id, but leaves
            // id1 and every existing element id untouched (vendor-like save).
            string outPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Project1-edited.vis");
            await app.Save(edited, outPath);
            System.IO.File.Delete(outPath);
            Assert.That(edited, Is.Not.Null);
        }
    }
}
