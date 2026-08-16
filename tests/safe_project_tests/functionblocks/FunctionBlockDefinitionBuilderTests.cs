using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Showcase tests for the code-authoring <see cref="FunctionBlockDefinitionBuilder"/>. Show, in real C#, how a
    /// caller authors a function block from code — master identity, the four resource containers plus the program graph
    /// with <c>link1</c>/<c>link2</c> wiring by handle — producing the same <see cref="FunctionBlockDefinition"/>
    /// a <c>FunctionBlocks\*.ifb</c> discovery yields, and how it drops into the <b>existing</b> project builder
    /// (<c>GroupRef.AddFunctionBlock</c> → insert transform) unchanged. The per-oracle canonical-fidelity gate lives in
    /// <see cref="FunctionBlockBuilderOracleTests"/>; these assert the readable scalar surface (identity, documentation)
    /// and the end-to-end insert. (Mirrors <see cref="AuthoringApiExamples"/>.)
    /// </summary>
    public class FunctionBlockDefinitionBuilderTests
    {
        private IhcSettings settings => TestSetup.Settings;

        // review F1: From(existing).Build() must CARRY ExplicitCloseIds (the save-to-library two-tag close set),
        // not silently reset it to Empty — which would re-emit self-closing pins where the vendor keeps the two-tag
        // form. Seed a real catalog block's copy with a close set and confirm the round-trip preserves it.
        [Test]
        public void From_Build_PreservesExplicitCloseIds()
        {
            FunctionBlockDefinition original = new BuiltInCatalog().FunctionBlocks.First();
            ImmutableHashSet<ElementId> closeIds = ImmutableHashSet.Create(new ElementId(0x60, 0x12), new ElementId(0x61, 0x12));
            FunctionBlockDefinition withCloseIds = original with { ExplicitCloseIds = closeIds };

            FunctionBlockDefinition rebuilt = FunctionBlockDefinitionBuilder.From(withCloseIds).Build();

            Assert.That(rebuilt.ExplicitCloseIds, Is.EqualTo(closeIds));
        }

        [Test]
        public void AuthorToggleBlock_FromCode_ShowsFunctionBlockBuilder()
        {
            // Master identity — the code peer of a FunctionBlocks\*.ifb header. DisplayName defaults to the
            // composed "1.1.01.e. Kip tænd sluk".
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create(masterType: "1.1.01", masterVersion: "e", masterName: "Kip tænd sluk")
                .CategoryPath("00. Foretrukne")
                .MasterProgrammer("Vendor")
                .MasterDate(new DateOnly(2009, 1, 1))
                .VendorMaster();

            // Resources land in their fixed containers; each add returns a handle used to wire the program graph.
            // Per-pin documentation metadata (programmatic-lookup only — never serialized into Body or an .ifb) is
            // authored on the resource itself, so the pin name is spelled once. The text is synthetic/illustrative.
            // Distinct from Note(), which sets the serialized 'note' attribute.
            FbResourceHandle kip = builder.AddInput("Kip",
                r => r.Documentation("Opdigtet hjælpetekst: denne indgang skifter udgangens tilstand i eksemplet."));
            FbResourceHandle sluk = builder.AddInput("Sluk",
                r => r.Documentation("Opdigtet hjælpetekst: denne indgang nulstiller udgangen i eksemplet."));
            FbResourceHandle udgang = builder.AddOutput("Udgang",
                r => r.Documentation("Opdigtet hjælpetekst: eksemplets udgangssignal."));
            FbResourceHandle timer = builder.AddSetting("resource_timer", "Timer", r => r.TimerHms(0, 3, 0));

            // Program graph: two triggers, then a sub-program with one condition and true/false action branches.
            // link1/link2 are wired by handle; the method tokens are the opaque per-block operation vocabulary.
            FbProgramBuilder program = builder.Program();
            program.AddEvent("Kip", kip, method: "_0xa");
            program.AddEvent("Sluk", sluk, method: "_0xa");

            FbSubProgramRef sub = program.AddSubProgram();
            sub.AddCondition("Er tændt", udgang, method: "_0xbe");
            sub.WhenTrue.AddAction("Sluk lys", udgang, method: "_0xdc");
            sub.WhenFalse.AddAction("Tænd lys", udgang, method: "_0xda", link2: timer);

            // The block-level help prose (the same programmatic-lookup-only metadata, one level up).
            builder.Documentation("Eksempelblok: styrer en udgang ud fra sine indgange. Denne hjælpetekst er opdigtet " +
                                  "og stammer ikke fra nogen leverandør.");

            FunctionBlockDefinition toggleBlock = builder.Build();

            // (Never run: every builder member is a Stage-1 stub this session.)
            Assert.Multiple(() =>
            {
                Assert.That(toggleBlock.MasterType, Is.EqualTo("1.1.01"));
                Assert.That(toggleBlock.DisplayName, Is.EqualTo("1.1.01.e. Kip tænd sluk"));
                Assert.That(toggleBlock.Body.Tag, Is.EqualTo("functionblock"));
                // The help metadata is read back for programmatic lookup — off the definition, not the serialized body.
                Assert.That(toggleBlock.Documentation.Summary, Does.StartWith("Eksempelblok"));
                Assert.That(toggleBlock.Inputs.Single(r => r.Name == "Kip").Documentation,
                    Is.EqualTo("Opdigtet hjælpetekst: denne indgang skifter udgangens tilstand i eksemplet."));
            });
        }

        // T027: Build() must be idempotent — a second call yields a structurally identical Body (matching
        // ProductDefinitionBuilder). The old builder re-materialized every call, drifting the placeholder ids of the
        // 5 containers, the functionblock root and the whole program graph off the shared allocator.
        [Test]
        public void Build_CalledTwice_YieldsStructurallyIdenticalBodies()
        {
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("1.1.01", "e", "Kip tænd sluk")
                .VendorMaster();
            FbResourceHandle kip = builder.AddInput("Kip");
            FbResourceHandle udgang = builder.AddOutput("Udgang");
            FbProgramBuilder program = builder.Program();
            program.AddEvent("Kip", kip, method: "_0xa");
            FbSubProgramRef sub = program.AddSubProgram();
            sub.WhenTrue.AddAction("Sluk lys", udgang, method: "_0xdc");

            FunctionBlockDefinition first = builder.Build();
            FunctionBlockDefinition second = builder.Build();

            Assert.That(second.Body, Is.EqualTo(first.Body),
                "a second Build() must reproduce the same placeholder ids (no drift off the shared allocator)");
        }

        // T028: the empty-template path honors per-block container-name/note overrides AND authored root attributes
        // uniformly (as the normal MaterializeBody path does), instead of silently dropping the name overrides,
        // 3-of-5 note overrides, and every root attribute. (The master_* identity stays omitted — the fb.def scaffold
        // carries none — so byte-fidelity of an un-customized empty template is unchanged; covered by fb05 oracle.)
        [Test]
        public void AsEmptyTemplate_HonorsContainerOverridesAndRootAttributes()
        {
            FunctionBlockDefinition block = FunctionBlockDefinitionBuilder
                .Create("1.1.01", "e", "Kip")
                .InputsName("Mine indgange")
                .SettingsNote("Egen note")
                .Note("Blok-note")
                .Locked()
                .AsEmptyTemplate()
                .Build();

            ProjectElement inputs = block.Body.FindChild("inputs")!;
            ProjectElement settings = block.Body.FindChild("settings")!;
            Assert.Multiple(() =>
            {
                Assert.That(inputs.GetAttribute("name"), Is.EqualTo("Mine indgange"), "container-name override honored");
                Assert.That(settings.GetAttribute("note"), Is.EqualTo("Egen note"), "settings-container-note override honored");
                Assert.That(block.Body.GetAttribute("note"), Is.EqualTo("Blok-note"), "root attribute honored");
                Assert.That(block.Body.GetAttribute("locked"), Is.EqualTo("yes"), "root attribute honored");
                Assert.That(block.Body.GetAttribute("master_name"), Is.Null, "the empty template still omits master identity");
                Assert.That(block.IsEmptyTemplate, Is.True);
            });
        }

        [Test]
        public async Task InsertAuthoredBlock_IntoLoadedProject_ShowsItWorksWithProjectBuilder()
        {
            FunctionBlockDefinition toggle = FunctionBlockDefinitionBuilder
                .Create("1.1.01", "e", "Kip tænd sluk")
                .VendorMaster()
                .Build();

            // Load needs no catalog/install dir; the built definition is inserted through the existing path.
            var app = new ProjectAppService(settings);
            Project project = await app.Load("testdata/projects/Project0-Tomt.vis");

            ProjectEditor editor = project.Edit();
            editor.Group("Stue").AddFunctionBlock(toggle).Locked();

            Project built = editor.ToProject();
            Assert.That(built, Is.Not.Null);
        }

        [Test]
        public void From_ThenAddInput_SplicesTheEditIntoTheBuiltBody()
        {
            // Finding 1: a From()-seeded builder must MERGE post-From edits into the body (as
            // ProductDefinitionBuilder.From does), not silently discard them — the old MaterializeDecoded re-emitted
            // only the original decoded body, so AddInput/AddOutput/Program/RawChild were dropped from Build().
            FunctionBlockDefinitionBuilder source = FunctionBlockDefinitionBuilder
                .Create("1.1.01", "e", "Kip tænd sluk")
                .VendorMaster();
            source.AddInput("Kip");
            FunctionBlockDefinition original = source.Build();

            FunctionBlockDefinitionBuilder reopened = FunctionBlockDefinitionBuilder.From(original);
            reopened.AddInput("Ny pin");
            FunctionBlockDefinition edited = reopened.Build();

            ProjectElement inputs = edited.Body.FindChild("inputs")!;
            var names = inputs.Children.Select(c => c.GetAttribute("name")).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(names, Does.Contain("Kip"), "the original input is preserved");
                Assert.That(names, Does.Contain("Ny pin"), "the post-From authored input is spliced in, not dropped");
            });
        }

        [Test]
        public void AuthorEnumBlock_TypedEnum_AndReopen_ShowSurfaceAdditions()
        {
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("1.1.20", "a", "Tilstandsstyring")
                .VendorMaster();

            // Typed enum authoring: define an embedded enum, add values, then wire settings/operands by handle +
            // human value name — no raw typedef/inivalue tokens on the caller.
            FbEnumDefRef mode = builder.AddEnumDefinition("Tilstand").AddValue("Nat").AddValue("Dag");
            FbResourceHandle setting = builder.AddSetting("resource_enum", "Tilstand", r => r.Enum(mode, "Nat"));
            FbResourceHandle output = builder.AddOutput("Udgang");

            FbProgramBuilder program = builder.Program();
            FbSubProgramRef sub = program.AddSubProgram();
            FbConditionRef cond = sub.AddCondition("Er nat", setting, method: "_0xbe");
            cond.AddEnumOperand("Nat", mode, "Nat");                 // typed enum operand (by handle + value name)
            sub.WhenTrue.AddAction("Tænd", output, method: "_0xda");

            ProjectValidationResult check = builder.Validate();      // non-throwing structured validation
            FunctionBlockDefinition block = builder.Build();
            FunctionBlockDefinitionBuilder reopened = FunctionBlockDefinitionBuilder.From(block);   // edit-existing gesture

            Assert.Multiple(() =>
            {
                Assert.That(check.IsValid, Is.True);
                Assert.That(reopened, Is.Not.Null);
            });
        }

        // ---- documentation authored ON the resource (the configurator form) ----

        // The retired name-keyed Documentation("Kip", …) repeated the resource name as a string key, so a typo bound
        // the text to nothing and failed silently. The configurator form spells the name once, at the add — and must
        // work for all four containers, since a block documents settings and internal variables too.
        [Test]
        public void DocumentationOnTheResource_CoversAllFourContainers_AndLeavesBodyUntouched()
        {
            FunctionBlockDefinitionBuilder documented = FunctionBlockDefinitionBuilder
                .Create("1.1.01", "e", "Kip tænd sluk").VendorMaster();
            documented.AddInput("resource_input", "Kip", r => r.Icon("_0x36").Note("Tænd/sluk.")
                .Documentation("skifter udgangen til modsat tilstand"));
            documented.AddOutput("resource_output", "Udgang", r => r.Documentation("udgangens aktuelle tilstand"));
            documented.AddSetting("resource_timer", "Timer", r => r.TimerHms(0, 3, 0).Documentation("timerens hviletid"));
            documented.AddInternalVariable("resource_flag", "Intern", r => r.Documentation("blokkens private flag"));

            // The same block authored without the help text — the body must be indistinguishable.
            FunctionBlockDefinitionBuilder bare = FunctionBlockDefinitionBuilder
                .Create("1.1.01", "e", "Kip tænd sluk").VendorMaster();
            bare.AddInput("resource_input", "Kip", r => r.Icon("_0x36").Note("Tænd/sluk."));
            bare.AddOutput("resource_output", "Udgang");
            bare.AddSetting("resource_timer", "Timer", r => r.TimerHms(0, 3, 0));
            bare.AddInternalVariable("resource_flag", "Intern");

            FunctionBlockDefinition block = documented.Build();
            Assert.Multiple(() =>
            {
                Assert.That(block.Inputs.Single().Documentation, Is.EqualTo("skifter udgangen til modsat tilstand"));
                Assert.That(block.Outputs.Single().Documentation, Is.EqualTo("udgangens aktuelle tilstand"));
                Assert.That(block.Settings.Single().Documentation, Is.EqualTo("timerens hviletid"));
                Assert.That(block.InternalVariables.Single().Documentation, Is.EqualTo("blokkens private flag"));
                Assert.That(block.Body, Is.EqualTo(bare.Build().Body),
                    "help text is programmatic-lookup only — the serialized body is exactly what it was without it");
            });
        }

        // The tag-free short form is the one a hand author reaches for first; it takes a configurator too, so
        // documenting a default-typed pin never forces the caller to spell "resource_input".
        [Test]
        public void ShortFormAddInputAndAddOutput_TakeAConfigurator_SoDocumentationNeedsNoTag()
        {
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("1.1.01", "e", "Kip tænd sluk").VendorMaster();
            FbResourceHandle kip = builder.AddInput("Kip", r => r.Documentation("skifter udgangen"));
            FbResourceHandle udgang = builder.AddOutput("Udgang", r => r.Documentation("udgangens tilstand"));

            FunctionBlockDefinition block = builder.Build();
            Assert.Multiple(() =>
            {
                Assert.That(block.Inputs.Single(r => r.Name == kip.Name).Documentation, Is.EqualTo("skifter udgangen"));
                Assert.That(block.Outputs.Single(r => r.Name == udgang.Name).Documentation, Is.EqualTo("udgangens tilstand"));
                Assert.That(block.Body.FindChild("inputs")!.Children.Single().Tag, Is.EqualTo("resource_input"),
                    "the short form still emits the default pin type");
            });
        }

        // ---- every pin owns its help text (US: per-resource independence) ----
        //
        // A display name does not identify a pin: the vendor catalog really does name an input and an output the same
        // thing (block 1.4.03 has both a "Sluk" input and a "Sluk" scene output), and they are different things that
        // deserve different descriptions. Help text is therefore read OFF THE PIN — the projection carries it — and
        // documenting one pin must leave every other pin alone, whatever it is called.

        [Test]
        public void DocumentationOnSameNamedPins_InDifferentContainers_StaysIndependent()
        {
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("1.4.03", "a", "Udendørslys").VendorMaster();
            builder.AddInput("Sluk", r => r.Documentation("indgangen: slukker udgangen"));
            builder.AddOutput("resource_scene", "Sluk", r => r.Documentation("udgangen: scenariet der hentes ved sluk"));

            FunctionBlockDefinition block = builder.Build();

            Assert.Multiple(() =>
            {
                Assert.That(block.Inputs.Single().Documentation, Is.EqualTo("indgangen: slukker udgangen"));
                Assert.That(block.Outputs.Single().Documentation,
                    Is.EqualTo("udgangen: scenariet der hentes ved sluk"),
                    "the output's text is its own — the same-named input must not overwrite it");
            });
        }

        [Test]
        public void DocumentationOnSameNamedPins_InTheSameContainer_StaysIndependent()
        {
            // Block 8.1.01 really carries two outputs both named "Sidst genstartet"; the name cannot separate them.
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("8.1.01", "", "Controller").VendorMaster();
            builder.AddOutput("Sidst genstartet", r => r.Documentation("den første: tidspunktet"));
            builder.AddOutput("Sidst genstartet", r => r.Documentation("den anden: årsagen"));

            FunctionBlockDefinition block = builder.Build();

            Assert.Multiple(() =>
            {
                Assert.That(block.Outputs[0].Documentation, Is.EqualTo("den første: tidspunktet"));
                Assert.That(block.Outputs[1].Documentation, Is.EqualTo("den anden: årsagen"));
            });
        }

        [Test]
        public void DocumentationOnEveryPin_IsIndependentAcrossAllFourContainers()
        {
            // One name, four containers: whichever pin is documented last must not claim the other three's text.
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("1.1.01", "e", "Kip").VendorMaster();
            builder.AddInput("Fælles", r => r.Documentation("indgangens tekst"));
            builder.AddOutput("Fælles", r => r.Documentation("udgangens tekst"));
            builder.AddSetting("resource_timer", "Fælles", r => r.TimerHms(0, 1, 0).Documentation("indstillingens tekst"));
            builder.AddInternalVariable("resource_flag", "Fælles", r => r.Documentation("den private variabels tekst"));

            FunctionBlockDefinition block = builder.Build();

            Assert.Multiple(() =>
            {
                Assert.That(block.Inputs.Single().Documentation, Is.EqualTo("indgangens tekst"));
                Assert.That(block.Outputs.Single().Documentation, Is.EqualTo("udgangens tekst"));
                Assert.That(block.Settings.Single().Documentation, Is.EqualTo("indstillingens tekst"));
                Assert.That(block.InternalVariables.Single().Documentation, Is.EqualTo("den private variabels tekst"));
            });
        }

        // A pin added to a From()-reopened block lands at the END of the decoded container. Its help text must attach
        // to that pin — not to whichever pin already sat at the position the fresh builder would have used.
        [Test]
        public void DocumentationOnAPinAddedAfterFrom_LeavesTheSeededPinsTextIntact()
        {
            FunctionBlockDefinitionBuilder original = FunctionBlockDefinitionBuilder
                .Create("1.1.01", "e", "Kip").VendorMaster();
            original.AddInput("Kip", r => r.Documentation("den oprindelige indgangs tekst"));
            original.AddOutput("Udgang", r => r.Documentation("den oprindelige udgangs tekst"));

            FunctionBlockDefinitionBuilder reopened = FunctionBlockDefinitionBuilder.From(original.Build());
            reopened.AddInput("Ny indgang", r => r.Documentation("den tilføjede indgangs tekst"));

            FunctionBlockDefinition block = reopened.Build();

            Assert.Multiple(() =>
            {
                Assert.That(block.Inputs.Select(r => r.Name), Is.EqualTo(new[] { "Kip", "Ny indgang" }));
                Assert.That(block.Inputs[0].Documentation, Is.EqualTo("den oprindelige indgangs tekst"),
                    "the seeded pin keeps its own text");
                Assert.That(block.Inputs[1].Documentation, Is.EqualTo("den tilføjede indgangs tekst"));
                Assert.That(block.Outputs.Single().Documentation, Is.EqualTo("den oprindelige udgangs tekst"));
            });
        }
    }
}
