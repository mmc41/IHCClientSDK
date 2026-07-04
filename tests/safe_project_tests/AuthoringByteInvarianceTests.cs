using System.Globalization;
using System.Text;

namespace Ihc.Projects.Tests
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
    /// the complementary, install-gated mutated-oracle replay track, whose vendor "after" oracle is already committed
    /// (<c>testdata/project3-KompleksWired-mutated.vis</c> + its replay spec
    /// <c>testdata/project3-KompleksWired-mutated.actions.md</c>); only the replay test itself is missing there.
    /// Likewise T5 gates that copy-then-delete is inverse-exact, not that <see cref="ProjectEditor.CopySubtree"/>
    /// matches what IHC Visual's clipboard paste would write (no vendor copy-paste oracle exists) — copy is an
    /// SDK-defined operation here, not a vendor-parity one. All tests run without an installed catalog.
    /// </para>
    /// </summary>
    public class AuthoringByteInvarianceTests
    {
        private const string Oracle = "project3-KompleksWired.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/" + Oracle);

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
            byte[] original = TestData.ReadBytes(Oracle);
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
            byte[] original = TestData.ReadBytes(Oracle);
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
            byte[] original = TestData.ReadBytes(Oracle);
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
            byte[] original = TestData.ReadBytes(Oracle);
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
            byte[] original = TestData.ReadBytes(Oracle);
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
            byte[] original = TestData.ReadBytes(Oracle);
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
            byte[] original = TestData.ReadBytes(Oracle);
            Project project = await LoadOracle();
            string room = project.Groups[0].GetAttribute("name")!;             // read the diacritic room name from the model

            ProjectEditor editor = project.Edit();
            // project3's only follow-links are the three FUGA→AND wires, so these two outputs are unlinked. Link
            // allocates exactly two halves; unlink must remove exactly those two — a structural inverse, N = 2.
            ResourceRef a = editor.Group(room).Product("Diode").Output("Lampe");
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
            byte[] original = TestData.ReadBytes(Oracle);
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

        // ---- T8: unlock then re-lock a loaded locked function block ----

        [Test]
        public async Task T8_FunctionBlockUnlockRelockCycle_IsByteIdentical()
        {
            byte[] original = TestData.ReadBytes(Oracle);
            Project project = await LoadOracle();
            ProjectElement locked = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") == "yes");
            string room = project.FindParent(locked.Id!.Value)!.GetAttribute("name")!;
            string name = locked.GetAttribute("name")!;

            ProjectEditor editor = project.Edit();
            editor.Group(room).FunctionBlock(name).Unlock().Locked();          // clear the flag, then re-set it

            byte[] actual = await Save(editor.ToProject());

            TestData.AssertBytesIdentical(original, actual, "T8 FB unlock/relock cycle");
        }

        // ---- T9: add an internal variable to a Tom blok, then delete it ----

        [Test]
        public async Task T9_AddResourceThenDelete_IsIdenticalExceptPinnedLastUniqueId()
        {
            byte[] original = TestData.ReadBytes(Oracle);
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
