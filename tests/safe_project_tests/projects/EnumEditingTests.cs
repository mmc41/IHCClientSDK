namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Catalog-free units for the pre-existing-enum editing surface (backlog M-A, US-030 edit-existing flows):
    /// <see cref="ProjectEditor.EnumDefinition"/> — the resolver that makes enums authored in an earlier session
    /// wireable (<see cref="ConditionRef.AddEnumOperand"/>) and appendable — and
    /// <see cref="ProjectEditor.AddEnumValues"/>. The vendor append semantics are pinned byte-exactly by
    /// <see cref="EnumAppendReplayByteFidelityTests"/> (empty enum, project3); these units pin the surrounding
    /// contract the oracle does not reach: name resolution against authentic wiring (project2's
    /// "NyTypeForThisProject", referenced by five <c>resource_enum</c> rows), the absent-name contract, the
    /// "[read only]" catalog-enum guard, index continuation on a NON-empty definition, and that appending leaves
    /// existing values and every referencing <c>resource_enum</c> untouched.
    /// </summary>
    public class EnumEditingTests
    {
        private const string Project2 = "project2-CustomBlock.vis";
        private const string Project3 = "project3-KompleksWired.vis";

        private static Task<Project> Load(string file) =>
            new ProjectAppService(TestSetup.Settings).Load("testdata/projects/" + file);

        // ---- resolver (M-A part 1) ----

        [Test]
        public async Task EnumDefinition_ResolvesExistingByName_WiresTypedefAndInitialValues()
        {
            Project project = await Load(Project2);

            EnumDefinitionRef def = project.Edit().EnumDefinition("NyTypeForThisProject");

            Assert.Multiple(() =>
            {
                Assert.That(def.Typedef, Is.EqualTo("_0x8647"), "typedef token = the definition's own id");
                Assert.That(def.InitialValue("Værdi1"), Is.EqualTo("_0x8748"), "first value resolves");
                Assert.That(def.InitialValue("Værdi3"), Is.EqualTo("_0x8948"), "last value resolves");
            });
        }

        [Test]
        public async Task EnumDefinition_UnknownName_ThrowsListingAvailableDefinitions()
        {
            ProjectEditor editor = (await Load(Project2)).Edit();

            var ex = Assert.Throws<InvalidOperationException>(() => editor.EnumDefinition("NoSuchEnum"));

            Assert.That(ex!.Message, Does.Contain("NoSuchEnum").And.Contain("NyTypeForThisProject"),
                "the message names the missing enum and lists what the project actually has");
        }

        // ---- catalog guard: typeid enums resolve (legal wiring targets) but refuse value edits ----

        [Test]
        public async Task AddEnumValues_OnCatalogTypeidEnum_Throws()
        {
            ProjectEditor editor = (await Load(Project3)).Edit();
            EnumDefinitionRef logning = editor.EnumDefinition("Logning");   // resolving a catalog enum is legal

            var ex = Assert.Throws<InvalidOperationException>(() => editor.AddEnumValues(logning, "Illegal"));

            Assert.That(ex!.Message, Does.Contain("Logning").And.Contain("read only").IgnoreCase,
                "the guard mirrors IHC Visual's \"[read only]\" refusal for typeid-bearing catalog enums");
        }

        // ---- append on a NON-empty, referenced definition (the boundary the empty-enum oracle cannot reach) ----

        [Test]
        public async Task AddEnumValues_AppendsInPlaceContinuingIndexes_ExistingValuesAndRefsUntouched()
        {
            Project project = await Load(Project2);
            ProjectEditor editor = project.Edit();

            EnumDefinitionRef refreshed = editor.AddEnumValues(
                editor.EnumDefinition("NyTypeForThisProject"), "Appended4", "Appended5");
            Project after = editor.ToProject();

            ProjectElement def = after.Child("enum_definitions")!.Children
                .Single(c => c.GetAttribute("id") == "_0x8647");
            var enumRefs = after.Root.Descendants()
                .Where(e => e.Tag == "resource_enum" && e.GetAttribute("typedef") == "_0x8647")
                .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(def.Children.Select(c => c.GetAttribute("name")),
                    Is.EqualTo(new[] { "Værdi1", "Værdi2", "Værdi3", "Appended4", "Appended5" }),
                    "existing rows untouched, new rows appended in argument order");
                Assert.That(def.Children.Select(c => c.GetAttribute("index")),
                    Is.EqualTo(new string?[] { null, "1", "2", "3", "4" }),
                    "index continues 0-based from the existing count; index=\"0\" stays elided");
                Assert.That(def.Children.Take(3).Select(c => c.GetAttribute("id")),
                    Is.EqualTo(new[] { "_0x8748", "_0x8848", "_0x8948" }), "existing value ids retained");
                Assert.That(enumRefs.Select(r => (r.GetAttribute("id"), r.GetAttribute("inivalue"))),
                    Is.EqualTo(new[]
                    {
                        ("_0xa20f", "_0x8748"), ("_0xb50f", "_0x8748"), ("_0x8a0f", "_0x8748"),
                        ("_0x8c0f", "_0x8748"), ("_0xf30f", "_0x8848"),
                    }),
                    "all five pre-existing references untouched (incl. the embedded \"Enumerator\" operand row)");
                Assert.That(refreshed.InitialValue("Værdi1"), Is.EqualTo("_0x8748"),
                    "the refreshed handle still covers the pre-existing values");
                Assert.That(refreshed.InitialValue("Appended5"), Does.EndWith("48"),
                    "new values allocate enum_value (type 0x48) ids");
            });
        }
    }
}
