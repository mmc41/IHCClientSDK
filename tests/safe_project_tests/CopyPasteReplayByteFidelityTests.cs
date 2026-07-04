using System.Globalization;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The copy/paste byte-fidelity gate for <see cref="ProjectEditor.CopySubtree"/> against the authentic vendor
    /// oracle <c>project3-KompleksWired-copied.vis</c> (IHC Visual 03.04.72.03 after three clipboard copy→paste
    /// actions on <c>project3-KompleksWired.vis</c>, single save). The SDK loads the original, reproduces the
    /// vendor's one-time load-time enum re-hoist (<see cref="ProjectEditor.NormalizeCatalogEnums"/>), replays the
    /// three copies through <see cref="ProjectEditor.CopySubtree"/> in allocation order, restamps to the oracle's
    /// clock and asserts byte-identity. Catalog-free (the copy path needs no install dir), so these run
    /// unconditionally. Two isolation tests pin the two behaviours the full replay depends on: Action 0 (enum
    /// re-hoist) and copy C's enum-footprint id burn.
    /// </summary>
    public class CopyPasteReplayByteFidelityTests
    {
        private const string Original = "project3-KompleksWired.vis";
        private const string CopyOracle = "project3-KompleksWired-copied.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        // ---- Action 0: the load-time re-hoist of the built-in catalog enums ----

        [Test]
        public async Task NormalizeCatalogEnums_ReHoistsBuiltInCatalogEnums_ToBottomWithFreshIds()
        {
            Project project = await new ProjectAppService(Settings).Load("testdata/" + Original);

            Project after = project.Edit().NormalizeCatalogEnums().ToProject();

            ProjectElement enums = after.Child("enum_definitions")!;
            var defs = enums.ChildrenOrEmpty().Where(e => e.Tag == "enum_definition").ToList();
            ProjectElement persienne = defs[^2];   // the two typeid enums are now the last two, renumbered
            ProjectElement logning = defs[^1];
            // project3 has 60 resource_enum rows in all; only the 4 that referenced Logning are re-pointed.
            var logningRefs = after.Root.Descendants()
                .Where(e => e.Tag == "resource_enum" && e.GetAttribute("typedef") == "_0x57347").ToList();
            var staleRefs = after.Root.Descendants()
                .Where(e => e.Tag == "resource_enum"
                    && (e.GetAttribute("typedef") == "_0x4747" || e.GetAttribute("inivalue") == "_0x4848")).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(persienne.GetAttribute("name"), Is.EqualTo("Persienne tilstand"));
                Assert.That(persienne.GetAttribute("id"), Is.EqualTo("_0x56d47"));
                Assert.That(persienne.GetAttribute("typeid"), Is.EqualTo("_0x10"), "typeid preserved");
                Assert.That(logning.GetAttribute("name"), Is.EqualTo("Logning"));
                Assert.That(logning.GetAttribute("id"), Is.EqualTo("_0x57347"));
                Assert.That(logning.ChildrenOrEmpty().First().GetAttribute("id"), Is.EqualTo("_0x57448"),
                    "first Logning value renumbered");
                Assert.That(after.LastUniqueId, Is.EqualTo("_0x579"), "13 ids consumed (2 defs + 11 values)");
                Assert.That(logningRefs, Has.Count.EqualTo(4), "the 4 Logning refs repoint at the re-hoisted def");
                Assert.That(logningRefs.All(r => r.GetAttribute("inivalue") == "_0x57448"), Is.True);
                Assert.That(staleRefs, Is.Empty, "no ref left pointing at the old Logning ids");
            });
        }

        // ---- Copy C: the enum-footprint id burn (falsification gate for the burn mechanism) ----

        [Test]
        public async Task CopySubtree_ProductReferencingSharedEnum_BurnsEnumFootprint()
        {
            Project project = await new ProjectAppService(Settings).Load("testdata/" + Original);
            ProjectEditor editor = project.Edit();
            editor.NormalizeCatalogEnums();                       // Logning → _0x57347 (+ 6 values)
            Project beforeCopy = editor.ToProject();
            int enumDefsBefore = beforeCopy.Child("enum_definitions")!
                .ChildrenOrEmpty().Count(e => e.Tag == "enum_definition");
            long lastBefore = HexValue(beforeCopy.LastUniqueId!);   // _0x579

            // Copy the "med logning" sensor (2 resource_enum → shared Logning) into an empty group.
            ElementId copyId = editor.CopySubtree(Id("_0x5e53"), Id("_0x56c32"));
            Project after = editor.ToProject();
            ProjectElement copy = after.FindById(copyId)!;
            var copyEnumRows = copy.Descendants().Where(e => e.Tag == "resource_enum").ToList();

            Assert.Multiple(() =>
            {
                // The vendor burns the referenced enum's footprint (Logning: def + 6 values = 7) between the product
                // id and its 9 serialized children → 17 ids for a 10-element product (1 + 7 + 9), not 10.
                Assert.That(HexValue(after.LastUniqueId!) - lastBefore, Is.EqualTo(17),
                    "product (1) + burned Logning footprint (7) + children (9)");
                Assert.That(after.Child("enum_definitions")!.ChildrenOrEmpty().Count(e => e.Tag == "enum_definition"),
                    Is.EqualTo(enumDefsBefore), "shared enum reused, not duplicated");
                Assert.That(copyEnumRows, Has.Count.EqualTo(2));
                Assert.That(copyEnumRows.All(r => r.GetAttribute("typedef") == "_0x57347"), Is.True,
                    "copy's resource_enum rows point at the shared re-hoisted Logning");
            });
        }

        // ---- Full replay: Action 0 → copies A, B, C → byte-identity ----

        [Test]
        public async Task Copy_ReplaysProject3CopyOracle_ByteIdentical()
        {
            byte[] expected = TestData.ReadBytes(CopyOracle);
            var app = new ProjectAppService(Settings);
            Project original = await app.Load("testdata/" + Original);

            ProjectEditor editor = original.Edit();
            editor.NormalizeCatalogEnums();                    // Action 0  (_0x56d.._0x579)
            editor.CopySubtree(Id("_0x5153"), Id("_0x56c32")); // A FUGA       → Lokalitet
            editor.CopySubtree(Id("_0x5453"), Id("_0x2a32"));  // B Lampeudtag → Udendørs
            editor.CopySubtree(Id("_0x5e53"), Id("_0x56c32")); // C TempSensor → Lokalitet (after A)

            Project stamped = MetadataStamper.Restamp(editor.ToProject(),
                new DateTimeOffset(2026, 7, 4, 15, 57, 29, TimeSpan.Zero)); // id2=_0x40f391d, modified 15:57
            using var ms = new MemoryStream();
            await app.Save(stamped, ms, ProjectSaveOptions.PreserveExistingMetadata);

            TestData.AssertBytesIdentical(expected, ms.ToArray(), "copy replay → " + CopyOracle);
        }

        // ----- helpers -----

        private static ElementId Id(string token) =>
            ElementId.TryParse(token, out ElementId id) ? id : throw new ArgumentException($"Bad id token: {token}");

        private static long HexValue(string token) =>
            long.Parse(token.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
