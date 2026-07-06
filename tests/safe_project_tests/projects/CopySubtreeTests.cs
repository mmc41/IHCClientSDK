using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// BL-4 — id-addressed clipboard clone (<see cref="ProjectEditor.CopySubtree"/> / <see cref="GroupRef.PasteInto"/>).
    /// Wraps the catalog-insert clone engine in an <see cref="ElementId"/>-based entry point: deep-copy a live
    /// in-project subtree with fresh ids (type-code suffix preserved), remapped internal IDREFs and shared enums,
    /// applying a <see cref="LinkCopyPolicy"/> to follow-link halves whose reciprocal partner lies outside the copy.
    /// <c>Project1-SimpelWired.vis</c> wires an FB to products in the same room, so a copied FB has both internal
    /// links (remapped) and external ones (policy-governed).
    /// </summary>
    public class CopySubtreeTests
    {
        private const string Oracle = "Project1-SimpelWired.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/" + Oracle);

        private static HashSet<ElementId> Ids(ProjectElement subtree)
        {
            var ids = new HashSet<ElementId>();
            foreach (ProjectElement e in new[] { subtree }.Concat(subtree.Descendants()))
            {
                if (e.Id is { } id)
                {
                    ids.Add(id);
                }
            }
            return ids;
        }

        private static IReadOnlyList<ElementId> LinkPartners(ProjectElement subtree) =>
            new[] { subtree }.Concat(subtree.Descendants())
                .Where(e => e.Tag is "link_from_resource" or "link_to_resource")
                .Select(e => ElementId.TryParse(e.GetAttribute("link"), out ElementId p) ? (ElementId?)p : null)
                .Where(p => p is not null).Select(p => p!.Value).ToList();

        private static Dictionary<string, int> NonLinkTagCounts(ProjectElement subtree)
        {
            var counts = new Dictionary<string, int>();
            foreach (ProjectElement e in new[] { subtree }.Concat(subtree.Descendants()))
            {
                if (e.Tag is "link_from_resource" or "link_to_resource")
                {
                    continue;
                }
                counts[e.Tag] = counts.GetValueOrDefault(e.Tag) + 1;
            }
            return counts;
        }

        // The first function block and a group other than the one that owns it.
        private static (ProjectElement fb, ElementId targetGroupId) FbAndOtherGroup(Project project)
        {
            ProjectElement fb = project.Root.Descendants().First(e => e.Tag == "functionblock");
            ElementId ownerGroupId = project.FindParent(fb.Id!.Value)!.Id!.Value;
            ElementId target = project.Groups.First(g => g.Id!.Value != ownerGroupId).Id!.Value;
            return (fb, target);
        }

        [Test]
        public async Task CopySubtree_FunctionBlock_FreshDisjointIds_SuffixPreserved_ValidatorClean()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();
            (ProjectElement fb, ElementId targetGroupId) = FbAndOtherGroup(project);
            HashSet<ElementId> sourceIds = Ids(fb);

            ElementId copyId = editor.CopySubtree(fb.Id!.Value, targetGroupId);
            Project after = editor.ToProject();
            ProjectElement copy = after.FindById(copyId)!;

            Assert.Multiple(() =>
            {
                Assert.That(copyId.TypeCode, Is.EqualTo(fb.Id!.Value.TypeCode), "root keeps its type-code suffix");
                Assert.That(copyId, Is.Not.EqualTo(fb.Id!.Value), "the copy root gets a fresh id");
                Assert.That(Ids(copy).Overlaps(sourceIds), Is.False, "every copied id is fresh (disjoint from source)");
                Assert.That(after.FindById(fb.Id!.Value), Is.Not.Null, "the source subtree is untouched");
                ProjectValidationResult v = app.Validate(after);
                Assert.That(v.IsValid, Is.True, "the clone is internally consistent: " + string.Join(" | ", v.Errors));
            });
        }

        [Test]
        public async Task CopySubtree_IsDeepStructuralTwin_OfSource()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            (ProjectElement fb, ElementId targetGroupId) = FbAndOtherGroup(project);

            ElementId copyId = editor.CopySubtree(fb.Id!.Value, targetGroupId);
            ProjectElement copy = editor.ToProject().FindById(copyId)!;

            Assert.That(NonLinkTagCounts(copy), Is.EqualTo(NonLinkTagCounts(fb)),
                "every non-link element of the source is deep-copied");
        }

        [Test]
        public async Task CopySubtree_DropExternal_NoLinkHalfPointsOutsideTheCopy()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            (ProjectElement fb, ElementId targetGroupId) = FbAndOtherGroup(project);
            Assert.That(LinkPartners(fb), Is.Not.Empty, "the source FB is wired (has links)");

            ElementId copyId = editor.CopySubtree(fb.Id!.Value, targetGroupId, LinkCopyPolicy.DropExternal);
            ProjectElement copy = editor.ToProject().FindById(copyId)!;

            HashSet<ElementId> copyIds = Ids(copy);
            Assert.That(LinkPartners(copy), Is.All.Matches<ElementId>(copyIds.Contains),
                "DropExternal leaves only links whose partner is inside the copy");
        }

        [Test]
        public async Task CopySubtree_ThenDeleteSource_CopyRemainsValid()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();
            (ProjectElement fb, ElementId targetGroupId) = FbAndOtherGroup(project);

            ElementId copyId = editor.CopySubtree(fb.Id!.Value, targetGroupId);
            editor.DeleteById(fb.Id!.Value);          // remove the original entirely
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(after.FindById(copyId), Is.Not.Null, "the copy survives deletion of the source");
                ProjectValidationResult v = app.Validate(after);
                Assert.That(v.IsValid, Is.True,
                    "the copy references none of the source's ids (internal IDREFs fully remapped): "
                    + string.Join(" | ", v.Errors));
            });
        }

        [Test]
        public async Task CopySubtree_KeepExternal_RetainsOutwardHalves_ValidatorFlagsThem()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();
            (ProjectElement fb, ElementId targetGroupId) = FbAndOtherGroup(project);

            ElementId copyId = editor.CopySubtree(fb.Id!.Value, targetGroupId, LinkCopyPolicy.KeepExternal);
            Project after = editor.ToProject();
            ProjectElement copy = after.FindById(copyId)!;

            HashSet<ElementId> copyIds = Ids(copy);
            Assert.Multiple(() =>
            {
                Assert.That(LinkPartners(copy).Any(p => !copyIds.Contains(p)), Is.True,
                    "KeepExternal retains link halves pointing at the source's partners");
                Assert.That(app.Validate(after).IsValid, Is.False,
                    "those one-way halves are not reciprocal — the caller must resolve them");
            });
        }

        [Test]
        public async Task CopySubtree_DoesNotDuplicateSharedEnums()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            (ProjectElement fb, ElementId targetGroupId) = FbAndOtherGroup(project);
            int before = project.Child("enum_definitions")!.Children.Length;

            editor.CopySubtree(fb.Id!.Value, targetGroupId);

            int after = editor.ToProject().Child("enum_definitions")!.Children.Length;
            Assert.That(after, Is.EqualTo(before), "an in-project copy references shared enums, it does not duplicate them");
        }

        [Test]
        public async Task PasteInto_ClonesUnderTargetGroup_ReturnsHandle()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();
            (ProjectElement fb, ElementId targetGroupId) = FbAndOtherGroup(project);
            editor.TryResolve(targetGroupId, out ElementRef? target);

            ElementRef pasted = editor.Group(target!.GetAttribute("name")!).PasteInto(fb.Id!.Value);
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(pasted.Tag, Is.EqualTo("functionblock"));
                Assert.That(after.FindParent(pasted.Id)!.Id, Is.EqualTo(targetGroupId), "the copy lands under the target room");
                Assert.That(app.Validate(after).IsValid, Is.True);
            });
        }
    }
}
