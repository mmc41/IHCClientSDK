using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// M3 / 3.4 — <see cref="ProjectEditor.AddEnumDefinition"/>: authoring a project-global user enum (definition +
    /// values) the way IHC Visual does on first use — project2's "NyTypeForThisProject" (def + 3 values, contiguous,
    /// each stamped with its 0-based index) and project3's value-less "TestEnum". Loads Project1 as a base (no
    /// catalog needed) so the allocator has a realistic high-water.
    /// </summary>
    public class EnumDefinitionAuthoringTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> Load() => new ProjectAppService(Settings).Load("testdata/Project1-SimpelWired.vis");

        private static long HexCounter(string? token) =>
            long.Parse(token!.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        private static string Token(long counter) => "_0x" + counter.ToString("x", CultureInfo.InvariantCulture);

        [Test]
        public async Task AddEnumDefinition_AllocatesDefThenValuesContiguously_StampsIndex_Appends()
        {
            Project project = await Load();
            long seed = HexCounter(project.LastUniqueId);
            ProjectEditor editor = project.Edit();

            EnumDefinitionRef def = editor.AddEnumDefinition("NyTypeForThisProject", "Værdi1", "Værdi2", "Værdi3");
            Project after = editor.ToProject();

            ProjectElement added = after.Child("enum_definitions")!.Children.Last();

            Assert.Multiple(() =>
            {
                Assert.That(added.GetAttribute("name"), Is.EqualTo("NyTypeForThisProject"));
                Assert.That(added.Id!.Value.Counter, Is.EqualTo(seed + 1), "def allocated at the next counter");
                Assert.That(added.Children.Select(v => v.Id!.Value.Counter),
                    Is.EqualTo(new[] { seed + 2, seed + 3, seed + 4 }), "3 values contiguous after the def (R1 document order)");
                Assert.That(added.Children.Select(v => v.GetAttribute("name")),
                    Is.EqualTo(new[] { "Værdi1", "Værdi2", "Værdi3" }));
                // 0-based index: 0 is the DTD default → elided; 1 and 2 are written (matches project2).
                Assert.That(added.Children[0].GetAttribute("index"), Is.Null, "index 0 elided (DTD default)");
                Assert.That(added.Children[1].GetAttribute("index"), Is.EqualTo("1"));
                Assert.That(added.Children[2].GetAttribute("index"), Is.EqualTo("2"));
                Assert.That(def.Typedef, Is.EqualTo(added.GetAttribute("id")), "ref wires a resource_enum typedef");
                Assert.That(def.InitialValue("Værdi2"), Is.EqualTo(added.Children[1].GetAttribute("id")),
                    "ref wires inivalue by value name");
                Assert.That(after.LastUniqueId, Is.EqualTo(Token(seed + 4)), "high-water advanced by def + 3 values");
            });
        }

        [Test]
        public async Task AddEnumDefinition_WithNoValues_AllocatesOnlyTheDef()
        {
            Project project = await Load();
            long seed = HexCounter(project.LastUniqueId);
            ProjectEditor editor = project.Edit();

            editor.AddEnumDefinition("TestEnum æøå");
            Project after = editor.ToProject();

            ProjectElement added = after.Child("enum_definitions")!.Children.Last();

            Assert.Multiple(() =>
            {
                Assert.That(added.GetAttribute("name"), Is.EqualTo("TestEnum æøå"));
                Assert.That(added.Children, Is.Empty, "no values");
                Assert.That(after.LastUniqueId, Is.EqualTo(Token(seed + 1)), "only the def id allocated");
            });
        }
    }
}
