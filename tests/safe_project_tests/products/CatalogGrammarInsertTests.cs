#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Extensions.Time.Testing;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The insert-time semantics of the structured grammar (what captured header text used to drive):
    /// catalog-only ATTLIST defaults — orphan declarations included — materialize onto the inserted body
    /// (spec ch. 09 §9.3.7); only <b>used, non-registry</b> declarations hoist into the project DTD, rendered in
    /// the project form (synthesized <c>&lt;!ELEMENT tag ANY&gt;</c>) so the saved file reloads; schema-declared
    /// IDREFs re-stamp with their targets on insert (no dangling <c>_0x</c> tokens); and the lenient
    /// verbatim-head fallback keeps full insert semantics end-to-end while catalog re-emission stays byte-faithful.
    /// </summary>
    public class CatalogGrammarInsertTests
    {
        private static string OraclePath(string relative) => TestData.PathOf(relative);

        private static ProjectAppService App() => new(TestSetup.Settings, new BuiltInCatalog(),
            new FakeTimeProvider(new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)));

        private static Project InsertIntoFresh(ProjectAppService app, ProductDefinition product)
        {
            Project blank = app.CreateNew(new ProjectDetails("P", "I", "DK"));
            ProjectEditor editor = blank.Edit();
            editor.Group("Stue").AddProduct(product);
            return editor.ToProject();
        }

        private static Project SaveAndReload(ProjectAppService app, Project project)
        {
            using var ms = new MemoryStream();
            app.Save(project, ms, ProjectSaveOptions.PreserveExistingMetadata).GetAwaiter().GetResult();
            return app.Load(new MemoryStream(ms.ToArray())).GetAwaiter().GetResult();
        }

        private static ProjectElement Find(Project project, string tag) =>
            project.Root.Descendants().First(e => e.Tag == tag);

        // ---- catalog-only defaults drive materialization ----

        [Test]
        public void Insert_MaterializesCatalogOnlyDefaults_FromConstructedGrammar()
        {
            ProductDefinition product = CatalogReader.ReadProduct(OraclePath("products/synthetic/synthetic_9f09_logging.def"));
            ProjectAppService app = App();

            Project built = InsertIntoFresh(app, product);

            Assert.Multiple(() =>
            {
                // locked rides the CATALOG default "yes" (≠ the .vis registry default), so materialization must
                // bake it onto the committed instance — exactly what captured header text used to do.
                Assert.That(Find(built, "product_dataline").GetAttribute("locked"), Is.EqualTo("yes"),
                    "catalog grammar default materialized where the registry defaults differently");
                // The non-registry orphan's defaults live in the hoisted project block: the committed instance
                // rides them (omit-if-default), and the project schema serves them as the effective values.
                ElementSchema schema = built.SchemaView.Get("resource_sample_log");
                Assert.That(schema.FindAttr("inivalue")!.Default, Is.EqualTo("500.00"),
                    "the orphan ATTLIST inivalue default drives the effective value");
                Assert.That(schema.FindAttr("interval")!.Default, Is.EqualTo("300"));
            });
        }

        [Test]
        public void Insert_Hoists_OnlyUsedNonRegistryDeclarations()
        {
            ProjectAppService app = App();

            Project logging = InsertIntoFresh(app,
                CatalogReader.ReadProduct(OraclePath("products/synthetic/synthetic_9f09_logging.def")));
            Project superset = InsertIntoFresh(app,
                CatalogReader.ReadProduct(OraclePath("products/synthetic/synthetic_9f10_superset.def")));
            Project caseSkew = InsertIntoFresh(app,
                CatalogReader.ReadProduct(OraclePath("products/synthetic/synthetic_9f12_caseskew.def")));

            Assert.Multiple(() =>
            {
                Assert.That(logging.InlineDtdBlocks.Keys, Is.EquivalentTo(new[] { "resource_sample_log" }),
                    "used + non-registry hoists; the registry-tag orphan (resource_enum) does not");
                Assert.That(superset.InlineDtdBlocks, Is.Empty,
                    "a superset DTD hoists nothing — every used tag is registry-declared");
                Assert.That(caseSkew.InlineDtdBlocks.Keys, Is.EquivalentTo(new[] { "resource_skew" }),
                    "the unused mis-cased resource_Skew declaration does not hoist (used∩non-registry, ordinal)");
            });
        }

        [Test]
        public void OrphanDeclaredInsert_SavesAndReloads()
        {
            // The review-critical path: the hoisted block for an orphan-ATTLIST tag must carry the synthesized
            // <!ELEMENT tag ANY> line — a catalog-faithful (ATTLIST-only) block would make the saved file
            // unloadable at ProjectSchemaRegistry.ReadTag.
            ProductDefinition product = CatalogReader.ReadProduct(OraclePath("products/synthetic/synthetic_9f09_logging.def"));
            ProjectAppService app = App();

            Project built = InsertIntoFresh(app, product);
            Assert.That(built.InlineDtdBlocks["resource_sample_log"],
                Does.Contain("<!ELEMENT resource_sample_log ANY>"), "the project rendering synthesizes the ELEMENT line");

            Project reloaded = SaveAndReload(app, built);
            Assert.That(reloaded.Equals(built), Is.True, "insert → commit → serialize → reload round-trips");
        }

        // ---- IDREF re-stamping ----

        private static void AssertNoDanglingIdRefs(Project project)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProjectElement element in new[] { project.Root }.Concat(project.Root.Descendants()))
            {
                if (element.GetAttribute("id") is { } id)
                {
                    ids.Add(id);
                }
            }
            var dangling = new List<string>();
            foreach (ProjectElement element in new[] { project.Root }.Concat(project.Root.Descendants()))
            {
                ElementSchema? schema = project.SchemaView.TryGet(element.Tag);
                if (schema is null)
                {
                    continue;
                }
                foreach ((string name, string value) in element.AttrsOrEmpty())
                {
                    if (schema.IsIdRef(name) && value.Length > 0 && value != "_0x0" && !ids.Contains(value))
                    {
                        dangling.Add($"<{element.Tag} id=\"{element.GetAttribute("id")}\"> {name}=\"{value}\"");
                    }
                }
            }
            Assert.That(dangling, Is.Empty,
                "every schema-declared IDREF must follow its re-stamped target:\n" + string.Join("\n", dangling));
        }

        [Test]
        public void GrammarBuiltProduct_SceneAndEnumIdRefs_FollowReStampedIds()
        {
            // A code-authored product carrying both IDREF kinds the product side uses: the scenes binding
            // (scene_resource → output) and the resource_enum wiring (typedef/inivalue → embedded enum stub).
            ProjectElement enumDefinition = ProjectElement.Create("enum_definition", new ElementId(0x80, 0x47),
                new[] { ("typeid", "_0x16") },
                new[] { ProjectElement.Create("enum_value", new ElementId(0x81, 0x48), new[] { ("typeid", "_0x17") },
                    Array.Empty<ProjectElement>()) });

            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9fd0", "IDREF probe")
                .ExtendGrammar(g => g.AttlistOnly("resource_enum",
                    GrammarAttr.Id("id"), GrammarAttr.Cdata("name", "Enumerator"),
                    GrammarAttr.IdRef("typedef"), GrammarAttr.IdRef("inivalue")))
                .RawChild(enumDefinition)
                .AddResource("resource_enum", "Logning",
                    r => r.Attribute("typedef", "_0x8047").Attribute("inivalue", "_0x8148"))
                .AddOutput("Udgang")
                .AddScenes("Scener")   // binds scene_resource to the LAST resource — the output
                .Build();

            ProjectAppService app = App();
            Project built = InsertIntoFresh(app, product);

            AssertNoDanglingIdRefs(built);
            ProjectElement scenes = Find(built, "scenes");
            ProjectElement output = Find(built, "dataline_output");
            Assert.That(scenes.GetAttribute("scene_resource"), Is.EqualTo(output.GetAttribute("id")),
                "the scenes binding follows the output's re-stamped id");
            Assert.That(SaveAndReload(app, built).Equals(built), Is.True);
        }

        [Test]
        public void GrammarBuiltFunctionBlock_ProgramOperandIdRefs_FollowReStampedIds()
        {
            // fb08's authoring shape: links, case switch variable and embedded enum operands all re-stamp on insert.
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder.Create("9.1.09", "a", "IDREF blok")
                .MasterProgrammer("Morten Christensen")
                .MasterDate(new DateOnly(2026, 3, 3));
            FbEnumDefRef mode = builder.AddEnumDefinition("Tilstand").AddValue("Fra").AddValue("Til", 1);
            FbResourceHandle start = builder.AddInput("Start");
            FbResourceHandle output = builder.AddOutput("Udgang");
            FbResourceHandle selector = builder.AddSetting("resource_enum", "Valg", r => r.Enum(mode, "Fra"));
            FbProgramBuilder program = builder.Program("Skift");
            program.AddEvent("%P -> ON", start, "_0xa");
            FbSubProgramRef sub = program.AddSubProgram();
            sub.AddCondition("%P = OFF", output, "_0x14");
            sub.WhenTrue.AddAction("%P = ON", output, "_0xa");
            FbCaseRef sw = program.AddCase("Vælg", selector);
            sw.Case("Fra", mode, "Fra").AddAction("%P = OFF", output, "_0x14");
            sw.Default().AddAction("%P = ON", output, "_0xa");
            FunctionBlockDefinition block = builder.Build();

            ProjectAppService app = App();
            Project blank = app.CreateNew(new ProjectDetails("P", "I", "DK"));
            ProjectEditor editor = blank.Edit();
            editor.Group("Stue").AddFunctionBlock(block);
            Project built = editor.ToProject();

            AssertNoDanglingIdRefs(built);
            Assert.That(SaveAndReload(app, built).Equals(built), Is.True);
        }

        // ---- the lenient verbatim-head fallback keeps full semantics ----

        private const string FallbackUserFile =
            "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n" +
            "<!DOCTYPE product_dataline[\r\n" +
            "   <!-- exotic user header: comments are outside the structured envelope -->\r\n" +
            "   <!ELEMENT product_dataline ANY>\r\n" +
            "   <!ATTLIST product_dataline id ID #REQUIRED\r\n" +
            "                  product_identifier CDATA #REQUIRED\r\n" +
            "                  name CDATA \"\"\r\n" +
            "                  locked (yes | no) \"yes\"\r\n" +
            "                  note CDATA \"\">\r\n" +
            "   <!ELEMENT resource_widget ANY>\r\n" +
            "   <!ATTLIST resource_widget id ID #REQUIRED\r\n" +
            "                  name CDATA \"\"\r\n" +
            "                  wired IDREF #IMPLIED\r\n" +
            "                  level CDATA \"42\">\r\n" +
            "]>\r\n" +
            "<product_dataline id=\"_0x01\" product_identifier=\"_0x9fee\" name=\"Fallback probe\">\r\n" +
            "  <dataline_input id=\"_0x02\" name=\"Pin\" />\r\n" +
            "  <resource_widget id=\"_0x03\" name=\"Widget\" wired=\"_0x02\" />\r\n" +
            "</product_dataline>";

        [Test]
        public void LenientFallback_UserFile_KeepsInsertSemantics_AndByteFaithfulReEmission()
        {
            byte[] fileBytes = Encoding.Latin1.GetBytes(FallbackUserFile);
            ProductDefinition read = CatalogReader.ReadProduct(new MemoryStream(fileBytes));
            Assert.That(read.Grammar.VerbatimHead, Is.Not.Null, "the exotic header triggers the fallback");

            // From → catalog re-emission must reproduce the source bytes via the verbatim head.
            ProductDefinition rebuilt = ProductDefinitionBuilder.From(read).Build() with
            {
                Body = CatalogIds.StampDocumentOrder(
                    ProductDefinitionBuilder.From(read).Build().Body, new[] { "_0x01", "_0x02", "_0x03" }, read.Grammar),
            };
            using var ms = new MemoryStream();
            CatalogFileWriter.Write(rebuilt, ms);
            Assert.That(CatalogTextCompare.Equivalent(fileBytes, ms.ToArray()), Is.True,
                "the fallback definition re-emits its exotic header byte-faithfully");

            // Insert → the projection drives defaults materialization, hoisting AND IDREF re-stamping.
            ProjectAppService app = App();
            Project built = InsertIntoFresh(app, read);
            Assert.Multiple(() =>
            {
                Assert.That(built.InlineDtdBlocks.ContainsKey("resource_widget"), Is.True,
                    "the projected declaration hoists despite the fallback");
                Assert.That(built.SchemaView.Get("resource_widget").FindAttr("level")!.Default, Is.EqualTo("42"),
                    "the projected default drives the effective value");
                ProjectElement widget = Find(built, "resource_widget");
                ProjectElement pin = Find(built, "dataline_input");
                Assert.That(widget.GetAttribute("wired"), Is.EqualTo(pin.GetAttribute("id")),
                    "the projected IDREF re-stamps to the pin's new id");
                Assert.That(widget.GetAttribute("wired"), Is.Not.EqualTo("_0x02"), "not the placeholder token");
            });
            Assert.That(SaveAndReload(app, built).Equals(built), Is.True, "save → reload succeeds");
        }
    }
}
