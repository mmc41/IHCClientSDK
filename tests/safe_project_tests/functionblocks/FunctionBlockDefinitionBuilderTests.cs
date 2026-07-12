using System;
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
    /// and the end-to-end insert. (Mirrors <see cref="AuthoringApiTests"/>.)
    /// </summary>
    public class FunctionBlockDefinitionBuilderTests
    {
        private IhcSettings settings => TestSetup.Settings;

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
            FbResourceHandle kip = builder.AddInput("Kip");
            FbResourceHandle sluk = builder.AddInput("Sluk");
            FbResourceHandle udgang = builder.AddOutput("Udgang");
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

            // Documentation metadata (programmatic-lookup only — never serialized into Body or an .ifb): the block's
            // help prose plus a per-pin description. The text is synthetic/illustrative. Distinct
            // from Note(), which sets the serialized 'note' attribute.
            builder
                .Documentation("Eksempelblok: styrer en udgang ud fra sine indgange. Denne hjælpetekst er opdigtet " +
                               "og stammer ikke fra nogen leverandør.")
                .Documentation(kip, "Opdigtet hjælpetekst: denne indgang skifter udgangens tilstand i eksemplet.")
                .Documentation(sluk, "Opdigtet hjælpetekst: denne indgang nulstiller udgangen i eksemplet.")
                .Documentation(udgang, "Opdigtet hjælpetekst: eksemplets udgangssignal.");

            FunctionBlockDefinition toggleBlock = builder.Build();

            // (Never run: every builder member is a Stage-1 stub this session.)
            Assert.Multiple(() =>
            {
                Assert.That(toggleBlock.MasterType, Is.EqualTo("1.1.01"));
                Assert.That(toggleBlock.DisplayName, Is.EqualTo("1.1.01.e. Kip tænd sluk"));
                Assert.That(toggleBlock.Body.Tag, Is.EqualTo("functionblock"));
                // The help metadata is read back for programmatic lookup — off the definition, not the serialized body.
                Assert.That(toggleBlock.Documentation.Summary, Does.StartWith("Eksempelblok"));
                Assert.That(toggleBlock.Documentation.ForResource("Kip"),
                    Is.EqualTo("Opdigtet hjælpetekst: denne indgang skifter udgangens tilstand i eksemplet."));
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
            var names = inputs.ChildrenOrEmpty().Select(c => c.GetAttribute("name")).ToList();
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
    }
}
