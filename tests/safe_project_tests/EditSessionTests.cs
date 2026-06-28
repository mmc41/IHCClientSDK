using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Phase-3 edit-session gates: building from scratch through the mutable session validates clean and re-loads
    /// structurally equal (gate c); loading→editing→re-saving preserves every existing <c>_0x</c> id (gate d, no
    /// install dir needed); and opening+committing a created project with no edits reproduces it (gate e). The
    /// catalog-backed gates skip gracefully without an IHC Visual install.
    /// </summary>
    public class EditSessionTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static ICatalog RequireCatalog()
        {
            string dir = Settings.IhcVisualInstallDir;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Assert.Ignore($"No IHC Visual install dir configured ('{dir}'); skipping install-dir-gated test.");
            }
            return CatalogDiscovery.FromInstallDir(dir);
        }

        private static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 6, 27, 16, 5, 51, TimeSpan.Zero));

        private static HashSet<string> Ids(ProjectElement element)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            void Walk(ProjectElement e)
            {
                if (e.GetAttribute("id") is { } id)
                {
                    ids.Add(id);
                }
                if (!e.Children.IsDefaultOrEmpty)
                {
                    foreach (ProjectElement c in e.Children)
                    {
                        Walk(c);
                    }
                }
            }
            Walk(element);
            return ids;
        }

        // ----- gate (d): load -> edit -> resave preserves every existing id (no install dir) -----

        [Test]
        public async Task LoadEditResave_NoEdits_PreservesProjectExactly()
        {
            var app = new ProjectAppService(Settings);
            Project project = await app.Load("testdata/Project1.vis");

            Project committed = project.Edit().ToProject();

            Assert.That(committed, Is.EqualTo(project), "opening and committing with no edits is a no-op");
        }

        [Test]
        public async Task LoadEditResave_AddLink_PreservesEveryExistingId_AndAddsTwoHalves()
        {
            var app = new ProjectAppService(Settings);
            Project project = await app.Load("testdata/Project1.vis");
            HashSet<string> before = Ids(project.Root);

            ProjectEditor editor = project.Edit();
            GroupRef stue = editor.Group("Stue");
            FunctionBlockRef kip = stue.FunctionBlock("1.1.01.e. Kip tænd sluk");
            editor.Link(kip.Output("ON puls"), kip.Input("Kip med timer"));   // two existing, unlinked resources
            Project edited = editor.ToProject();

            HashSet<string> after = Ids(edited.Root);
            Assert.Multiple(() =>
            {
                Assert.That(before.IsSubsetOf(after), Is.True, "every existing id is preserved");
                Assert.That(after.Count, Is.EqualTo(before.Count + 2), "exactly two new link-half ids added");
                Assert.That(app.Validate(edited).IsValid, Is.True, "the new link keeps the project valid");
            });
        }

        // ----- gate (e): authored empty project equals the CreateNew output -----

        [Test]
        public void CreateNew_OpenAndCommitWithNoEdits_EqualsCreateNewOutput()
        {
            ICatalog catalog = RequireCatalog();
            var details = new ProjectDetails("P", "I", "DK");

            Project created = new ProjectAppService(Settings, catalog, Clock()).CreateNew(details);
            Project reopened = new ProjectAppService(Settings, catalog, Clock()).CreateNew(details).Edit().ToProject();

            Assert.That(reopened, Is.EqualTo(created));
        }

        // ----- gate (c): build from scratch -> validate clean -> reload structurally equal -----

        [Test]
        public async Task BuildFromScratch_AddProductFunctionBlockAndLink_ValidatesAndRoundTrips()
        {
            ICatalog catalog = RequireCatalog();
            var app = new ProjectAppService(Settings, catalog, Clock());

            Project created = app.CreateNew(new ProjectDetails("Morten Christensen", "Morten", "Danmark"));
            ProjectEditor editor = created.Edit();
            GroupRef stue = editor.Group("Stue");

            ProductRef fuga = stue.AddProduct(catalog.Product("_0x2101")).Name("LK FUGA Tryk 2 tast").Locked().EnduserReport();
            FunctionBlockRef kip = stue.AddFunctionBlock(catalog.FunctionBlock("1.1.01")).Locked();
            editor.Link(fuga.Input("Tryk (venstre)"), kip.Input("Kip"));

            Project built = editor.ToProject();

            ProjectValidationResult validation = app.Validate(built);
            Assert.That(validation.IsValid, Is.True, "errors: " + string.Join(" | ", validation.Errors));

            using var ms = new MemoryStream();
            await app.Save(built, ms, ProjectSaveOptions.PreserveExistingMetadata);
            Project reloaded = await app.Load(new MemoryStream(ms.ToArray()));

            Assert.That(reloaded, Is.EqualTo(built), "authored project re-loads structurally equal");
        }

        [Test]
        public void BuildFromScratch_AddedComponentsGetFreshIdsWithCorrectSuffixes()
        {
            ICatalog catalog = RequireCatalog();
            var app = new ProjectAppService(Settings, catalog, Clock());

            Project created = app.CreateNew(new ProjectDetails("P", "I", "DK"));
            ProjectEditor editor = created.Edit();
            GroupRef stue = editor.Group("Stue");
            ProductRef fuga = stue.AddProduct(catalog.Product("_0x2101"));
            Project built = editor.ToProject();

            ProjectElement stueGroup = built.Groups.First(g => g.GetAttribute("name") == "Stue");
            ProjectElement product = stueGroup.FindChild("product_dataline")!;

            Assert.Multiple(() =>
            {
                Assert.That(ElementId.TryParse(product.GetAttribute("id"), out ElementId pid) && pid.TypeCode == 0x53,
                    Is.True, "product_dataline keeps suffix 0x53");
                Assert.That(product.GetAttribute("product_identifier"), Is.EqualTo("_0x2101"));
                Assert.That(app.Validate(built).IsValid, Is.True);
            });
        }
    }
}
