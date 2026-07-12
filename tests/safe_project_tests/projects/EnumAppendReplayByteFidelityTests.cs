using System.Globalization;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The enum-append byte-fidelity gate for <see cref="ProjectEditor.AddEnumValues"/> against the authentic vendor
    /// oracle <c>project3-KompleksWired-enumappend.vis</c> (IHC Visual 03.04.72.03 after appending three values to
    /// the pre-existing EMPTY enum <c>TestEnum æøå äö "x"</c> on <c>project3-KompleksWired.vis</c>, single save) —
    /// the mutation-context complement of <see cref="EnumValuesReplayByteFidelityTests"/>, which pinned enum
    /// <em>creation</em>. Pins the ENG-A3 semantics: append <b>in place</b> (the definition keeps id
    /// <c>_0x51147</c> and its document position — after Action 0 it sits BEFORE the two re-hoisted catalog enums,
    /// so a move-to-bottom bug breaks the bytes), one contiguous 0x48 id per value with no burn, and <c>index</c>
    /// continuing 0-based from empty with <c>index="0"</c> elided. Both verbs are catalog-free (allocator +
    /// inline-DTD only), so these run unconditionally.
    /// </summary>
    public class EnumAppendReplayByteFidelityTests
    {
        private const string Original = "project3-KompleksWired.vis";
        private const string EnumAppendOracle = "project3-KompleksWired-enumappend.vis";
        private const string TestEnumName = "TestEnum æøå äö \"x\"";
        private static IhcSettings Settings => TestSetup.Settings;

        // ---- Full replay: Action 0 → append AppendA/B/C to the empty TestEnum → byte-identity ----

        [Test]
        public async Task AddEnumValues_ReplaysProject3EnumAppendOracle_ByteIdentical()
        {
            byte[] expected = TestData.ReadBytes("projects/" + EnumAppendOracle);
            var app = new ProjectAppService(Settings);
            Project original = await app.Load("testdata/projects/" + Original);

            ProjectEditor editor = original.Edit();
            editor.NormalizeCatalogEnums();                              // Action 0: _0x56c -> _0x579
            editor.AddEnumValues(editor.EnumDefinition(TestEnumName),    // Action V: values _0x57a48/_0x57b48/_0x57c48
                "AppendA", "AppendB", "AppendC");

            // id2=_0xb0d1105 decodes to day 11 / hour 13 / min 17 / sec 5; <modified> is minute-precision (13:17),
            // so the second (5) lives only in id2 and must be supplied to the restamp clock.
            Project stamped = MetadataStamper.Restamp(editor.ToProject(),
                new DateTimeOffset(2026, 7, 11, 13, 17, 5, TimeSpan.Zero));
            using var ms = new MemoryStream();
            await app.Save(stamped, ms, ProjectSaveOptions.PreserveExistingMetadata);

            TestData.AssertBytesIdentical(expected, ms.ToArray(), "enum-append replay → " + EnumAppendOracle);
        }

        // ---- Validate: appending to the standalone, unreferenced enum stays legal ----

        [Test]
        public async Task AppendedValues_OnStandaloneUnreferencedEnum_ValidateClean()
        {
            var app = new ProjectAppService(Settings);
            Project original = await app.Load("testdata/projects/" + Original);

            ProjectEditor editor = original.Edit();
            editor.NormalizeCatalogEnums();
            editor.AddEnumValues(editor.EnumDefinition(TestEnumName), "AppendA", "AppendB", "AppendC");

            ProjectValidationResult validation = app.Validate(editor.ToProject());

            Assert.That(validation.IsValid, Is.True,
                "appending values to a standalone, unreferenced enum is legal; errors: "
                + string.Join(" | ", validation.Errors));
        }

        // ---- Composition isolation: in-place position, id retention, contiguous allocation, no burn ----

        [Test]
        public async Task AddEnumValues_AfterNormalizeCatalogEnums_AppendsInPlaceNoBurn()
        {
            Project original = await new ProjectAppService(Settings).Load("testdata/projects/" + Original);

            ProjectEditor editor = original.Edit();
            editor.NormalizeCatalogEnums();
            Project boundary = editor.ToProject();                       // pure snapshot at the Action-0 boundary
            int positionBefore = IndexOfTestEnum(boundary);
            editor.AddEnumValues(editor.EnumDefinition(TestEnumName), "AppendA", "AppendB", "AppendC");
            Project after = editor.ToProject();
            ProjectElement def = after.Child("enum_definitions")!.Children
                .Single(c => c.GetAttribute("name") == TestEnumName);

            Assert.Multiple(() =>
            {
                Assert.That(HexCounter(boundary.LastUniqueId), Is.EqualTo(0x579), "Action-0 boundary");
                Assert.That(def.GetAttribute("id"), Is.EqualTo("_0x51147"), "definition id retained");
                Assert.That(positionBefore,
                    Is.LessThan(boundary.Child("enum_definitions")!.Children.Length - 1),
                    "precondition: TestEnum is NOT at the bottom (the re-hoisted catalog enums sit below it), "
                    + "so position retention genuinely discriminates in-place from move-to-bottom");
                Assert.That(IndexOfTestEnum(after), Is.EqualTo(positionBefore), "document position retained");
                Assert.That(def.Children.Select(c => (long)c.Id!.Value.Counter),
                    Is.EqualTo(new long[] { 0x57a, 0x57b, 0x57c }),
                    "one contiguous id per value, allocated right after the boundary — no burn");
                Assert.That(HexCounter(after.LastUniqueId) - HexCounter(boundary.LastUniqueId), Is.EqualTo(3),
                    "exactly +3 ids");
            });
        }

        // ----- helpers -----

        private static int IndexOfTestEnum(Project project)
        {
            var defs = project.Child("enum_definitions")!.Children;
            for (int i = 0; i < defs.Length; i++)
            {
                if (defs[i].GetAttribute("name") == TestEnumName)
                {
                    return i;
                }
            }
            return -1;
        }

        private static long HexCounter(string? token) =>
            long.Parse(token!.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
