using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Showcase tests for the code-authoring <see cref="ProductDefinitionBuilder"/>. Show, in real C#, how a caller
    /// re-encodes a product entirely from code — no IHC Visual install dir and no catalog — producing the same
    /// <see cref="ProductDefinition"/> a <c>Products\*.def</c> discovery yields, and how that built definition drops
    /// into the <b>existing</b> project builder (<c>GroupRef.AddProduct</c> → insert transform) unchanged. The
    /// per-oracle canonical-fidelity gate lives in <see cref="ProductBuilderOracleTests"/>; these assert the readable
    /// scalar surface (identity, documentation) and the end-to-end insert. (Mirrors <see cref="AuthoringApiExamples"/>.)
    /// </summary>
    public class ProductDefinitionBuilderTests
    {
        private static IhcSettings settings => TestSetup.Settings;

        [Test]
        public void AuthorPushButtonProduct_FromCode_ShowsProductBuilder()
        {
            // Author the product type template purely from code — the code peer of a Products\*.def descriptor.
            // No catalog, no install dir: identity + install attributes + two inputs (address / cable colour / note).
            // Inputs and outputs share one ProductResourceDefBuilder (Action-configurator), matching the function-block side.
            ProductDefinition pushButton = ProductDefinitionBuilder
                .Dataline(productIdentifier: "_0x2101", displayName: "Tryk 2 tast")
                .CategoryPath("01. Tryk/2 taster")
                .Locked().EnduserReport()
                // Documentation metadata (programmatic-lookup only — never serialized into Body or a .def): the product's
                // help prose plus a per-pin description authored ON the pin. The text is synthetic/illustrative.
                // Distinct from Note(), which sets the serialized 'note' attribute.
                .AddInput("Tryk (venstre)", i => i.Address("_0x1").CableColour("Rød").Note("Øverste tast")
                    .Documentation("Opdigtet hjælpetekst: venstre tast i eksemplet."))
                .AddInput("Tryk (højre)",   i => i.Address("_0x2").CableColour("Grå").Note("Nederste tast")
                    .Documentation("Opdigtet hjælpetekst: højre tast i eksemplet."))
                .Documentation("Eksempelprodukt: en 2-tast trykkontakt. Denne hjælpetekst er opdigtet og stammer " +
                               "ikke fra nogen leverandør.")
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
                Assert.That(pushButton.Resources.Single(r => r.Name == "Tryk (venstre)").Documentation,
                    Is.EqualTo("Opdigtet hjælpetekst: venstre tast i eksemplet."));
            });
        }

        [Test]
        public void Build_IsIdempotent_RepeatedBuildProducesIdenticalIds()
        {
            // Finding 17: Build() memoizes its root/scenes ids, so Build→preview then Build→write from one builder
            // produces byte-stable ids instead of drifting off the persistent (never-reset) allocator.
            ProductDefinitionBuilder builder = ProductDefinitionBuilder
                .Dataline(productIdentifier: "_0x2201", displayName: "Stikkontakt")
                .AddOutput("Udgang", o => o.Address("_0x1"))
                .AddScenes();

            ProductDefinition first = builder.Build();
            ProductDefinition second = builder.Build();

            Assert.Multiple(() =>
            {
                Assert.That(second.Body.Id, Is.EqualTo(first.Body.Id), "the root id is stable across Build() calls");
                Assert.That(second.Body.FindChild("scenes")!.Id, Is.EqualTo(first.Body.FindChild("scenes")!.Id),
                    "the scenes id is stable across Build() calls");
            });
        }

        [Test]
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

        [Test]
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

        // ---- documentation authored ON the resource (the configurator form) ----

        // The retired name-keyed Documentation("Tryk (højre)", …) repeated the resource name as a string key, so a
        // typo bound the text to nothing and failed silently. The configurator form spells the name once, at the add.
        // It must reach the same programmatic-lookup-only map — and, like every other documentation form, leave the
        // serialized body alone.
        [Test]
        public void DocumentationOnTheResource_SurfacesOnTheDefinition_AndLeavesBodyUntouched()
        {
            ProductDefinition documented = ProductDefinitionBuilder
                .Dataline(productIdentifier: "_0x2101", displayName: "Tryk 2 tast")
                .AddInput("Tryk (venstre)", i => i.Address("_0x1").Note("Øverste tast"))
                .AddInput("Tryk (højre)", i => i.Address("_0x2").Note("Nederste tast")
                    .Documentation("HELP-SENTINEL: sluttekontakt i tangentens højre side."))
                .Build();

            // The same product authored without the help text — the body must be indistinguishable.
            ProductDefinition bare = ProductDefinitionBuilder
                .Dataline(productIdentifier: "_0x2101", displayName: "Tryk 2 tast")
                .AddInput("Tryk (venstre)", i => i.Address("_0x1").Note("Øverste tast"))
                .AddInput("Tryk (højre)", i => i.Address("_0x2").Note("Nederste tast"))
                .Build();

            ProjectElement rightPin = documented.Body.Children.Single(c => c.GetAttribute("name") == "Tryk (højre)");
            Assert.Multiple(() =>
            {
                Assert.That(documented.Resources.Single(r => r.Name == "Tryk (højre)").Documentation,
                    Is.EqualTo("HELP-SENTINEL: sluttekontakt i tangentens højre side."));
                Assert.That(documented.Resources.Single(r => r.Name == "Tryk (venstre)").Documentation, Is.Null,
                    "an undocumented sibling stays undocumented");
                Assert.That(documented.Body, Is.EqualTo(bare.Body),
                    "help text is programmatic-lookup only — the serialized body is exactly what it was without it");
                Assert.That(rightPin.GetAttribute("note"), Is.EqualTo("Nederste tast"),
                    "the serialized note attribute is unaffected by the help text authored beside it");
            });
        }

        // A resource spliced in through the raw-subtree escape hatch has no configurator to hang help text on — the
        // one case the retired name-keyed overload still served. RawChild takes the key off the element's OWN 'name'
        // attribute, so the name is still spelled exactly once and cannot drift from the resource it documents.
        [Test]
        public void RawChildDocumentation_KeysOffTheSplicedElementsOwnName_AndLeavesBodyUntouched()
        {
            ProjectElement shutterUp = new("airlink_shutter_up", new ElementId(0x90, 0x05),
                ImmutableArray.Create(("id", "_0x9005"), ("name", "Op"), ("address_channel", "_0x01")),
                ImmutableArray<ProjectElement>.Empty);

            ProductDefinition documented = ProductDefinitionBuilder
                .Airlink(productIdentifier: "_0x4501", displayName: "Jalousi 2 tast")
                .RawChild(shutterUp, "Aktiverer stigeudgangen (persienne op).")
                .Build();

            // The same product authored without the help text — the serialized body must be indistinguishable.
            ProductDefinition bare = ProductDefinitionBuilder
                .Airlink(productIdentifier: "_0x4501", displayName: "Jalousi 2 tast")
                .RawChild(shutterUp)
                .Build();

            Assert.Multiple(() =>
            {
                Assert.That(documented.Resources.Single().Documentation,
                    Is.EqualTo("Aktiverer stigeudgangen (persienne op)."));
                Assert.That(documented.Body, Is.EqualTo(bare.Body),
                    "help text is programmatic-lookup only — the serialized body is exactly what it was without it");
            });
        }

        // ---- every resource owns its help text (US: per-resource independence) ----
        //
        // A display name does not identify a resource: the vendor catalog really does repeat one (Beolink1000 has four
        // pins called "Not in use"), and Controller Link's eight outputs even share one pinned id. Help text is
        // therefore read OFF THE RESOURCE — the projection carries it — so documenting one leaves the others alone.

        [Test]
        public void DocumentationOnSameNamedResources_StaysIndependent()
        {
            ProductDefinition product = ProductDefinitionBuilder
                .Dataline(productIdentifier: "_0x21000003", displayName: "Beolink1000")
                .AddInput("Not in use", i => i.Documentation("den første ubrugte kanal"))
                .AddInput("Not in use", i => i.Documentation("den anden ubrugte kanal"))
                .Build();

            Assert.Multiple(() =>
            {
                Assert.That(product.Resources[0].Documentation, Is.EqualTo("den første ubrugte kanal"));
                Assert.That(product.Resources[1].Documentation, Is.EqualTo("den anden ubrugte kanal"));
            });
        }

        [Test]
        public void RawChildDocumentation_OnSameNamedSplicedResources_StaysIndependent()
        {
            // Controller Link OUT: every output carries the SAME pinned id _0x02, so neither name nor id separates
            // these two — only their position in the body does.
            ProjectElement first = new("dataline_output", new ElementId(0x02, 0x05),
                ImmutableArray.Create(("id", "_0x02"), ("name", "Link")), ImmutableArray<ProjectElement>.Empty);

            ProductDefinition product = ProductDefinitionBuilder
                .Dataline(productIdentifier: "_0x2704", displayName: "Controller Link OUT")
                .RawChild(first, "kanal 1 til den anden controller")
                .RawChild(first, "kanal 2 til den anden controller")
                .Build();

            Assert.Multiple(() =>
            {
                Assert.That(product.Resources[0].Documentation, Is.EqualTo("kanal 1 til den anden controller"));
                Assert.That(product.Resources[1].Documentation, Is.EqualTo("kanal 2 til den anden controller"));
            });
        }

        // A product body interleaves resources with structural children (scenes, the settings containers), and only
        // the resources are projected. The help text must follow the RESOURCE across that filtering, not slide onto a
        // neighbour because a structural child sits between them.
        [Test]
        public void DocumentationSurvives_AStructuralChildBetweenTwoResources()
        {
            ProductDefinition product = ProductDefinitionBuilder
                .Airlink(productIdentifier: "_0x4410", displayName: "Lysdæmper")
                .AddInput("Tænd", i => i.Documentation("tænder dæmperen"))
                .RawChild(ElementWithId("dimmer_settings", 0x11))
                .AddInput("Sluk", i => i.Documentation("slukker dæmperen"))
                .Build();

            Assert.Multiple(() =>
            {
                Assert.That(product.Resources.Select(r => r.Name), Is.EqualTo(new[] { "Tænd", "Sluk" }),
                    "the structural child is not a resource");
                Assert.That(product.Resources[0].Documentation, Is.EqualTo("tænder dæmperen"));
                Assert.That(product.Resources[1].Documentation, Is.EqualTo("slukker dæmperen"));
            });
        }

        private static ProjectElement ElementWithId(string tag, int counter) =>
            new(tag, new ElementId(counter, 0x05),
                ImmutableArray.Create(("id", $"_0x{counter:x2}")), ImmutableArray<ProjectElement>.Empty);
    }
}
