using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FakeItEasy;

using Ihc.App;
using Ihc.Vis.Catalog;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The Import, Export and controller-bridge refusals, and the one row that does NOT refuse.
    ///
    /// <para><b>Three of the four rows already failed the operation</b> and gained a code and a Danish sentence,
    /// on the same terms as the load and save families: the operation carries the dotted head, the cause keeps
    /// its bare published id, and the English diagnostic is unchanged.</para>
    ///
    /// <para><b>The fourth, <c>import-catalog-wrong-kind</c>, succeeds today</b> — measured, not assumed. D13
    /// says a Fatal row whose condition currently succeeds keeps today's posture and is recorded rather than
    /// given a new refusal, so this fixture pins what actually happens: a <c>.ifb</c> read as a product yields a
    /// definition with an empty identifier and a <c>functionblock</c> body. That disagreement between catalogue
    /// and code is recorded by <c>CatalogCompletenessTests.KnownUnimplemented</c>, which carries the reason; this
    /// fixture is the half that executes it, written down where it can be seen changing.</para>
    ///
    /// <para><b>Two operations, not one bridge.</b> A download and an upload are separate heads because what a
    /// user does next differs completely — nothing to fetch, versus a controller whose own state is now
    /// uncertain — and one head would put those two behind the same filter.</para>
    /// </summary>
    [TestFixture]
    public sealed class ImportBridgeRefusalTests
    {
        private static ProblemCatalog Catalog => ProblemCatalog.Current;

        /// <summary>
        /// The shared catalog-file wrap, at its owner. Both the runtime import and the install-directory scan
        /// reach it, and both are the same act — taking a definition file in — so one identity serves both.
        /// </summary>
        [Test]
        public void AMalformedCatalogFileIsRefusedAsImportCatalogUnparsable()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ihc-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "broken.def");
            try
            {
                File.WriteAllText(path,
                    "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<product_dataline id=\"_0x1\" name=\"X\">");

                RefusedImportException refusal =
                    Assert.Throws<RefusedImportException>(() => CatalogReader.ReadProduct(path))!;

                AssertRefusal(refusal, ImportRefusalCodes.CatalogUnparsable, "broken.def");
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        /// <summary>
        /// An empty controller, at its owner. Naming it is what keeps the failure from becoming a
        /// NullReferenceException one frame later, which is a bug report rather than an actionable message.
        /// </summary>
        [Test]
        public void AnEmptyControllerIsRefusedAsImportControllerNoProject()
        {
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.GetProject()).Returns((ProjectFile?)null);

            RefusedOperationException refusal = Assert.ThrowsAsync<RefusedOperationException>(
                async () => await Fakes.BridgeService(controller).DownloadFrom())!;

            AssertRefusal(refusal, BridgeRefusalCodes.ControllerNoProject, "IsIHCProjectAvailable");
        }

        /// <summary>
        /// A declined store, at its owner, and STILL a <see cref="ProjectUploadException"/> — the type callers
        /// already catch for the one failure that leaves the controller's own state uncertain.
        /// </summary>
        [Test]
        public async Task ADeclinedStoreIsRefusedAsExportControllerDeclined()
        {
            var controller = A.Fake<IControllerService>();
            A.CallTo(() => controller.StoreProject(A<ProjectFile>._)).Returns(false);
            ProjectAppService app = Fakes.BridgeService(controller);
            Project project = await app.Load(TestData.PathOf("projects/Project1-SimpelWired.vis"));

            ProjectUploadException refusal = Assert.ThrowsAsync<ProjectUploadException>(
                async () => await app.UploadTo(project, ProjectSaveOptions.PreserveExistingMetadata))!;

            Assert.Multiple(() =>
            {
                Assert.That(refusal.Problems!.Operation.Code, Is.EqualTo(OperationCodes.BridgeUpload));
                Assert.That(refusal.Problems.Cause.Code, Is.EqualTo(BridgeRefusalCodes.ControllerDeclined.Cause));
                Assert.That(refusal.Problems.Cause.Message,
                    Is.EqualTo(BridgeRefusalCodes.ControllerDeclined.CauseLabel));
                Assert.That(refusal.Message, Does.Contain("StoreProject"));
            });
        }

        /// <summary>
        /// THE POSTURE THIS TASK DID NOT CHANGE, characterized so it cannot drift unnoticed. Reading a file as
        /// the wrong catalog kind succeeds and produces a degenerate definition: the identifying attribute is
        /// empty and the body carries the other kind's root tag. Nothing refuses, so there is no site to give an
        /// identity to — and inventing one here would be a new refusal this backlog explicitly does not make.
        /// </summary>
        [Test]
        public void ReadingAFileAsTheWrongCatalogKindStillSucceedsToday()
        {
            ProductDefinition blockAsProduct =
                CatalogReader.ReadProduct(TestData.PathOf("functionblocks/synthetic/synthetic_fb01_toggle.ifb"));
            FunctionBlockDefinition productAsBlock =
                CatalogReader.ReadFunctionBlock(TestData.PathOf("products/synthetic/synthetic_9f01_input.def"));

            Assert.Multiple(() =>
            {
                Assert.That(blockAsProduct.ProductIdentifier, Is.Empty, "a block carries no product_identifier");
                Assert.That(blockAsProduct.Body.Tag, Is.EqualTo("functionblock"),
                    "and the body is plainly the other kind");
                Assert.That(productAsBlock.MasterType, Is.Empty, "a product carries no master_type");
                Assert.That(productAsBlock.Body.Tag, Is.EqualTo("product_dataline"));

                Assert.That(Catalog.TryGet(new ProblemCode("import-catalog-wrong-kind"),
                    out ProblemCatalogEntry entry), Is.True, "the row exists and stays Active");
                Assert.That(entry.Status, Is.EqualTo(ProblemCodeStatus.Active));
                Assert.That(entry.MessageTemplate, Is.Empty,
                    "with no Danish sentence, because nothing raises it — an authored label would imply it does");
            });
        }

        /// <summary>
        /// Every cause keeps its BARE published id under a DOTTED head, and every head is a governed entry whose
        /// template is the words the site hands over. The same guard the load and save families carry, applied
        /// to this one rather than assumed from them.
        /// </summary>
        [Test]
        public void EveryCauseKeepsItsPublishedIdUnderAGovernedHead()
        {
            RefusalIdentity[] identities =
            [
                ImportRefusalCodes.CatalogUnparsable,
                BridgeRefusalCodes.ControllerNoProject,
                BridgeRefusalCodes.ControllerDeclined,
            ];

            Assert.Multiple(() =>
            {
                foreach (RefusalIdentity identity in identities)
                {
                    Assert.That(identity.Cause.Value, Does.Not.Contain("."), identity.Cause.Value);
                    Assert.That(identity.Operation.Value, Does.Contain("."), identity.Cause.Value);

                    Assert.That(Catalog.TryGet(identity.Cause, out ProblemCatalogEntry cause), Is.True,
                        identity.Cause.Value);
                    Assert.That(cause.MessageTemplate, Is.EqualTo(identity.CauseLabel), identity.Cause.Value);

                    Assert.That(Catalog.TryGet(identity.Operation, out ProblemCatalogEntry head), Is.True,
                        identity.Operation.Value);
                    Assert.That(head.MessageTemplate, Is.EqualTo(identity.OperationLabel), identity.Operation.Value);
                    Assert.That(head.Section, Is.EqualTo(ProblemCatalogSection.OperationOutcomes),
                        identity.Operation.Value);
                }
            });
        }

        /// <summary>
        /// The two directions are two operations. Folding them into one <c>bridge.*</c> head would put "there is
        /// nothing to fetch" and "the controller's state is now uncertain" behind the same filter, which are the
        /// two outcomes a user most needs told apart.
        /// </summary>
        [Test]
        public void DownloadAndUploadAreSeparateOperations()
        {
            Assert.Multiple(() =>
            {
                Assert.That(OperationCodes.BridgeDownload, Is.Not.EqualTo(OperationCodes.BridgeUpload));
                Assert.That(OperationCodes.BridgeDownload.Value, Is.EqualTo("bridge.download"));
                Assert.That(OperationCodes.BridgeUpload.Value, Is.EqualTo("bridge.upload"));
                Assert.That(OperationCodes.BridgeDownloadLabel, Is.Not.EqualTo(OperationCodes.BridgeUploadLabel));
            });
        }

        private static void AssertRefusal(
            IProblemCarrier refusal, RefusalIdentity identity, string diagnosticFragment)
        {
            Assert.Multiple(() =>
            {
                Assert.That(refusal.Problems, Is.Not.Null, "a refusal carries its operation and its cause");
                Assert.That(refusal.Problems!.Operation.Code, Is.EqualTo(identity.Operation));
                Assert.That(refusal.Problems.Cause.Code, Is.EqualTo(identity.Cause));
                Assert.That(refusal.Problems.Cause.Message, Is.EqualTo(identity.CauseLabel),
                    "the Danish sentence the user reads");
                Assert.That(((Exception)refusal).Message, Does.Contain(diagnosticFragment),
                    "and the English diagnostic is unchanged — it was joined, not replaced");
            });
        }
    }
}
