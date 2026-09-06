using System.Linq;
using Ihc.Vis.Session;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T003 — the ONE central engine-level locked-ancestor authorization (PG-2, US-020/US-026, review finding #2):
    /// every STRUCTURAL mutation whose target lies at/within a <c>locked="yes"</c> function block's subtree is refused
    /// <b>whoever drives the editor</b> — direct engine calls throw, and the session commands surface a clean
    /// <see cref="EditStatus.Refused"/> via <c>Evaluate</c>. Covered families: insert variable / program-row / pin,
    /// <see cref="ProjectEditor.ReorderSubtree"/>, and <see cref="ProjectEditor.MoveSubtree"/> /
    /// <see cref="ProjectEditor.CopySubtree"/> whose target parent is inside the locked subtree. The guard checks the
    /// mutation's TARGET, so relocating or copying the whole locked block to an unlocked locality — and every edit on
    /// an unlocked block — stays allowed (no over-reach). Oracle: the locked <c>AutoProof</c> block and the unlocked
    /// <c>Custom blok</c>, both under the <c>Stue</c> locality of <c>project2-CustomBlock.vis</c>.
    /// </summary>
    public class LockedBlockStructuralGuardTests
    {
        private const string Oracle = "project2-CustomBlock.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() => new ProjectAppService(Settings).Load("testdata/projects/" + Oracle);

        private static ProjectElement Fb(Project p, string name) =>
            p.Root.Descendants().First(e => e.Tag == "functionblock" && e.GetAttribute("name") == name);

        private static ElementId SectionId(Project p, string block, string section) =>
            Fb(p, block).FindChild(section)!.Id!.Value;

        // ---- engine level: a direct editor call is refused (throws) whoever drives it ----

        [Test]
        public async Task Engine_InsertVariable_IntoLockedBlockSection_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId autoProof = Fb(project, "AutoProof").Id!.Value;

            var ex = Assert.Throws<InvalidOperationException>(
                () => editor.FunctionBlock(autoProof).AddInternalVariable("resource_flag", "New"));
            Assert.That(ex!.Message, Does.Contain("låst funktionsblok"));
        }

        [Test]
        public async Task Engine_InsertProgramRow_IntoLockedBlock_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId actions = Fb(project, "AutoProof").Descendants().First(e => e.Tag == "actions").Id!.Value;

            Assert.Throws<InvalidOperationException>(() => editor.AllocateChild(actions, "action"));
        }

        [Test]
        public async Task Engine_ReorderNode_InsideLockedBlock_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId output = Fb(project, "AutoProof").FindChild("outputs")!.Children.First().Id!.Value;

            Assert.Throws<InvalidOperationException>(() => editor.ReorderSubtree(output, 0));
        }

        [Test]
        public async Task Engine_MoveNode_IntoLockedBlockSection_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId customBlok = Fb(project, "Custom blok").Id!.Value;
            ElementId lockedInputs = SectionId(project, "AutoProof", "inputs");

            Assert.Throws<InvalidOperationException>(() => editor.MoveSubtree(customBlok, lockedInputs));
        }

        [Test]
        public async Task Engine_CopyNode_IntoLockedBlockSection_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId customBlok = Fb(project, "Custom blok").Id!.Value;
            ElementId lockedInputs = SectionId(project, "AutoProof", "inputs");

            Assert.Throws<InvalidOperationException>(() => editor.CopySubtree(customBlok, lockedInputs));
        }

        // ---- session level: the command surfaces the refusal as a clean verdict (not a fault) ----

        [Test]
        public async Task Session_AddVariable_IntoLockedBlock_IsRefused()
        {
            Project project = await LoadOracle();
            var svc = new ProjectAppService(Settings);
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId lockedInputs = SectionId(project, "AutoProof", "inputs");

            AddVariable? command = svc.Commands.AddVariable(session.Current!, lockedInputs, "resource_input", "New");
            Assert.That(command, Is.Not.Null);
            EditOutcome outcome = session.Apply(command!);

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Does.Contain("låst funktionsblok"));
            });
        }

        [Test]
        public async Task Session_ReorderNode_InsideLockedBlock_IsRefused()
        {
            Project project = await LoadOracle();
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId output = Fb(project, "AutoProof").FindChild("outputs")!.Children.First().Id!.Value;

            EditOutcome outcome = session.Apply(new ReorderNode(output, 0));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }

        // ---- no over-reach: the guard is about the TARGET, not "anything touching a locked block" ----

        [Test]
        public async Task Engine_InsertVariable_IntoUnlockedBlock_IsAllowed()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId customBlok = Fb(project, "Custom blok").Id!.Value;

            Assert.DoesNotThrow(() => editor.FunctionBlock(customBlok).AddInput("New"));
        }

        [Test]
        public async Task Engine_MoveLockedBlock_ToAnUnlockedLocality_IsAllowed()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId autoProof = Fb(project, "AutoProof").Id!.Value;
            ElementId emptyLocality = project.Groups.Last().Id!.Value;   // an empty room, not locked

            Assert.DoesNotThrow(() => editor.MoveSubtree(autoProof, emptyLocality));
        }

        // review B1: the guard is target-only for INSERT/copy/move-INTO, but a node strictly INSIDE a locked block
        // must not be torn OUT of it either — that mutates the locked subtree, exactly as reorder/delete refuse. The
        // whole-block relocation above still works (the block has no locked ancestor), so this is not over-reach.
        [Test]
        public async Task Engine_MoveNode_OutOfLockedBlock_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId lockedPin = Fb(project, "AutoProof").FindChild("outputs")!.Children
                .First(c => c.Id is not null).Id!.Value;
            ElementId emptyLocality = project.Groups.Last().Id!.Value;

            var ex = Assert.Throws<InvalidOperationException>(() => editor.MoveSubtree(lockedPin, emptyLocality));
            Assert.That(ex!.Message, Does.Contain("låst funktionsblok"));
        }

        // review A1: the drag-over probe must not offer a reorder the command would refuse. Two same-tag siblings
        // inside a locked block are a reorderable PAIR by parent/tag, yet the locked-block gate forbids reordering
        // them — so CanReorderNode must agree with CanReorder (the menu gate) and the ReorderNode Apply: all false.
        [Test]
        public void Session_CanReorderNode_NodeInsideLockedBlock_AgreesWithMenuGate_BothFalse()
        {
            ProjectElement locked = Node("functionblock", "_0x5228", new[] { ("name", "Locked"), ("locked", "yes") },
                Node("outputs", "_0x5424", new[] { ("name", "Output") },
                    Node("resource_output", "_0x6312", new[] { ("name", "A") }),
                    Node("resource_output", "_0x6412", new[] { ("name", "B") })));
            ProjectElement root = Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("last_unique_id", "_0x7000") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "Stue") }, locked)));
            var session = new ProjectDocumentSession();
            session.Open(new Project(root));
            ElementId.TryParse("_0x6312", out ElementId a);
            ElementId.TryParse("_0x6412", out ElementId b);

            Assert.Multiple(() =>
            {
                Assert.That(session.CanReorderNode(a, b), Is.False, "the drag-over hint must not offer a locked-block reorder");
                Assert.That(session.CanReorder(a, 1), Is.False, "and it agrees with the menu gate");
            });
        }

        // ---- The two doors onto the locked-ancestor rule ----

        // The backdrop the two shapes below hang off: a well-formed root, a Localities container and one room.
        // Written once because what those tests are ABOUT is the branch under the room, and a root spelled out
        // per test is both a copy to keep in step and three lines between the reader and the subject.
        private static Project InStue(params ProjectElement[] contents) =>
            WithRoot(Node("groups", "_0x2031", new[] { ("name", "L") },
                Node("group", "_0x2132", new[] { ("name", "Stue") }, contents)));

        /// <summary>
        /// The gates ask T003 per pointer event, so they answer it by walking the snapshot's index UPWARD instead
        /// of running the engine's whole-tree <c>BuildPath</c>. The two must answer alike for every id and both
        /// senses of <c>inclusive</c>: a gate that diverged would offer an edit the engine then throws on, which
        /// reaches a caller as a Failed operation rather than the clean refusal the whole gate exists to give.
        /// The peer of <c>CatalogPinDeleteGuardTests.IndexDoor_AgreesWithTheEngineWalk_ForEveryId</c>, which pins
        /// the same equivalence for the ownership rule the locked clause sits inside.
        /// </summary>
        [Test]
        public async Task IndexWalk_AgreesWithTheEngineWalk_ForEveryId()
        {
            Project project = await LoadOracle();
            ProjectIndex index = project.ElementIndex;
            var ids = project.Root.DescendantsAndSelf()
                .Where(e => e.Id is not null).Select(e => e.Id!.Value).Distinct().ToList();

            Assert.Multiple(() =>
            {
                Assert.That(ids.Any(id => ProjectEditor.IsWithinLockedBlock(project.Root, id, inclusive: false)),
                    Is.True, "the oracle really does hold a locked block with something inside it");

                foreach (ElementId id in ids)
                {
                    foreach (bool inclusive in new[] { false, true })
                    {
                        Assert.That(index.IsWithinLockedBlock(id, inclusive),
                            Is.EqualTo(ProjectEditor.IsWithinLockedBlock(project.Root, id, inclusive)),
                            $"{id.ToToken()} inclusive:{inclusive}");
                    }
                }

                var absent = new ElementId(0x7FFFFF, 0x99);
                Assert.That(index.IsWithinLockedBlock(absent, inclusive: false), Is.False,
                    "an absent id is nobody's refusal to make, as on the engine's door");
            });
        }

        /// <summary>
        /// The upward walk steps by ELEMENT, so an id-less container between the locked block and its descendant
        /// does not end it.
        /// </summary>
        /// <remarks>
        /// An id-less element inside a project is an ordinary open-world shape — the engine admits element types a
        /// project's own DTD declares, the edit-open guard skips id-less elements rather than refusing them, and
        /// <c>ProjectChangeSet</c> rolls an id-less descendant's change up into its nearest id-bearing ancestor. A
        /// walk keyed on ids reports the ancestors above such a container as absent, which here means allowing an
        /// edit inside a locked block and then throwing from the engine.
        /// </remarks>
        [Test]
        public void BothDoors_RefuseThroughAnIdlessContainer()
        {
            Project project = InStue(
                Node("functionblock", "_0x5228", new[] { ("name", "Locked"), ("locked", "yes") },
                    Node("imported_container", null, [],
                        Node("resource_output", "_0x6312", new[] { ("name", "A") }))));
            ElementId.TryParse("_0x6312", out ElementId inside);

            Assert.Multiple(() =>
            {
                Assert.That(ProjectEditor.IsWithinLockedBlock(project.Root, inside, inclusive: false), Is.True,
                    "the engine's chain reaches the locked block through the id-less container");
                Assert.That(project.ElementIndex.IsWithinLockedBlock(inside, inclusive: false), Is.True,
                    "and so does the index walk the gates use");
                Assert.That(ProjectCommands.ClassifyDeletionRefusal(project, inside).Kind,
                    Is.EqualTo(ProjectEditor.ClassifyDeletionRefusal(project.Root, inside).Kind),
                    "so the delete gate classifies it exactly as the engine's door does");
            });
        }

        /// <summary>
        /// A duplicated id names two elements; both doors then have to be about the SAME one.
        /// </summary>
        /// <remarks>
        /// The tree below is the shape where an independently-filled parent map goes wrong: the first element
        /// carrying the id sits inside the locked block, while a later twin hangs directly off a shallower holder
        /// that the walk reaches first. Read as "the first element whose CHILDREN carry the id", the parent is the
        /// unlocked holder and the gate allows a delete the engine refuses. Read as "the parent of the element the
        /// id resolved to", it is the locked block's own <c>outputs</c>, and the two doors agree. A duplicated id
        /// is itself a validation finding, but the gates still have to be coherent on a project carrying one.
        /// </remarks>
        [Test]
        public void BothDoors_RefuseTheFirstCarrierOfADuplicatedId()
        {
            Project project = InStue(
                Node("holder", "_0x3000", new[] { ("name", "Holder") },
                    Node("functionblock", "_0x5228", new[] { ("name", "Locked"), ("locked", "yes") },
                        Node("outputs", "_0x5424", new[] { ("name", "Output") },
                            Node("resource_output", "_0x6312", new[] { ("name", "A") }))),
                    Node("resource_output", "_0x6312", new[] { ("name", "Twin") })));
            ElementId.TryParse("_0x6312", out ElementId duplicated);

            Assert.Multiple(() =>
            {
                Assert.That(project.FindParent(duplicated)?.Tag, Is.EqualTo("outputs"),
                    "non-vacuity: the id really does resolve into the locked branch, not to the shallower twin");
                Assert.That(ProjectEditor.IsWithinLockedBlock(project.Root, duplicated, inclusive: false), Is.True,
                    "the engine's chain runs through the locked block");
                Assert.That(project.ElementIndex.IsWithinLockedBlock(duplicated, inclusive: false), Is.True,
                    "and the index walk follows the same branch rather than the twin's");
                Assert.That(ProjectCommands.ClassifyDeletionRefusal(project, duplicated).Kind,
                    Is.EqualTo(ProjectEditor.ClassifyDeletionRefusal(project.Root, duplicated).Kind),
                    "so both delete doors name the same rule");
            });
        }
    }
}
