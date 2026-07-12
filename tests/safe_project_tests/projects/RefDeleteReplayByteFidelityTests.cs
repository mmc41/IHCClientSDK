namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The reference-cascade delete byte-fidelity gate (M-B, US-009's program-reference half) for
    /// <see cref="ProjectEditor.DeleteById(Ihc.Vis.Model.ElementId, DeleteReferencePolicy)"/> against the authentic
    /// vendor oracle <c>project2-CustomBlock-refdelete.vis</c> (IHC Visual 03.04.72.03 after one recorded delete on
    /// <c>project2-CustomBlock.vis</c>, single save). The SDK loads the original, reproduces Action 0
    /// (<see cref="ProjectEditor.NormalizeCatalogEnums"/>), deletes the FB output <c>Udgang _0x7112</c> under
    /// <see cref="DeleteReferencePolicy.CascadeReferences"/>, restamps to the oracle's clock and asserts
    /// byte-identity. Pinned vendor semantics (ENG2-A5, §18 M-B = <b>A, row-only</b>): the resource definition and
    /// all four referencing <c>action</c> rows go <b>whole</b> on any link-slot match — <c>_0xf6ca</c> goes even
    /// though its other slot (<c>link2="_0x7011"</c>) references a live resource, and that partner <c>_0x7011</c>
    /// survives with its remaining refs intact — parent Kommandoer containers stay (emptied ones re-serialize
    /// self-closed), the
    /// delete allocates nothing (<c>last_unique_id</c> unchanged), and no confirmation is involved (GUI concern).
    /// Catalog-free.
    /// </summary>
    public class RefDeleteReplayByteFidelityTests
    {
        private const string Original = "project2-CustomBlock.vis";
        private const string RefDeleteOracle = "project2-CustomBlock-refdelete.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        // ---- Full replay: Action 0 → cascade delete → byte-identity ----

        [Test]
        public async Task CascadeDeleteOutput_ReplaysCustomBlockRefDeleteOracle_ByteIdentical() =>
            // id2 _0xc0d0414 decodes to day 12 / hour 13 / min 4 / sec 20; <modified> is minute-precision (13:04).
            await ReplayOracle.AssertReplaysByteIdentical(Original, RefDeleteOracle,
                new DateTimeOffset(2026, 7, 12, 13, 4, 20, TimeSpan.Zero),
                editor => editor.DeleteById(
                    editor.Group("Stue").FunctionBlock("Custom blok").Output("Udgang").Id!.Value,
                    DeleteReferencePolicy.CascadeReferences));

        // ---- Cascade-set exactness: exactly the vendor's five ids go, nothing else, nothing allocated ----

        [Test]
        public async Task CascadeDelete_RemovesExactlyTheReferencingRows_ParentsSurvive()
        {
            Project original = await ReplayOracle.LoadProject(Original);

            ProjectEditor editor = original.Edit();
            FunctionBlockRef custom = editor.Group("Stue").FunctionBlock("Custom blok");
            ResourceRef udgang = custom.Output("Udgang");
            Assert.That(udgang.Id!.Value.ToToken(), Is.EqualTo("_0x7112"), "the pinned oracle endpoint");
            Project before = editor.ToProject();
            var beforeIds = before.Root.DescendantsAndSelf().Where(e => e.Id is not null)
                .Select(e => e.Id!.Value.ToToken()).ToHashSet();
            int indgangRefsBefore = CountLinkSlotRefs(before, "_0x7011");

            editor.DeleteById(udgang.Id!.Value, DeleteReferencePolicy.CascadeReferences);
            Project after = editor.ToProject();

            var afterIds = after.Root.DescendantsAndSelf().Where(e => e.Id is not null)
                .Select(e => e.Id!.Value.ToToken()).ToHashSet();
            var removed = beforeIds.Except(afterIds).OrderBy(t => t, StringComparer.Ordinal).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(removed, Is.EquivalentTo(
                    new[] { "_0x7112", "_0xedca", "_0xf5ca", "_0xf6ca", "_0xf7ca" }),
                    "exactly the resource + its four referencing action rows — nothing else (ENG2-A5 freed set)");
                foreach (string container in new[] { "_0xeb66", "_0xec66", "_0xe766", "_0xe866" })
                {
                    Assert.That(afterIds, Does.Contain(container),
                        $"emptied parent container {container} survives (row-only cascade, §18-A)");
                }
                Assert.That(afterIds, Does.Contain("_0x7011"), "the any-slot partner Indgang survives");
                Assert.That(CountLinkSlotRefs(after, "_0x7011"), Is.EqualTo(indgangRefsBefore - 1),
                    "Indgang loses only the ref carried by the removed row (_0xf6ca's link2)");
                Assert.That(after.LastUniqueId, Is.EqualTo(before.LastUniqueId), "a delete allocates nothing");
            });
            Assert.That(new ProjectAppService(Settings).Validate(after).IsValid, Is.True,
                "no dangling reference remains after the cascade");
        }

        // ---- Strict regression: the default policy still refuses and mutates nothing ----

        [Test]
        public async Task StrictDelete_OfReferencedResource_StillThrowsAndLeavesProjectUntouched()
        {
            byte[] original = TestData.ReadBytes("projects/" + Original);
            var app = new ProjectAppService(Settings);
            Project project = await ReplayOracle.LoadProject(Original);

            ProjectEditor editor = project.Edit();
            ResourceRef udgang = editor.Group("Stue").FunctionBlock("Custom blok").Output("Udgang");
            var refused = Assert.Throws<InvalidOperationException>(
                () => editor.DeleteById(udgang.Id!.Value));

            Assert.That(refused!.Message, Does.Contain("dangling references"), "the strict guard names the reason");
            using var ms = new MemoryStream();
            await app.Save(editor.ToProject(), ms, ProjectSaveOptions.PreserveExistingMetadata);
            TestData.AssertBytesIdentical(original, ms.ToArray(), "a refused strict delete mutates nothing");
        }

        private static int CountLinkSlotRefs(Project project, string idToken) =>
            project.Root.Descendants().Count(e =>
                e.GetAttribute("link1") == idToken || e.GetAttribute("link2") == idToken);
    }
}
