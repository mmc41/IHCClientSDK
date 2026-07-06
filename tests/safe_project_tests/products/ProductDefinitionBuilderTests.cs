using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Stage-1 preview of the code-authoring <see cref="ProductDefinitionBuilder"/>. Shows, in real C#, how a caller
    /// re-encodes a product entirely from code — no IHC Visual install dir and no catalog — producing the same
    /// <see cref="ProductDefinition"/> a <c>Products\*.def</c> discovery yields, and how that built definition drops
    /// into the <b>existing</b> project builder (<c>GroupRef.AddProduct</c> → insert transform) unchanged. These tests
    /// are <c>[Explicit]</c>: the solution builds and they are present but never run (they exercise the stub builder);
    /// their only purpose is to let the user approve the authoring surface. (Mirrors <see cref="AuthoringApiTests"/>.)
    /// </summary>
    public class ProductDefinitionBuilderTests
    {
        private IhcSettings settings => TestSetup.Settings;

        [Test, Explicit("Stage 1: builder-API preview against stubs; authors a product from code — not run")]
        public void AuthorPushButtonProduct_FromCode_ShowsProductBuilder()
        {
            // Author the product type template purely from code — the code peer of a Products\*.def descriptor.
            // No catalog, no install dir: identity + install attributes + two inputs (address / cable colour / note).
            // Inputs and outputs share one ProductResourceDefBuilder (Action-configurator), matching the function-block side.
            ProductDefinition pushButton = ProductDefinitionBuilder
                .Dataline(productIdentifier: "_0x2101", displayName: "Tryk 2 tast")
                .CategoryPath("01. Tryk/2 taster")
                .Locked().EnduserReport()
                .AddInput("Tryk (venstre)", i => i.Address("_0x1").CableColour("Rød").Note("Øverste tast"))
                .AddInput("Tryk (højre)",   i => i.Address("_0x2").CableColour("Grå").Note("Nederste tast"))
                // Documentation metadata (programmatic-lookup only — never serialized into Body or a .def): the product's
                // help prose plus a per-pin description keyed by resource name. The text is synthetic/illustrative.
                // Distinct from Note(), which sets the serialized 'note' attribute.
                .Documentation("Eksempelprodukt: en 2-tast trykkontakt. Denne hjælpetekst er opdigtet og stammer " +
                               "ikke fra nogen leverandør.")
                .Documentation("Tryk (venstre)", "Opdigtet hjælpetekst: venstre tast i eksemplet.")
                .Documentation("Tryk (højre)",   "Opdigtet hjælpetekst: højre tast i eksemplet.")
                .Build();

            // The build yields exactly the ProductDefinition a .def discovery would — the same record the insert
            // transform deep-copies. (Never run: every builder member is a Stage-1 stub this session.)
            Assert.Multiple(() =>
            {
                Assert.That(pushButton.ProductIdentifier, Is.EqualTo("_0x2101"));
                Assert.That(pushButton.DisplayName, Is.EqualTo("Tryk 2 tast"));
                Assert.That(pushButton.Body.Tag, Is.EqualTo("product_dataline"));
                // The help metadata is read back for programmatic lookup — off the definition, not the serialized body.
                Assert.That(pushButton.Documentation.Summary, Does.StartWith("Eksempelprodukt"));
                Assert.That(pushButton.Documentation.ForResource("Tryk (venstre)"),
                    Is.EqualTo("Opdigtet hjælpetekst: venstre tast i eksemplet."));
            });
        }

        [Test, Explicit("Stage 1: builder-API preview against stubs; inserts a code-authored product — not run")]
        public async Task InsertAuthoredProduct_IntoLoadedProject_ShowsItWorksWithProjectBuilder()
        {
            // An output-bearing product with a scenes container (vendor default label via DefaultScenesName),
            // again authored entirely from code.
            ProductDefinition socket = ProductDefinitionBuilder
                .Dataline(productIdentifier: "_0x2201", displayName: "Stikkontakt")
                .Locked()
                .AddOutput("Udgang", o => o.Address("_0x1").Backup())
                .AddScenes()
                .Build();

            // Load needs no catalog/install dir (the service's catalog is lazy and never forced by Load), so this is a
            // standalone insert. When BuiltInCatalog lands, CreateNew becomes available with no install dir either.
            var app = new ProjectAppService(settings);
            Project project = await app.Load("testdata/projects/Project0-Tomt.vis");

            // A code-authored definition is a first-class ProductDefinition, indistinguishable from a catalog-discovered
            // one: it flows through the existing GroupRef.AddProduct path with no adapter — the whole point of
            // "works with the existing project builder".
            ProjectEditor editor = project.Edit();
            editor.Group("Stue").AddProduct(socket).Name("Stikkontakt").Locked();

            Project built = editor.ToProject();
            Assert.That(built, Is.Not.Null);
        }

        [Test, Explicit("Stage 1: builder-API preview against stubs; exercises the review-driven surface additions — not run")]
        public void EditExistingType_NamedFamily_AndValidate_ShowSurfaceAdditions()
        {
            // Named factory for a non-dataline family (was reachable only via a magic-string Create before).
            ProjectValidationResult check = ProductDefinitionBuilder
                .Airlink(productIdentifier: "_0x5401", displayName: "Airlink relæ")
                .AddResource("airlink_relay", "Relæ", r => r.Icon("_0x1"))
                .Validate();   // non-throwing, structured — for live GUI field validation

            // Open an existing/discovered definition and edit it (the "edit-existing type" gesture a library editor needs).
            ProductDefinition original = ProductDefinitionBuilder.Dataline("_0x2101", "Tryk").Build();
            ProductDefinition edited = ProductDefinitionBuilder.From(original).Note("Edited from GUI").Build();

            Assert.Multiple(() =>
            {
                Assert.That(check.IsValid, Is.True);
                Assert.That(edited.DisplayName, Is.EqualTo("Tryk"));
            });
        }
    }
}
