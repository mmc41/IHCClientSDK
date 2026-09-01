namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The export byte gate for <see cref="FunctionBlockRef.ExportDefinition"/> (backlog G5, US-021 "Gem…") against
    /// the corpus's only authentic vendor-exported user block, <c>functionblocks/gemoracle-kip.ifb</c>: IHC Visual
    /// 03.04.72.03 "Gem Funktionsblok" on the placed, locked stock block "1.1.01.e. Kip tænd sluk" (<c>_0x8b28</c>)
    /// in project3's 'Stue &amp; Køkken "åben"', dialog Navn=GemOracle / Note=Oracle tooltip, exported 2026-07-11
    /// (ENG-A4). Pins the capture's design gates: project ids VERBATIM (the root stays <c>_0x8b28</c>), keyless
    /// user-block identity (<c>master_schneider_electric</c>/<c>master_type</c>/<c>master_version</c> stripped,
    /// programmer/date re-attributed, <c>locked</c> retained, icon <c>_0x10</c>), instance wiring stripped, and a
    /// DTD head declaring exactly the element types the body uses in first-occurrence order. The API is
    /// deliberately read-only — the vendor's in-document rename/re-stamp side effect is NOT mirrored (no post-Gem
    /// <c>.vis</c> oracle pins those bytes); T18 in <see cref="AuthoringByteInvarianceTests"/> pins the
    /// untouched-project half. Catalog-free (project load + registry only), so these run unconditionally.
    /// </summary>
    public class ExportDefinitionOracleTests
    {
        private const string Original = "project3-KompleksWired.vis";
        private const string GemOracle = "functionblocks/gemoracle-kip.ifb";
        private const string Room = "Stue & Køkken \"åben\"";
        private const string KipBlock = "1.1.01.e. Kip tænd sluk";
        private static IhcSettings Settings => TestSetup.Settings;

        // ---- The byte gate: the exported definition writes the vendor .ifb (whitespace-normalized) ----

        [Test]
        public async Task ExportDefinition_GemBlok_MatchesVendorIfb_TextIdentical()
        {
            byte[] expected = TestData.ReadBytes(GemOracle);

            FunctionBlockDefinition exported = ExportGemOracle((await LoadOriginal()).Edit());

            AssertEquivalent(expected, Write(exported));
        }

        // ---- Identity: keyless user block, ids verbatim, locked retained, user-library icon ----

        [Test]
        public async Task ExportDefinition_ProducesKeylessUserBlockIdentity()
        {
            FunctionBlockDefinition exported = ExportGemOracle((await LoadOriginal()).Edit());
            ProjectElement root = exported.Body;

            Assert.Multiple(() =>
            {
                Assert.That(exported.MasterType, Is.Empty, "keyless: no master_type");
                Assert.That(exported.MasterVersion, Is.Empty, "keyless: no master_version");
                Assert.That(exported.MasterName, Is.EqualTo("GemOracle"), "master_name = dialog Navn");
                Assert.That(exported.DisplayName, Is.EqualTo("GemOracle"), "bare name for a keyless block");
                Assert.That(exported.CategoryPath, Is.Empty, "a standalone export has no catalog-tree category");
                Assert.That(exported.IsEmptyTemplate, Is.False);
                Assert.That(root.GetAttribute("id"), Is.EqualTo("_0x8b28"), "the root keeps its project id verbatim");
                Assert.That(root.GetAttribute("master_schneider_electric"), Is.Null, "vendor flag stripped");
                Assert.That(root.GetAttribute("master_type"), Is.Null, "catalog key stripped");
                Assert.That(root.GetAttribute("master_version"), Is.Null, "variant letter stripped");
                Assert.That(root.GetAttribute("master_programmer"), Is.EqualTo("Morten Christensen"));
                Assert.That(root.GetAttribute("master_date_year"), Is.EqualTo("2026"));
                Assert.That(root.GetAttribute("master_date_month"), Is.EqualTo("7"), "no zero padding");
                Assert.That(root.GetAttribute("master_date_day"), Is.EqualTo("11"));
                Assert.That(root.GetAttribute("locked"), Is.EqualTo("yes"), "locked state retained");
                Assert.That(root.GetAttribute("icon"), Is.EqualTo("_0x10"), "user-library block icon");
                Assert.That(root.GetAttribute("note"), Is.EqualTo("Oracle tooltip"), "note = dialog Note");
            });
        }

        [Test]
        public async Task ExportDefinition_WithoutNote_OmitsNoteAttribute()
        {
            ProjectEditor editor = (await LoadOriginal()).Edit();

            FunctionBlockDefinition exported = editor.Group(Room).FunctionBlock(KipBlock)
                .ExportDefinition("NoNote", "Morten Christensen", new DateOnly(2026, 7, 11));

            Assert.That(exported.Body.GetAttribute("note"), Is.Null,
                "an omitted dialog Note yields the lean vendor form — no note attribute (the source note never leaks)");
        }

        // ---- Grammar: exactly the used element types, first-occurrence order, vendor prolog datum ----

        [Test]
        public async Task ExportDefinition_GrammarDeclaresUsedTypesInFirstOccurrenceOrder()
        {
            FunctionBlockDefinition exported = ExportGemOracle((await LoadOriginal()).Edit());

            List<string> usedTags = FirstOccurrenceTags(exported.Body);
            Assert.Multiple(() =>
            {
                // One declaration per element type the SOURCE uses, in first-occurrence order. Source and body agree
                // here because this block has no wiring rows to strip; where they differ the head follows the source
                // (S-22), which is what ExportDefinition_StripsSceneWiring_ButKeepsItsTraces pins.
                Assert.That(exported.Grammar.Declarations.Select(d => d.Tag), Is.EqualTo(usedTags),
                    "one declaration per used element type, in first-occurrence order");
                Assert.That(usedTags, Has.Count.EqualTo(18), "the Kip census: 18 element types");
                Assert.That(exported.Grammar.DeclaredEncoding, Is.EqualTo("ISO-8859-1"));
                Assert.That(exported.SourceEncoding, Is.EqualTo(CatalogTextEncoding.Latin1));
            });
        }

        // ---- Wiring strip: authored scene wiring never reaches the export ----

        // Re-sourced for uxparity S-22. The wiring ROWS never reaching the export is the property that matters and it
        // still holds. What does NOT hold — and was asserted here — is that a wired block therefore exports byte-for-
        // byte like a never-wired one. Measured against IHC Visual's own save-to-library: it keeps two traces of the
        // wiring it removed, so the two exports legitimately differ.
        [Test]
        public async Task ExportDefinition_StripsSceneWiring_ButKeepsItsTraces()
        {
            ProjectEditor editor = (await LoadOriginal()).Edit();
            byte[] unwired = Write(ExportGemOracle(editor));
            ResourceRef pin = editor.Group(Room).FunctionBlock(KipBlock).SceneOutput("Scenarie Sluk");
            editor.LinkScene(pin, editor.Group(Room).Product("Lampeudtag").Scenes(), SceneValue.Relay(on: true));

            FunctionBlockDefinition exported = ExportGemOracle(editor);
            string text = System.Text.Encoding.Latin1.GetString(Write(exported));

            Assert.Multiple(() =>
            {
                Assert.That(exported.Body.Descendants().Any(e => e.Tag == "scene_link"), Is.False,
                    "the scene_link row inside the pin is stripped — no catalog file carries instance wiring");
                Assert.That(exported.Grammar.Declarations.Select(d => d.Tag), Does.Contain("scene_link"),
                    "but the head still DECLARES the stripped type, as the vendor's export does");
                Assert.That(text, Does.Contain("</resource_scene>"),
                    "and the pin it emptied keeps its two-tag form instead of collapsing to '/>'");
                Assert.That(Write(exported), Is.Not.EqualTo(unwired),
                    "so a wired block does not export byte-for-byte like a never-wired one");
            });
        }

        // ---- Round-trip: the written file reads back equal and re-inserts with the source shape ----

        [Test]
        public async Task ExportedDefinition_ReadsBackAndReinsertsWithSourceShape()
        {
            Project original = await LoadOriginal();
            FunctionBlockDefinition exported = ExportGemOracle(original.Edit());
            ProjectElement source = original.Root.Descendants()
                .Single(e => e.Tag == "functionblock" && e.GetAttribute("name") == KipBlock);

            using var ms = new MemoryStream(Write(exported));
            FunctionBlockDefinition imported = CatalogReader.ReadFunctionBlock(ms);
            ProjectEditor editor = original.Edit();
            editor.Group(Room).AddFunctionBlock(imported);
            ProjectElement placed = editor.ToProject().Root.Descendants()
                .Single(e => e.Tag == "functionblock" && e.GetAttribute("name") == "GemOracle");

            Assert.Multiple(() =>
            {
                Assert.That(imported.Grammar, Is.EqualTo(exported.Grammar), "the structured grammar round-trips exactly");
                AssertSameShapeModuloIds(source, placed);
            });
        }

        // ----- helpers -----

        private static Task<Project> LoadOriginal() =>
            new ProjectAppService(Settings).Load("testdata/projects/" + Original);

        private static FunctionBlockDefinition ExportGemOracle(ProjectEditor editor) =>
            editor.Group(Room).FunctionBlock(KipBlock)
                .ExportDefinition("GemOracle", "Morten Christensen", new DateOnly(2026, 7, 11), "Oracle tooltip");

        private static byte[] Write(FunctionBlockDefinition definition)
        {
            using var ms = new MemoryStream();
            CatalogFileWriter.Write(definition, ms);
            return ms.ToArray();
        }

        private static List<string> FirstOccurrenceTags(ProjectElement root)
        {
            var seen = new HashSet<string>();
            var order = new List<string>();
            foreach (ProjectElement element in new[] { root }.Concat(root.Descendants()))
            {
                if (seen.Add(element.Tag))
                {
                    order.Add(element.Tag);
                }
            }
            return order;
        }

        private static void AssertEquivalent(byte[] expected, byte[] actual)
        {
            if (!CatalogTextCompare.Equivalent(expected, actual))
            {
                int offset = CatalogTextCompare.FirstDifference(expected, actual);
                Assert.Fail($"Exported block differs from '{GemOracle}' (whitespace-normalized) at offset {offset}.\n"
                            + $"  expected: [{CatalogTextCompare.Context(expected, offset)}]\n"
                            + $"  actual:   [{CatalogTextCompare.Context(actual, offset)}]");
            }
        }

        // Tag structure and display names must survive export → read → insert; ids are freshly allocated on insert
        // and attribute values may re-canonicalize, so neither is compared. Root names differ by design (the source
        // keeps its provenance label, the import carries the dialog Navn), so names are compared from the children down.
        private static void AssertSameShapeModuloIds(ProjectElement source, ProjectElement placed)
        {
            Assert.That(placed.Tag, Is.EqualTo(source.Tag));
            var a = source.Children;
            var b = placed.Children;
            Assert.That(b.Length, Is.EqualTo(a.Length), $"child count under <{source.Tag}>");
            for (int i = 0; i < a.Length; i++)
            {
                Assert.That(b[i].GetAttribute("name"), Is.EqualTo(a[i].GetAttribute("name")),
                    $"name of child {i} under <{source.Tag}>");
                AssertSameShapeModuloIds(a[i], b[i]);
            }
        }
    }
}
