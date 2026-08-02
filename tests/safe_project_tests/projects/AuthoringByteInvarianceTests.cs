using System.Globalization;
using System.Text;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The authoring/builder byte-invariance gate on <c>project3-KompleksWired.vis</c> — the largest, most exotic
    /// authentic oracle (237 KB, 2,499 lines, airlink + s0/kWh + RS-485 + enums + follow-links + custom FBs). Each
    /// test drives a <b>real mutation through the builder API followed by an exact inverse (or a no-op)</b>, then
    /// saves with <see cref="ProjectSaveOptions.PreserveExistingMetadata"/> (no clock re-stamp) and asserts the bytes
    /// against the committed original — so every layer of the pipeline (<see cref="InsertTransform"/> via copy,
    /// <see cref="ProjectEditor.MoveSubtree"/>, the <see cref="ProjectEditor.DeleteById"/> cascade,
    /// <see cref="ProjectEditor.Link"/>/<see cref="ProjectEditor.Unlink"/>, attribute editing, and the
    /// <c>Canonicalizer</c>/allocator in <see cref="ProjectEditor.ToProject"/>) is gated on byte-fidelity against
    /// project3's exotic content, a hole the id/structure/validation-only mutation tests and the Project1-only byte
    /// mutation test left open.
    /// <para>
    /// Two assertion shapes: <b>byte-identical to the original</b> for reversible ops that allocate nothing (move,
    /// rename, attr set-then-restore, no-op round-trip); and <b>identical except the <c>last_unique_id</c> token,
    /// pinned to <c>original counter + N</c></b> for allocate-then-remove pairs (copy-then-delete, link-then-unlink,
    /// add-then-delete) — allocation is monotone so the only legal residue of a perfect structural inverse is the
    /// advanced counter, and pinning the exact value simultaneously gates delete/unlink-cascade exactness, monotone
    /// allocation, and allocation-count exactness (N is computed independently from the source, never read back).
    /// </para>
    /// <para>
    /// <b>Confidence boundary (not oversold).</b> These prove the pipeline preserves byte-fidelity through real
    /// mutations and that inverses are exact — they do <em>not</em> prove that newly-authored content matches the
    /// vendor byte-for-byte (exact id <em>values</em>, freshly generated DTD blocks, enum-hoist placement). That is
    /// the complementary mutated-oracle replay track, whose vendor "after" oracle is already committed
    /// (<c>testdata/projects/project3-KompleksWired-mutated.vis</c>, replay spec inline in
    /// <c>testdata/testdataoverview.md</c>); only that replay test is still missing. Likewise T5 gates that
    /// copy-then-delete is inverse-exact; the vendor-parity of <see cref="ProjectEditor.CopySubtree"/> itself — that
    /// it byte-reproduces what IHC Visual's clipboard paste writes — is now gated by
    /// <c>CopyPasteReplayByteFidelityTests</c> against the committed <c>project3-KompleksWired-copied.vis</c> oracle.
    /// All tests run without an installed catalog.
    /// </para>
    /// </summary>
    public class AuthoringByteInvarianceTests
    {
        private const string Oracle = "project3-KompleksWired.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/projects/" + Oracle);

        private static async Task<byte[]> Save(Project project)
        {
            var app = new ProjectAppService(Settings);
            using var ms = new MemoryStream();
            await app.Save(project, ms, ProjectSaveOptions.PreserveExistingMetadata);
            return ms.ToArray();
        }

        // ---- T1: the whole-tree round-trip through the edit session ----

        [Test]
        public async Task T1_NoOpEditorRoundTrip_IsByteIdentical()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();

            byte[] actual = await Save(project.Edit().ToProject());

            TestData.AssertBytesIdentical(original, actual, "T1 no-op editor round-trip");
        }

        // ---- T2: attribute set-then-restore across the exotic families ----

        // One representative element of each exotic family; set-then-restore its (present) name and save.
        [TestCase("product_airlink")]
        [TestCase("product_rs485_led_dimmer")]
        [TestCase("kWh")]              // an s0_device child
        [TestCase("resource_enum")]
        public async Task T2a_SetThenRestoreName_AcrossExoticFamilies_IsByteIdentical(string tag)
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();
            ProjectElement element = project.Root.Descendants().First(e => e.Tag == tag);
            string name = element.GetAttribute("name")!;

            ProjectEditor editor = project.Edit();
            editor.TryResolve(element.Id!.Value, out ElementRef? handle);
            handle!.SetAttribute("name", name + " (temp)").SetAttribute("name", name);

            byte[] actual = await Save(editor.ToProject());

            TestData.AssertBytesIdentical(original, actual, $"T2a set-then-restore <{tag}> name");
        }

        // The omit-if-default boundary: an s0 kWh whose accessibility is omitted (defaulting to "read-write"). Set it
        // to a non-default token, then back to the default — canonicalization must re-omit it → byte-identical.
        [Test]
        public async Task T2b_OmitIfDefaultBoundary_S0Accessibility_ReOmitted_IsByteIdentical()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();
            ProjectElement kwh = project.Root.Descendants()
                .First(e => e.Tag == "kWh" && e.GetAttribute("accessibility") is null);

            ProjectEditor editor = project.Edit();
            editor.TryResolve(kwh.Id!.Value, out ElementRef? handle);
            handle!.SetAttribute("accessibility", "read")       // non-default → materialized
                   .SetAttribute("accessibility", "read-write"); // == DTD default → dropped on commit

            byte[] actual = await Save(editor.ToProject());

            TestData.AssertBytesIdentical(original, actual, "T2b omit-if-default (s0 kWh accessibility)");
        }

        // ---- T3: move a wired subtree out and back to its exact slot ----

        [Test]
        public async Task T3_MoveWiredComponentThereAndBack_IsByteIdentical()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();
            ProjectElement sourceRoom = project.Groups[0];                       // Stue: holds the wired FUGA
            ProjectElement moved = sourceRoom.Children.First(HasLinks);          // the follow-linked product
            int originalIndex = IndexOfChild(sourceRoom, moved.Id!.Value);
            ElementId targetGroupId = project.Groups[^1].Id!.Value;             // an empty room to park it in

            ProjectEditor editor = project.Edit();
            editor.MoveSubtree(moved.Id!.Value, targetGroupId);                 // relocate (ids preserved)
            editor.MoveSubtree(moved.Id!.Value, sourceRoom.Id!.Value, originalIndex);  // put it back exactly

            byte[] actual = await Save(editor.ToProject());

            TestData.AssertBytesIdentical(original, actual, "T3 move wired subtree there-and-back");
        }

        // ---- T4: rename + note cycle on the diacritic-heavy room ----

        [Test]
        public async Task T4_GroupRenameAndNoteCycle_IsByteIdentical()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();
            ProjectElement room = project.Groups[0];                            // "Stue & Køkken \"åben\"" + Latin-1 note
            string name = room.GetAttribute("name")!;
            string note = room.GetAttribute("note")!;

            ProjectEditor editor = project.Edit();
            GroupRef group = editor.Group(name);
            group.Name(name + " (temp)").Name(name);                            // rename out and back
            group.Note(note + " (temp)").Note(note);                            // re-note out and back

            byte[] actual = await Save(editor.ToProject());

            TestData.AssertBytesIdentical(original, actual, "T4 group rename + note cycle");
        }

        // ---- T5: copy a link/enum/scene-free subtree, then delete the copy ----

        [Test]
        public async Task T5_CopyThenDeleteTheCopy_IsIdenticalExceptPinnedLastUniqueId()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();
            // The s0 device is follow-link-free, enum-free and scenes-free, so copy-then-delete is a structural
            // inverse whose only residue is the counter. N = its id-bearing element count (every element gets a
            // fresh id; an in-project copy hoists no enums, so no extra burns).
            ProjectElement source = project.Root.Descendants().First(e => e.Tag == "s0_device");
            int n = IdBearingElementCount(source);
            ElementId targetGroupId = project.Groups[0].Id!.Value;              // paste into a different room

            ProjectEditor editor = project.Edit();
            ElementId copyId = editor.CopySubtree(source.Id!.Value, targetGroupId);
            editor.DeleteById(copyId);
            Project after = editor.ToProject();

            AssertIdenticalExceptLastUniqueId(original, await Save(after), project.LastUniqueId!, after.LastUniqueId!,
                n, "T5 copy-then-delete-the-copy");
        }

        // ---- T6: link two unlinked resources, then unlink them ----

        [Test]
        public async Task T6_LinkThenUnlink_IsIdenticalExceptPinnedLastUniqueId()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();
            string room = project.Groups[0].GetAttribute("name")!;             // read the diacritic room name from the model

            ProjectEditor editor = project.Edit();
            // project3's only follow-links are the three FUGA→AND wires, so both of these are unlinked. Link
            // allocates exactly two halves; unlink must remove exactly those two — a structural inverse, N = 2.
            // The pair is the vendor's most common wire (a block result driving a product output, 83× in the
            // authored corpus); two product outputs would not be linkable at all — see LinkLegalityTests.
            ResourceRef a = editor.Group(room).FunctionBlock("1.1.01.e. Kip tænd sluk").Output("Udgang");
            ResourceRef b = editor.Group(room).Product("Lampeudtag").Output("Udgang");
            editor.Link(a, b).Unlink(a, b);
            Project after = editor.ToProject();

            AssertIdenticalExceptLastUniqueId(original, await Save(after), project.LastUniqueId!, after.LastUniqueId!,
                2, "T6 link-then-unlink");
        }

        // ---- T7: intra-container reorder there-and-back ----

        [Test]
        public async Task T7_ReorderThereAndBack_IsByteIdentical()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();
            ProjectElement room = project.Groups[0];
            ProjectElement child = room.Children.First(c => c.GetAttribute("name") == "Diode");
            int originalIndex = IndexOfChild(room, child.Id!.Value);

            ProjectEditor editor = project.Edit();
            editor.MoveSubtree(child.Id!.Value, room.Id!.Value, index: 0);      // reorder to the front
            editor.MoveSubtree(child.Id!.Value, room.Id!.Value, originalIndex); // and back to its slot

            byte[] actual = await Save(editor.ToProject());

            TestData.AssertBytesIdentical(original, actual, "T7 reorder there-and-back");
        }

        // ---- T8: unlock a loaded locked function block, then undo ----
        //
        // Re-sourced for uxparity S-20: unlocking is no longer a flag toggle with a flag-toggle inverse — it also
        // discards the library identity and re-stamps ownership (FunctionBlockRef.Unlock), so `Unlock().Locked()`
        // cannot return the file to its original bytes and asserting that it does would only be pinning a bug.
        // The operation that IS invertible is the session command, so the invariance is stated over that.

        [Test]
        public async Task T8_FunctionBlockUnlockThenUndo_IsByteIdentical()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();
            ProjectElement locked = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") == "yes");

            var session = new Session.ProjectDocumentSession();
            session.Open(project);
            session.Apply(new Session.UnlockFunctionBlock(locked.Id!.Value, "Test Installer", new DateOnly(2026, 1, 1)));
            session.Undo();

            byte[] actual = await Save(session.Current!);

            TestData.AssertBytesIdentical(original, actual, "T8 FB unlock/undo cycle");
        }

        // ---- T9: add an internal variable to a Tom blok, then delete it ----

        [Test]
        public async Task T9_AddResourceThenDelete_IsIdenticalExceptPinnedLastUniqueId()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();
            ProjectElement emptyBlock = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Tom blok");
            string room = project.FindParent(emptyBlock.Id!.Value)!.GetAttribute("name")!;

            ProjectEditor editor = project.Edit();
            FunctionBlockRef block = editor.Group(room).FunctionBlock("Tom blok");
            ResourceRef added = block.AddInternalVariable("resource_flag", "burn");  // materializes one resource id
            editor.DeleteById(added.Id!.Value);
            Project after = editor.ToProject();

            AssertIdenticalExceptLastUniqueId(original, await Save(after), project.LastUniqueId!, after.LastUniqueId!,
                1, "T9 add-resource-then-delete");
        }

        // ---- T10: author an enum (def + 3 values), then delete it ----

        // Never NormalizeCatalogEnums() here: Action 0 is an irreversible byte mutation (renumbers/moves the catalog
        // enums, repoints 4 refs) that deleting the authored enum cannot undo — so inverse-exactness must live on the
        // un-normalized project3. AddEnumDefinition appends regardless of normalization; the def is unreferenced, so
        // DeleteById cascades only its own 3 value children and trips no dangling-ref guard. N = 1 def + 3 values.
        [Test]
        public async Task T10_AddEnumWithValuesThenDelete_IsIdenticalExceptPinnedLastUniqueId()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();

            ProjectEditor editor = project.Edit();
            EnumDefinitionRef def = editor.AddEnumDefinition("InverseProbe", "Alpha", "Beta", "Gamma");
            ElementId.TryParse(def.Typedef, out ElementId defId);   // def.Typedef == defId.ToToken()
            editor.DeleteById(defId);                                // removes the def + its 3 value children
            Project after = editor.ToProject();

            AssertIdenticalExceptLastUniqueId(original, await Save(after), project.LastUniqueId!, after.LastUniqueId!,
                1 + 3, "T10 add-enum-with-values-then-delete");
        }

        // ---- T11: scene-link a product's scenes container to an FB scene pin, then unlink ----

        // N = member row + scene_link (member-first, ENG-A2). T11a uses the relay endpoints of the -scenelinks
        // oracle; T11b uses its dimmer endpoints but with NON-default magnitudes (80 % / 5 s) deliberately: the A2
        // capture could only record the scenario dialog's defaults (100 % / 1000 ms), so non-default value
        // serialization is gated here instead.
        [Test]
        public async Task T11a_LinkSceneRelayThenUnlink_IsIdenticalExceptPinnedLastUniqueId()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();

            ProjectEditor editor = project.Edit();
            GroupRef room = editor.Group("Stue & Køkken \"åben\"");
            ResourceRef pin = room.FunctionBlock("1.1.01.e. Kip tænd sluk").SceneOutput("Scenarie Sluk");
            ScenesRef target = room.Product("Lampeudtag").Scenes();
            editor.LinkScene(pin, target, SceneValue.Relay(on: true));
            editor.UnlinkScene(pin, target);
            Project after = editor.ToProject();

            AssertIdenticalExceptLastUniqueId(original, await Save(after), project.LastUniqueId!, after.LastUniqueId!,
                2, "T11a link-scene-relay-then-unlink");
        }

        [Test]
        public async Task T11b_LinkSceneDimmerNonDefaultThenUnlink_IsIdenticalExceptPinnedLastUniqueId()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();

            ProjectEditor editor = project.Edit();
            ResourceRef pin = editor.Group("Værelse").FunctionBlock("4.1.01. AND (\"Og\"- blok)")
                .SceneOutput("Scenarie Sluk");
            ScenesRef target = editor.Group("Soveværelse").Product("Dimmer Universal").Scenes();
            editor.LinkScene(pin, target, SceneValue.Dimmer(80, TimeSpan.FromSeconds(5)));
            editor.UnlinkScene(pin, target);
            Project after = editor.ToProject();

            AssertIdenticalExceptLastUniqueId(original, await Save(after), project.LastUniqueId!, after.LastUniqueId!,
                2, "T11b link-scene-dimmer-then-unlink");
        }

        // ---- T12: OR-then-AND toggle on an existing stock conditions group (G2, US-029) ----

        // Pure attribute cycle through the id-addressed ConditionsGroupRef surface on a loaded project: Or() writes
        // the literal type="or", And() returns to the DTD default "and" which the canonicalizer re-omits — no ids,
        // no residue. The target is a stock conditions group that ships without a type attribute.
        [Test]
        public async Task T12_OrThenAndOnExistingConditions_IsByteIdentical()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();
            ProjectElement conditions = project.Root.Descendants()
                .First(e => e.Tag == "conditions" && e.GetAttribute("type") is null);

            ProjectEditor editor = project.Edit();
            editor.ConditionsGroup(conditions.Id!.Value).Or().And();

            TestData.AssertBytesIdentical(original, await Save(editor.ToProject()),
                "T12 or-then-and cycle on a stock group");
        }

        // ---- T13: author a sub-program + nested logic group on an empty block, then delete the sub (G2) ----

        // N = the 4-node program_sub skeleton + 1 nested conditions group; deleting the program_sub removes all
        // five. Garage's "Tom blok" ships with an empty program_simple, so the authored sub is its sole program_sub.
        [Test]
        public async Task T13_AddSubProgramWithConditionGroupThenDelete_IsIdenticalExceptPinnedLastUniqueId()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();

            ProjectEditor editor = project.Edit();
            SubProgramRef sub = editor.Group("Garage").FunctionBlock("Tom blok").Program().AddSubProgram();
            sub.Conditions.AddConditionGroup();
            ProjectElement garage = editor.ToProject().Groups.First(g => g.GetAttribute("name") == "Garage");
            editor.DeleteById(garage.Descendants().Single(e => e.Tag == "program_sub").Id!.Value);
            Project after = editor.ToProject();

            AssertIdenticalExceptLastUniqueId(original, await Save(after), project.LastUniqueId!, after.LastUniqueId!,
                4 + 1, "T13 add-sub-program-with-group-then-delete");
        }

        // ---- T14: author a case switch (values + default action), then delete the host sub (G3, US-031) ----

        // N = the 4-node program_sub skeleton + program_case with its eagerly-allocated Else (2) + one case value
        // (case_action + embedded counter operand, 2) + one Else action — the SDK burns nothing (the vendor's
        // Rediger-konstant burn is a UI artifact). Deleting the program_sub removes all nine.
        [Test]
        public async Task T14_AddCaseWithValuesThenDelete_IsIdenticalExceptPinnedLastUniqueId()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();

            ProjectEditor editor = project.Edit();
            FunctionBlockRef tomBlok = editor.Group("Garage").FunctionBlock("Tom blok");
            ResourceRef criterion = tomBlok.AddInternalVariable("resource_counter", "T14 Tæller");
            SubProgramRef sub = tomBlok.Program().AddSubProgram();
            CaseRef kase = sub.WhenTrue.AddCase("Case (%LT)", criterion);
            kase.Case("Case", "resource_counter", op => op.SetAttribute("inivalue", "100"));
            kase.Default().AddAction("%P = 0", criterion, "_0xa");
            ProjectElement garage = editor.ToProject().Groups.First(g => g.GetAttribute("name") == "Garage");
            editor.DeleteById(garage.Descendants().Single(e => e.Tag == "program_sub").Id!.Value);
            editor.DeleteById(criterion.Id!.Value);
            Project after = editor.ToProject();

            AssertIdenticalExceptLastUniqueId(original, await Save(after), project.LastUniqueId!, after.LastUniqueId!,
                1 + 4 + 2 + 2 + 1, "T14 add-case-with-values-then-delete");
        }

        // ---- T15: Projektinfo set-then-restore across all three id-less metadata blocks (G4, US-039) ----

        // Fill every dialog field of project_info / customer_info / installer_info, then restore the seed:
        // project3 carries only programmer + installer name/country, so every other field restores to ""
        // (blank ⇒ re-omitted on commit), re-emptying customer_info entirely. Pure cycle — no ids involved.
        [Test]
        public async Task T15_SetThenRestoreProjectInfo_IsByteIdentical()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();

            ProjectEditor editor = project.Edit();
            editor.SetProjectInfo(p => p.Programmer("T15").Number("T15").Drawing("T15").Type("T15").Description("T15"))
                  .SetCustomerInfo(c => c.Name("T15").Address("T15").City("T15").ZipCode("T15").Country("T15")
                                         .Phone("T15").MobilePhone("T15").Email("T15"))
                  .SetInstallerInfo(i => i.Name("T15").Address("T15").City("T15").ZipCode("T15").Country("T15")
                                          .Phone("T15").MobilePhone("T15").Email("T15"));
            editor.SetProjectInfo(p => p.Programmer("Morten Christensen").Number("").Drawing("").Type("").Description(""))
                  .SetCustomerInfo(c => c.Name("").Address("").City("").ZipCode("").Country("")
                                         .Phone("").MobilePhone("").Email(""))
                  .SetInstallerInfo(i => i.Name("Morten").Address("").City("").ZipCode("").Country("Danmark")
                                          .Phone("").MobilePhone("").Email(""));

            byte[] actual = await Save(editor.ToProject());

            TestData.AssertBytesIdentical(original, actual, "T15 Projektinfo set-then-restore");
        }

        // ---- T17: append values to the pre-existing empty TestEnum, then delete them (M-A, US-030) ----

        // Never NormalizeCatalogEnums() here (same reason as T10). "TestEnum æøå äö "x"" ships empty and
        // unreferenced, so the three appended values are the only delta, deleting them trips no dangling-ref
        // guard, and the emptied definition must serialize back to its original self-closed form. N = 3 values.
        [Test]
        public async Task T17_AddEnumValuesThenDelete_IsIdenticalExceptPinnedLastUniqueId()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();

            ProjectEditor editor = project.Edit();
            EnumDefinitionRef appended = editor.AddEnumValues(
                editor.EnumDefinition("TestEnum æøå äö \"x\""), "P1", "P2", "P3");
            foreach (string value in new[] { "P1", "P2", "P3" })
            {
                ElementId.TryParse(appended.InitialValue(value), out ElementId valueId);
                editor.DeleteById(valueId);
            }
            Project after = editor.ToProject();

            AssertIdenticalExceptLastUniqueId(original, await Save(after), project.LastUniqueId!, after.LastUniqueId!,
                3, "T17 append-enum-values-then-delete");
        }

        // ---- T18: exporting a placed block is a pure read — the saved project is untouched (G5, US-021) ----

        // The deliberate deviation from vendor Gem (which renames + re-stamps the in-document block): the SDK's
        // ExportDefinition must leave the session without a trace, so a bare save afterwards is byte-identical.
        [Test]
        public async Task T18_ExportDefinition_LeavesProjectByteIdentical()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();

            ProjectEditor editor = project.Edit();
            editor.Group("Stue & Køkken \"åben\"").FunctionBlock("1.1.01.e. Kip tænd sluk")
                .ExportDefinition("T18", "T18", new DateOnly(2026, 7, 12), "T18");

            TestData.AssertBytesIdentical(original, await Save(editor.ToProject()), "T18 export-is-read-only");
        }

        // ----- helpers -----

        private static bool HasLinks(ProjectElement subtree) =>
            new[] { subtree }.Concat(subtree.Descendants())
                .Any(e => e.Tag is "link_from_resource" or "link_to_resource");

        private static int IndexOfChild(ProjectElement parent, ElementId childId)
        {
            for (int i = 0; i < parent.Children.Length; i++)
            {
                if (parent.Children[i].Id == childId)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// The number of id-bearing elements in the subtree (root included) — exactly the count of fresh ids a copy
        /// of it allocates, since <see cref="InsertTransform"/> allocates one per element carrying an <c>id</c>.
        /// </summary>
        private static int IdBearingElementCount(ProjectElement subtree)
        {
            int count = 0;
            void Walk(ProjectElement e)
            {
                if (e.GetAttribute("id") is not null)
                {
                    count++;
                }
                if (!e.Children.IsDefaultOrEmpty)
                {
                    foreach (ProjectElement child in e.Children)
                    {
                        Walk(child);
                    }
                }
            }
            Walk(subtree);
            return count;
        }

        /// <summary>
        /// Asserts the saved bytes equal the original with only the root <c>last_unique_id</c> token advanced to
        /// <c>original + expectedAllocations</c> — patching that exact value into the expected buffer (not a bare
        /// "differs only there" comparison), so the byte diff also gates monotone, count-exact allocation. Cross-checks
        /// the committed project's own <c>last_unique_id</c> against the same expected value for a named diagnostic.
        /// </summary>
        private static void AssertIdenticalExceptLastUniqueId(byte[] original, byte[] actual, string originalToken,
            string actualToken, int expectedAllocations, string label)
        {
            long expectedValue = HexValue(originalToken) + expectedAllocations;
            string expectedToken = "_0x" + expectedValue.ToString("x", CultureInfo.InvariantCulture);
            Assert.That(actualToken, Is.EqualTo(expectedToken),
                $"{label}: the inverse must advance last_unique_id by exactly {expectedAllocations} (monotone; freed ids are not reused)");
            byte[] expected = PatchLastUniqueId(original, originalToken, expectedToken);
            TestData.AssertBytesIdentical(expected, actual, label);
        }

        private static long HexValue(string token) =>
            long.Parse(token.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        private static byte[] PatchLastUniqueId(byte[] original, string oldToken, string newToken)
        {
            byte[] search = Encoding.ASCII.GetBytes($"last_unique_id=\"{oldToken}\"");
            byte[] replacement = Encoding.ASCII.GetBytes($"last_unique_id=\"{newToken}\"");
            int at = IndexOf(original, search);
            if (at < 0)
            {
                throw new InvalidOperationException($"last_unique_id token '{oldToken}' not found in the original bytes.");
            }
            var patched = new byte[original.Length - search.Length + replacement.Length];
            Array.Copy(original, 0, patched, 0, at);
            Array.Copy(replacement, 0, patched, at, replacement.Length);
            Array.Copy(original, at + search.Length, patched, at + replacement.Length,
                original.Length - at - search.Length);
            return patched;
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
