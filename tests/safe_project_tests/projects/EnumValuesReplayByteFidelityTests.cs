using System.Globalization;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The enum-authoring byte-fidelity gate for <see cref="ProjectEditor.AddEnumDefinition"/> against the authentic
    /// vendor oracle <c>project3-KompleksWired-enumvalues.vis</c> (IHC Visual 03.04.72.03 after authoring one new
    /// enum-with-values on <c>project3-KompleksWired.vis</c>, single save). The SDK loads the original, reproduces
    /// the vendor's one-time load-time enum re-hoist (<see cref="ProjectEditor.NormalizeCatalogEnums"/> — Action 0),
    /// authors the standalone enum <c>ValueOracleEnum = {Alpha, Beta, Gamma}</c> (Action E), restamps to the oracle's
    /// clock and asserts byte-identity. This is the one gate the six passive fixtures over the same oracle never
    /// exercise: the enum-authoring <b>write</b> path composed in a real mutation context on populated,
    /// post-re-hoist project3. Both verbs are catalog-free (allocator + inline-DTD only), so these run unconditionally.
    /// </summary>
    public class EnumValuesReplayByteFidelityTests
    {
        private const string Original = "project3-KompleksWired.vis";
        private const string EnumValuesOracle = "project3-KompleksWired-enumvalues.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        // ---- Full replay: Action 0 → author ValueOracleEnum → byte-identity ----

        [Test]
        public async Task AddEnumWithValues_ReplaysProject3EnumValuesOracle_ByteIdentical()
        {
            byte[] expected = TestData.ReadBytes("projects/" + EnumValuesOracle);
            var app = new ProjectAppService(Settings);
            Project original = await app.Load("testdata/projects/" + Original);

            ProjectEditor editor = original.Edit();
            editor.NormalizeCatalogEnums();                                        // Action 0: _0x56c -> _0x579
            editor.AddEnumDefinition("ValueOracleEnum", "Alpha", "Beta", "Gamma"); // Action E: def _0x57a, vals _0x57b/c/d

            // id2=_0x4120a2f decodes to day 4 / hour 18 / min 10 / sec 47; <modified> is minute-precision (18:10),
            // so the second (47) lives only in id2 and must be supplied to the restamp clock.
            Project stamped = MetadataStamper.Restamp(editor.ToProject(),
                new DateTimeOffset(2026, 7, 4, 18, 10, 47, TimeSpan.Zero));
            using var ms = new MemoryStream();
            await app.Save(stamped, ms, ProjectSaveOptions.PreserveExistingMetadata);

            TestData.AssertBytesIdentical(expected, ms.ToArray(), "enum-values replay → " + EnumValuesOracle);
        }

        // ---- Validate: the authored enum is standalone / unreferenced, and that is legal ----

        [Test]
        public async Task AddedEnum_StandaloneUnreferenced_ValidatesClean()
        {
            var app = new ProjectAppService(Settings);
            Project original = await app.Load("testdata/projects/" + Original);

            ProjectEditor editor = original.Edit();
            editor.NormalizeCatalogEnums();
            editor.AddEnumDefinition("ValueOracleEnum", "Alpha", "Beta", "Gamma");
            Project after = editor.ToProject();

            ProjectValidationResult validation = app.Validate(after);

            Assert.That(validation.IsValid, Is.True,
                "a standalone, unreferenced authored enum is legal (project3 already ships an unreferenced TestEnum); "
                + "errors: " + string.Join(" | ", validation.Errors));
        }

        // ---- Composition isolation: the def lands right after the Action-0 boundary, no id burn ----

        [Test]
        public async Task AddEnumDefinition_AfterNormalizeCatalogEnums_AllocatesContiguouslyNoBurn()
        {
            Project original = await new ProjectAppService(Settings).Load("testdata/projects/" + Original);

            ProjectEditor editor = original.Edit();
            editor.NormalizeCatalogEnums();
            long boundary = HexCounter(editor.ToProject().LastUniqueId);   // == _0x579 (ToProject is a pure snapshot)
            editor.AddEnumDefinition("CompositionProbe", "A", "B", "C");
            Project after = editor.ToProject();
            ProjectElement def = after.Child("enum_definitions")!.Children.Last();

            Assert.Multiple(() =>
            {
                Assert.That(boundary, Is.EqualTo(0x579), "Action-0 boundary");
                Assert.That(def.Id!.Value.Counter, Is.EqualTo(0x57a), "authored def contiguous right after the re-hoist");
                Assert.That(HexCounter(after.LastUniqueId) - boundary, Is.EqualTo(4), "def + 3 values, no burn");
                Assert.That(after.Root.Descendants().Any(e => e.GetAttribute("typedef") == def.GetAttribute("id")),
                    Is.False, "authored enum is standalone / unreferenced");
            });
        }

        // ----- helpers -----

        private static long HexCounter(string? token) =>
            long.Parse(token!.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
