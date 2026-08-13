using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// review3 H1 / ADR-002 (D09): the SDK is the authority on what may NOT be deleted as a direct target — a
    /// product's catalog-declared pin (a <c>resource_</c>/<c>dataline_</c>/<c>airlink_</c> child of a product device
    /// root) and any node inside a LOCKED function block are owned by the catalog/library, not the installer. All
    /// three delete surfaces enforce it: the engine (<see cref="ProjectEditor.DeleteById(ElementId, DeleteReferencePolicy)"/>)
    /// throws, the <see cref="DeleteNode"/> command is Refused, and <c>PreviewDelete</c> reports not-deletable —
    /// while a subtree delete that removes them as part of a product or block still works (the guard only inspects
    /// the direct target). Oracle <c>Project1-SimpelWired.vis</c> holds wired products (with catalog pins) and a
    /// locked library block ("Kip", <c>locked="yes"</c>) with an internal program.
    /// </summary>
    public class CatalogPinDeleteGuardTests
    {
        private const string Oracle = "Project1-SimpelWired.vis";
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> LoadOracle() => App.Load("testdata/projects/" + Oracle);

        private static bool IsPinTag(string tag) =>
            tag.StartsWith("resource_", StringComparison.Ordinal)
            || tag.StartsWith("dataline_", StringComparison.Ordinal)
            || tag.StartsWith("airlink_", StringComparison.Ordinal);

        // A product device root that owns at least one catalog-declared pin, and that first pin.
        private static (ProjectElement Product, ProjectElement Pin) PickProductAndCatalogPin(Project project)
        {
            ProjectElement product = project.Root.Descendants()
                .First(e => ProductClassifier.IsProduct(e.Tag) && e.Children.Any(c => IsPinTag(c.Tag)));
            return (product, product.Children.First(c => IsPinTag(c.Tag)));
        }

        // A program node (an action row) that lives inside a locked (library) function block.
        private static ProjectElement PickNodeInsideLockedBlock(Project project)
        {
            ProjectElement lockedBlock = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") == "yes");
            return lockedBlock.Descendants().First(e => e.Tag == "action");
        }

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
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

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused),
                "the DeleteNode command refuses a catalog pin before it ever reaches the engine");
        }

        [Test]
        public async Task Command_DeleteNode_LockedBlockNode_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectElement inner = PickNodeInsideLockedBlock(project);

            EditOutcome outcome = Session(project).Apply(new DeleteNode(inner.Id!.Value, Cascade: false));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused),
                "the DeleteNode command refuses a locked-block node");
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

                DeleteImpact innerImpact = app.Commands.PreviewDelete(project, inner.Id!.Value);
                Assert.That(innerImpact.Deletable, Is.False, "a locked-block node is not offered for delete");
                Assert.That(innerImpact.Kind, Is.EqualTo(DeleteKind.NotDeletable));
            });
        }
    }
}
