using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// M3 / 3.6 — intra-container reorder via <see cref="ProjectEditor.MoveSubtree"/>: the tool that lets the V4
    /// replay reproduce project2's creation-order ≠ document-order containers (e.g. condition "%P = %S" is created
    /// third but written first, and internalsettings "Flag" is created early but written last). Pins that a
    /// same-parent move reorders children while <b>preserving every id and allocating nothing</b> (spec ch. 02 §6.6);
    /// the add-then-delete counter burns the same replay needs are the R4 property already pinned by
    /// <c>AllocatorMonotonicityTests</c> (M2) via the shared <see cref="ProjectEditor.DeleteById"/>.
    /// </summary>
    public class SubtreeReorderTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> Load() => new ProjectAppService(Settings).Load("testdata/projects/Project1-SimpelWired.vis");

        private static ElementId FirstProgram(Project p) =>
            p.Root.Descendants().First(e => e.Tag == "program_simple").Id!.Value;

        private static (string Room, string Block) FirstBlock(Project p) =>
            p.Groups
                .Where(g => !g.Children.IsEmpty)
                .SelectMany(g => g.Children.Where(c => c.Tag == "functionblock").Select(c => (g.GetAttribute("name")!, c.GetAttribute("name")!)))
                .First();

        /// <summary>The authored sub-program's conditions container in <paramref name="p"/> (the last program_sub of the program).</summary>
        private static ProjectElement AuthoredConditions(Project p, ElementId programId) =>
            p.FindById(programId)!.FindChild("actions")!.Children.Last(c => c.Tag == "program_sub").FindChild("conditions")!;

        [Test]
        public async Task MoveSubtree_SameParentReorder_MovesChildToFront_PreservesIdsAndCounter()
        {
            Project project = await Load();
            ElementId progId = FirstProgram(project);
            (string room, string block) = FirstBlock(project);
            ProjectEditor editor = project.Edit();
            FunctionBlockRef fb = editor.Group(room).FunctionBlock(block);
            fb.Unlock("Test Installer", new DateOnly(2026, 1, 1));   // Project1's block ships library-locked; unlock before authoring (T003) — id-neutral
            ResourceRef p = fb.AddInput("__pgm_p");

            // Author three conditions in creation order (as the user clicks) — mirrors project2's 239/240/241.
            SubProgramRef sub = editor.Program(progId).AddSubProgram();
            sub.AddCondition("%P = OFF", p, method: "_0x14");
            sub.AddCondition("%P = ON", p, method: "_0xa");
            sub.AddCondition("%P = %S", p, method: "_0x1e", link2: p);

            // Ids are stable across snapshots, so peek to resolve them, then reorder on the still-live editor.
            Project before = editor.ToProject();
            ProjectElement condsBefore = AuthoredConditions(before, progId);
            System.Collections.Generic.List<ElementId> ids = condsBefore.Children.Select(c => c.Id!.Value).ToList();
            string highWater = before.LastUniqueId!;

            // The vendor writes "%P = %S" (created third, ids[2]) at the top — replay it as a same-parent move to index 0.
            editor.MoveSubtree(ids[2], condsBefore.Id!.Value, index: 0);
            Project after = editor.ToProject();

            ProjectElement condsAfter = AuthoredConditions(after, progId);
            Assert.Multiple(() =>
            {
                Assert.That(condsAfter.Children.Select(c => c.Id!.Value),
                    Is.EqualTo(new[] { ids[2], ids[0], ids[1] }), "moved child leads; the rest keep relative order");
                Assert.That(condsAfter.Children.Select(c => c.GetAttribute("name")),
                    Is.EqualTo(new[] { "%P = %S", "%P = OFF", "%P = ON" }), "document order now matches the oracle shape");
                Assert.That(condsAfter.Children.Count, Is.EqualTo(3), "a move never adds or drops a child");
                Assert.That(after.LastUniqueId, Is.EqualTo(highWater), "a move allocates nothing and reuses no id");
            });
        }
    }
}
