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
            new ProjectAppService(Settings).Load("testdata/projects/" + Oracle);

        // review B2: copying a BARE external reciprocal half (its partner outside the copy) must report "nothing to
        // paste" via PasteInto's guard — not silently paste a one-way link. DropExternal now drops the copy root
        // itself, so CopySubtree yields an unresolvable id and PasteInto raises the (previously dead) guard.
        [Test]
        public async Task PasteInto_BareExternalReciprocalHalf_IsRejected_NotPastedOneWay()
        {
            Project project = await LoadOracle();
            ProjectElement half = project.Root.Descendants()
                .First(e => e.Tag is "link_from_resource" or "link_to_resource" && e.Id is not null);
            ElementId loc = project.Groups.First().Id!.Value;
            ProjectEditor editor = project.Edit();

            Assert.That(() => editor.Group(loc).PasteInto(half.Id!.Value),
                Throws.InvalidOperationException.With.Message.Contains("bare reciprocal half"));
        }

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

        // Review Low: copying a node into its own descendant is refused, exactly as MoveSubtree already does —
        // otherwise the clone would nest inside a copy of itself, building an invalid tree.
        [Test]
        public async Task CopySubtree_IntoOwnDescendant_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ProjectElement fb = project.Root.Descendants().First(e => e.Tag == "functionblock");
            ElementId sourceId = fb.Id!.Value;
            ElementId descendantId = fb.Descendants().First(d => d.Id is not null).Id!.Value;

            Assert.Throws<System.InvalidOperationException>(() => editor.CopySubtree(sourceId, descendantId),
                "a copy into the source's own descendant is refused, like MoveSubtree");
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

        // ----- scene-half prune policy (G1c): DropExternal covers scene rows, not just follow-link halves -----
        //
        // SDK-defined policy (labeled provisional until a vendor parity capture of a scene-membered copy exists):
        // a scene member row (scene_relay/scene_dimmer/scene_shutter) or scene_link whose reciprocal partner lies
        // OUTSIDE the copied subtree is dropped BEFORE the clone allocates ids — same rule and same no-phantom-burn
        // guarantee as follow-link halves. Scene pairs on project3-KompleksWired are authored in-session via
        // LinkScene (the committed corpus has no scene rows).

        [Test]
        public async Task CopySubtree_DropExternal_PrunesSceneMemberRow_NoPhantomBurn()
        {
            Project project = await LoadProject3();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();

            // Copy Lampeudtag before any scene membership exists → the copy's baseline allocation count.
            long baseline = Counter(editor.ToProject().LastUniqueId!);
            ElementRef plainCopy = editor.Group("Garage").PasteInto(Id("_0x5453"));
            long afterPlain = Counter(editor.ToProject().LastUniqueId!);
            long plainAllocations = afterPlain - baseline;

            // Wire the scene membership (+2 ids), then copy the now-membered product elsewhere.
            LinkRelayScene(editor);
            ElementRef memberedCopy = editor.Group("Soveværelse").PasteInto(Id("_0x5453"));
            long afterMembered = Counter(editor.ToProject().LastUniqueId!);
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(afterMembered - afterPlain - 2, Is.EqualTo(plainAllocations),
                    "the dropped member row consumes no id — pruned before the clone allocates (no phantom burn)");
                Assert.That(memberedCopy.Element.Descendants().Any(e => IsSceneHalf(e.Tag)), Is.False,
                    "the member's partner scene_link lies outside the copy, so the member row is dropped");
                Assert.That(memberedCopy.Element.Children.Any(c => c.Tag == "scenes"), Is.True,
                    "only the member row is pruned — the scenes container itself copies");
                Assert.That(after.Root.Descendants().Count(e => e.Tag == "scene_relay"), Is.EqualTo(1),
                    "the source's member row is untouched");
                Assert.That(after.Root.Descendants().Count(e => e.Tag == "scene_link"), Is.EqualTo(1),
                    "the source's scene_link is untouched");
                Assert.That(plainCopy.Element.Children.Any(c => c.Tag == "scenes"), Is.True,
                    "baseline copy sanity: same shape modulo the membership");
                Assert.That(app.Validate(after).IsValid, Is.True,
                    "a pruned copy leaves no one-sided scene half behind");
            });
        }

        [Test]
        public async Task CopySubtree_DropExternal_PrunesSceneLinkOffCopiedFb()
        {
            Project project = await LoadProject3();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();
            LinkRelayScene(editor);

            ElementRef copy = editor.Group("Garage").PasteInto(Id("_0x8b28"));   // the Kip block carrying the scene_link
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(copy.Element.Descendants().Any(e => e.Tag == "scene_link"), Is.False,
                    "the scene_link's partner member lies outside the copy, so it is dropped");
                Assert.That(copy.Element.Descendants().Count(e => e.Tag == "resource_scene"), Is.EqualTo(2),
                    "the scene pins themselves copy — only the wired half is pruned");
                Assert.That(after.Root.Descendants().Count(e => e.Tag == "scene_link"), Is.EqualTo(1),
                    "the source's scene_link is untouched");
                Assert.That(app.Validate(after).IsValid, Is.True);
            });
        }

        [Test]
        public async Task CopySubtree_WholeRoom_KeepsInternalScenePair_Remapped()
        {
            Project project = await LoadProject3();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();
            LinkRelayScene(editor);
            string sourceMemberToken = editor.ToProject().Root.Descendants()
                .Single(e => e.Tag == "scene_relay").GetAttribute("id")!;

            // Both halves live inside the copied room (member on Lampeudtag, scene_link on the Kip block).
            ElementId roomCopyId = editor.CopySubtree(Id("_0x2132"), Id("_0x2031"));
            Project after = editor.ToProject();
            ProjectElement roomCopy = after.FindById(roomCopyId)!;

            ProjectElement member = roomCopy.Descendants().Single(e => e.Tag == "scene_relay");
            ProjectElement sceneLink = roomCopy.Descendants().Single(e => e.Tag == "scene_link");
            Assert.Multiple(() =>
            {
                Assert.That(member.GetAttribute("link"), Is.EqualTo(sceneLink.GetAttribute("id")),
                    "an internal pair survives the copy, remapped member -> scene_link");
                Assert.That(sceneLink.GetAttribute("link"), Is.EqualTo(member.GetAttribute("id")),
                    "…and scene_link -> member");
                Assert.That(member.GetAttribute("id"), Is.Not.EqualTo(sourceMemberToken),
                    "the copy's member is a fresh id, not the source row");
                Assert.That(member.GetAttribute("relay_value"), Is.EqualTo("on"), "the member's value copies");
                Assert.That(app.Validate(after).IsValid, Is.True);
            });
        }

        // ----- scene-policy helpers (project3 endpoints pinned by the -scenelinks oracle work, G1b) -----

        private static Task<Project> LoadProject3() =>
            new ProjectAppService(Settings).Load("testdata/projects/project3-KompleksWired.vis");

        /// <summary>Wires the relay membership: Kip pin "Scenarie Sluk" (_0x974a) ↔ Lampeudtag "Scenarier" (_0x5649).</summary>
        private static void LinkRelayScene(ProjectEditor editor)
        {
            GroupRef room = editor.Group("Stue & Køkken \"åben\"");
            ResourceRef pin = room.FunctionBlock("1.1.01.e. Kip tænd sluk").SceneOutput("Scenarie Sluk");
            editor.LinkScene(pin, room.Product("Lampeudtag").Scenes(), SceneValue.Relay(on: true));
        }

        private static bool IsSceneHalf(string tag) =>
            tag is "scene_relay" or "scene_dimmer" or "scene_shutter" or "scene_link";

        private static long Counter(string token) =>
            long.Parse(token.AsSpan(3), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture);

        private static ElementId Id(string token) =>
            ElementId.TryParse(token, out ElementId id) ? id : throw new System.ArgumentException($"Bad id token: {token}");
    }
}
