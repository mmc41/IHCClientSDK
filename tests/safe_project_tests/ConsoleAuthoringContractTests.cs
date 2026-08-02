using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// R1 acceptance (T010 / review finding F30): the authoring door works <b>without any GUI</b>. This drives the
    /// full frontend-independent flow through the public SDK surface only — <see cref="ProjectAppService"/>, its
    /// <see cref="ProjectCommands"/> gateway and the public <see cref="IProjectDocument"/> port — with NO
    /// <c>ProjectWorkflow</c>, no Avalonia and no view-model in sight: load a project, obtain commands through the
    /// one door (a context-free locality insert AND a catalog-bearing product insert), apply them through the
    /// session, validate, then save and reload to prove the edits round-trip. A console/service frontend authors
    /// exactly this way.
    /// </summary>
    public class ConsoleAuthoringContractTests
    {
        [Test]
        public async Task AuthoringDoor_Load_Command_Apply_Validate_Save_WithoutAnyGui()
        {
            var app = new ProjectAppService(TestSetup.Settings);
            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");

            // A stateful console runner uses the same public document port as any other interactive frontend.
            IProjectDocument document = app.OpenDocument(project);

            // 1. A context-free command obtained through the one door, then applied.
            EditOutcome<ElementId> localityOutcome = document.Apply(app.Commands.AddLocality(document.Current!, "Console Room"));
            Assert.That(localityOutcome.Status, Is.EqualTo(EditStatus.Committed), "the gateway locality command commits");
            ElementId localityId = localityOutcome.Value;

            // 2. A catalog-bearing command obtained through the door — proving it resolves the embedded catalog
            //    headlessly (no IHC Visual install, no GUI) — inserted into the just-authored locality.
            Ihc.Vis.Products.ProductDefinition productDef = app.GetAvailableProducts().First();
            AddProduct? productCommand = app.Commands.AddProduct(document.Current!, localityId, productDef.ProductIdentifier);
            Assert.That(productCommand, Is.Not.Null, "the door resolved the catalog product");
            EditOutcome<ElementId> productOutcome = document.Apply(productCommand!);
            Assert.That(productOutcome.Status, Is.EqualTo(EditStatus.Committed), "the gateway product command commits");

            Project edited = document.Current!;

            // 3. Validate the authored project through the door.
            ProjectValidationResult validation = app.Validate(edited);
            Assert.That(validation.IsValid, Is.True,
                "the authored project is valid: " + string.Join(" | ", validation.Errors));

            // 4. Save through the door and reload — the edits survive the byte round-trip.
            string path = Path.Combine(Path.GetTempPath(), $"ihc-console-contract-{Guid.NewGuid():N}.vis");
            try
            {
                await app.Save(edited, path);
                Project reloaded = await app.Load(path);
                Assert.Multiple(() =>
                {
                    Assert.That(reloaded.FindById(localityId)?.GetAttribute("name"), Is.EqualTo("Console Room"),
                        "the gateway-authored locality survives save+reload");
                    Assert.That(reloaded.FindById(productOutcome.Value), Is.Not.Null,
                        "the gateway-authored product survives save+reload");
                });
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
