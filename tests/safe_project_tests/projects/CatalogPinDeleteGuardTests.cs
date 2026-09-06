using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// ADR-002: the SDK is the authority on what may NOT be deleted as a direct target — a
    /// product's catalog-declared pin (a <c>resource_</c>/<c>dataline_</c>/<c>airlink_</c> child of a product device
    /// root) and any node inside a LOCKED function block are owned by the catalog/library, not the installer. All
    /// three delete surfaces enforce it: the engine (<see cref="ProjectEditor.DeleteById(ElementId, DeleteReferencePolicy)"/>)
    /// throws, the <see cref="DeleteNode"/> command is Refused, and <c>PreviewDelete</c> reports not-deletable —
    /// while a subtree delete that removes them as part of a product or block still works (the guard only inspects
    /// the direct target). Oracle <c>Project1-SimpelWired.vis</c> holds wired products (with catalog pins) and a
    /// locked library block ("Kip", <c>locked="yes"</c>) with an internal program. The equivalence test below takes
    /// the larger <c>project3-KompleksWired.vis</c> instead, because agreement over every id is only worth asserting
    /// on a tree that reaches both refusal clauses many times over.
    /// </summary>
    public class CatalogPinDeleteGuardTests : SessionCommandFixture
    {
        private const string Oracle = "Project1-SimpelWired.vis";
        private static Task<Project> LoadOracle() => App.Load("testdata/projects/" + Oracle);

        private static bool IsPinTag(string tag) =>
            tag.StartsWith("resource_", StringComparison.Ordinal)
            || tag.StartsWith("dataline_", StringComparison.Ordinal)
            || tag.StartsWith("airlink_", StringComparison.Ordinal);

        // A product device root that owns at least one catalog-declared pin, and that first pin. Internal rather
        // than private because SessionIndexPinTests measures the delete GATE over the same two owned nodes, and a
        // second copy of the pick is how the two fixtures would come to measure different nodes.
        internal static (ProjectElement Product, ProjectElement Pin) PickProductAndCatalogPin(Project project)
        {
            ProjectElement product = project.Root.Descendants()
                .First(e => ProductClassifier.IsProduct(e.Tag) && e.Children.Any(c => IsPinTag(c.Tag)));
            return (product, product.Children.First(c => IsPinTag(c.Tag)));
        }

        // A program node (an action row) that lives inside a locked (library) function block.
        internal static ProjectElement PickNodeInsideLockedBlock(Project project)
        {
            ProjectElement lockedBlock = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") == "yes");
            return lockedBlock.Descendants().First(e => e.Tag == "action");
        }

        // ---- Engine (ProjectEditor.DeleteById): the authoritative guard ----

        [Test]
        public async Task Engine_DeleteById_DirectCatalogPin_IsRefused()
        {
            Project project = await LoadOracle();
            (_, ProjectElement pin) = PickProductAndCatalogPin(project);

            InvalidOperationException? refused =
                Assert.Throws<InvalidOperationException>(() => project.Edit().DeleteById(pin.Id!.Value));

            Assert.That(refused!.Message, Does.Contain("katalogdefineret").IgnoreCase,
                "the engine names why a catalog-declared pin cannot be deleted on its own");
        }

        [Test]
        public async Task Engine_DeleteById_NodeInsideLockedBlock_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectElement inner = PickNodeInsideLockedBlock(project);

            InvalidOperationException? refused =
                Assert.Throws<InvalidOperationException>(() => project.Edit().DeleteById(inner.Id!.Value));

            Assert.That(refused!.Message, Does.Contain("låst").IgnoreCase,
                "the engine names why a node inside a locked block cannot be deleted");
        }

        [Test]
        public async Task Engine_DeleteById_WholeProduct_StillRemovesItsCatalogPins()
        {
            Project project = await LoadOracle();
            (ProjectElement product, ProjectElement pin) = PickProductAndCatalogPin(project);

            // Deleting the product (the direct target) is allowed and cascades to remove its pins — the guard only
            // inspects the direct target, so subtree removal is untouched (the same behaviour DeleteCascadeTests proves).
            Project after = project.Edit().DeleteById(product.Id!.Value).ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(after.FindById(product.Id!.Value), Is.Null, "the product is gone");
                Assert.That(after.FindById(pin.Id!.Value), Is.Null, "its catalog pin went with it (subtree delete)");
            });
        }

        // ---- Command layer (DeleteNode.Evaluate via the session) ----

        [Test]
        public async Task Command_DeleteNode_CatalogPin_IsRefused()
        {
            Project project = await LoadOracle();
            (_, ProjectElement pin) = PickProductAndCatalogPin(project);

            EditOutcome outcome = Session(project).Apply(new DeleteNode(pin.Id!.Value, Cascade: false));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused),
                    "the DeleteNode command refuses a catalog pin before it ever reaches the engine");
                Assert.That(outcome.Code, Is.EqualTo(EditRefusalCodes.DeletionRefusedCatalogPin),
                    "and names WHICH rule refused (D5), so a caller filters on the code rather than the sentence");
            });
        }

        [Test]
        public async Task Command_DeleteNode_LockedBlockNode_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectElement inner = PickNodeInsideLockedBlock(project);

            EditOutcome outcome = Session(project).Apply(new DeleteNode(inner.Id!.Value, Cascade: false));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused),
                    "the DeleteNode command refuses a locked-block node");
                Assert.That(outcome.Code, Is.EqualTo(EditRefusalCodes.DeletionRefusedLockedBlock),
                    "and names the locked-block rule rather than the pin one");
            });
        }

        // ---- The two doors onto the ownership rule ----

        /// <summary>
        /// The menu gate classifies deletability on every selection change, so it resolves its three inputs — the
        /// target, its parent, its locked ancestors — off the snapshot's index rather than walking the tree the way
        /// the engine's door does. The two lookups must classify every id alike: a gate that diverged would hide a
        /// delete the engine performs, or offer one it refuses. The engine's whole-tree walk is the second opinion
        /// worth having precisely because <c>BuildPath</c> shares no lookup with the index.
        /// </summary>
        [Test]
        public async Task IndexDoor_AgreesWithTheEngineWalk_ForEveryId()
        {
            // The richer oracle: both refusal clauses have to be exercised for an agreement test to mean anything.
            Project project = await Load("project3-KompleksWired.vis");
            var byWalk = project.Root.DescendantsAndSelf()
                .Where(e => e.Id is not null)
                .Select(e => e.Id!.Value)
                .Distinct()
                .ToDictionary(id => id, id => ProjectEditor.ClassifyDeletionRefusal(project.Root, id));

            Assert.Multiple(() =>
            {
                var kinds = byWalk.Values.Select(r => r.Kind).ToList();
                Assert.That(kinds, Does.Contain(DeletionRefusalKind.CatalogPin),
                    "the oracle exercises the catalog-pin clause");
                Assert.That(kinds, Does.Contain(DeletionRefusalKind.LockedBlock),
                    "the oracle exercises the locked-block clause");

                foreach ((ElementId id, DeletionRefusal expected) in byWalk)
                {
                    Assert.That(ProjectCommands.ClassifyDeletionRefusal(project, id), Is.EqualTo(expected),
                        id.ToToken());
                }

                Assert.That(ProjectCommands.ClassifyDeletionRefusal(project, new ElementId(0x7FFFFF, 0x99)),
                    Is.EqualTo(DeletionRefusal.Allowed),
                    "an absent id is nobody's refusal to make, as on the engine's door");
            });
        }

        // ---- Preview (ProjectCommands.PreviewDelete) ----

        [Test]
        public async Task PreviewDelete_CatalogPinAndLockedBlockNode_AreNotDeletable()
        {
            Project project = await LoadOracle();
            ProjectAppService app = App;
            (_, ProjectElement pin) = PickProductAndCatalogPin(project);
            ProjectElement inner = PickNodeInsideLockedBlock(project);

            Assert.Multiple(() =>
            {
                DeleteImpact pinImpact = app.Commands.PreviewDelete(project, pin.Id!.Value);
                Assert.That(pinImpact.Deletable, Is.False, "a catalog pin is not offered for delete");
                Assert.That(pinImpact.Kind, Is.EqualTo(DeleteKind.NotDeletable));
                Assert.That(pinImpact.Refusal!.Code, Is.EqualTo(EditRefusalCodes.DeletionRefusedCatalogPin),
                    "the dialog names the same rule the menu gate does");

                DeleteImpact innerImpact = app.Commands.PreviewDelete(project, inner.Id!.Value);
                Assert.That(innerImpact.Deletable, Is.False, "a locked-block node is not offered for delete");
                Assert.That(innerImpact.Kind, Is.EqualTo(DeleteKind.NotDeletable));
                Assert.That(innerImpact.Refusal!.Code, Is.EqualTo(EditRefusalCodes.DeletionRefusedLockedBlock),
                    "and the locked-block rule where that is the one refusing");
            });
        }
    }
}
