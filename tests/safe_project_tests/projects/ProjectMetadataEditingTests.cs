using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// G4 (US-039) — authoring the three id-less root metadata blocks (<c>project_info</c> /
    /// <c>customer_info</c> / <c>installer_info</c>) through <see cref="ProjectEditor.SetProjectInfo"/> /
    /// <see cref="ProjectEditor.SetCustomerInfo"/> / <see cref="ProjectEditor.SetInstallerInfo"/>, reading them
    /// back through the typed <see cref="Project"/> getters, and populating them at creation through the
    /// expanded <see cref="ProjectDetails"/>. Field vocabulary and semantics (upsert — only configured fields
    /// written; blank ⇒ attribute omitted; <c>udf</c> never written) are pinned by the A1 oracle
    /// (<c>project3-KompleksWired-projektinfo.vis</c>, see testdataoverview.md); the byte-level replay lands in
    /// <c>ProjectInfoReplayByteFidelityTests</c> (G4b).
    /// </summary>
    public class ProjectMetadataEditingTests
    {
        private const string Oracle = "project3-KompleksWired.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/projects/" + Oracle);

        [Test]
        public async Task Getters_ReadSeedMetadata()
        {
            Project project = await LoadOracle();

            Assert.Multiple(() =>
            {
                Assert.That(project.Programmer, Is.EqualTo("Morten Christensen"));
                Assert.That(project.InstallerName, Is.EqualTo("Morten"));
                Assert.That(project.InstallerCountry, Is.EqualTo("Danmark"));
                Assert.That(project.CustomerName, Is.Null, "customer_info is empty in the seed");
                Assert.That(project.ProjectNumber, Is.Null);
                Assert.That(project.Drawing, Is.Null);
                Assert.That(project.ProjectType, Is.Null);
                Assert.That(project.Description, Is.Null);
                Assert.That(project.InstallerEmail, Is.Null);
                Assert.That(project.CustomerMobilePhone, Is.Null);
            });
        }

        [Test]
        public async Task SetProjectInfo_WritesOnlyConfiguredFields()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();

            editor.SetProjectInfo(p => p.Number("G4-num").Drawing("G4-draw"));
            Project committed = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(committed.ProjectNumber, Is.EqualTo("G4-num"));
                Assert.That(committed.Drawing, Is.EqualTo("G4-draw"));
                Assert.That(committed.Programmer, Is.EqualTo("Morten Christensen"), "unconfigured field untouched");
                Assert.That(committed.ProjectType, Is.Null);
                Assert.That(committed.Description, Is.Null);
            });
        }

        [Test]
        public async Task SetAllThreeBlocks_FullFieldSet_ReadsBackThroughGetters()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();

            editor.SetProjectInfo(p => p.Programmer("G4-prog").Number("G4-num").Drawing("G4-draw")
                                        .Type("G4-type").Description("G4-desc"))
                  .SetCustomerInfo(c => c.Name("G4-c-name").Address("G4-c-addr").City("G4-c-city")
                                         .ZipCode("G4-c-zip").Country("G4-c-country").Phone("G4-c-phone")
                                         .MobilePhone("G4-c-mobile").Email("G4-c-email"))
                  .SetInstallerInfo(i => i.Name("G4-i-name").Address("G4-i-addr").City("G4-i-city")
                                          .ZipCode("G4-i-zip").Country("G4-i-country").Phone("G4-i-phone")
                                          .MobilePhone("G4-i-mobile").Email("G4-i-email"));
            Project committed = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(committed.Programmer, Is.EqualTo("G4-prog"));
                Assert.That(committed.ProjectNumber, Is.EqualTo("G4-num"));
                Assert.That(committed.Drawing, Is.EqualTo("G4-draw"));
                Assert.That(committed.ProjectType, Is.EqualTo("G4-type"));
                Assert.That(committed.Description, Is.EqualTo("G4-desc"));

                Assert.That(committed.CustomerName, Is.EqualTo("G4-c-name"));
                Assert.That(committed.CustomerAddress, Is.EqualTo("G4-c-addr"));
                Assert.That(committed.CustomerCity, Is.EqualTo("G4-c-city"));
                Assert.That(committed.CustomerZipCode, Is.EqualTo("G4-c-zip"));
                Assert.That(committed.CustomerCountry, Is.EqualTo("G4-c-country"));
                Assert.That(committed.CustomerPhone, Is.EqualTo("G4-c-phone"));
                Assert.That(committed.CustomerMobilePhone, Is.EqualTo("G4-c-mobile"));
                Assert.That(committed.CustomerEmail, Is.EqualTo("G4-c-email"));

                Assert.That(committed.InstallerName, Is.EqualTo("G4-i-name"));
                Assert.That(committed.InstallerAddress, Is.EqualTo("G4-i-addr"));
                Assert.That(committed.InstallerCity, Is.EqualTo("G4-i-city"));
                Assert.That(committed.InstallerZipCode, Is.EqualTo("G4-i-zip"));
                Assert.That(committed.InstallerCountry, Is.EqualTo("G4-i-country"));
                Assert.That(committed.InstallerPhone, Is.EqualTo("G4-i-phone"));
                Assert.That(committed.InstallerMobilePhone, Is.EqualTo("G4-i-mobile"));
                Assert.That(committed.InstallerEmail, Is.EqualTo("G4-i-email"));
            });
        }

        [Test]
        public async Task SetInstallerInfo_BlankClearsField()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();

            editor.SetInstallerInfo(i => i.Country(""));
            Project committed = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(committed.InstallerCountry, Is.Null, "blank ⇒ attribute omitted (A1 pinned semantics)");
                Assert.That(committed.InstallerName, Is.EqualTo("Morten"), "unconfigured sibling field untouched");
            });
        }

        [Test]
        public void CreateNew_ExpandedDetails_PopulatesAllMetadataBlocks()
        {
            var app = new ProjectAppService(Settings);

            Project project = app.CreateNew(new ProjectDetails("G4-prog", "G4-i-name", "G4-i-country")
            {
                ProjectNumber = "G4-num",
                Drawing = "G4-draw",
                ProjectType = "G4-type",
                Description = "G4-desc",
                InstallerAddress = "G4-i-addr",
                InstallerCity = "G4-i-city",
                InstallerZipCode = "G4-i-zip",
                InstallerPhone = "G4-i-phone",
                InstallerMobilePhone = "G4-i-mobile",
                InstallerEmail = "G4-i-email",
                CustomerName = "G4-c-name",
                CustomerAddress = "G4-c-addr",
                CustomerCity = "G4-c-city",
                CustomerZipCode = "G4-c-zip",
                CustomerCountry = "G4-c-country",
                CustomerPhone = "G4-c-phone",
                CustomerMobilePhone = "G4-c-mobile",
                CustomerEmail = "G4-c-email",
            });

            Assert.Multiple(() =>
            {
                Assert.That(project.Programmer, Is.EqualTo("G4-prog"));
                Assert.That(project.ProjectNumber, Is.EqualTo("G4-num"));
                Assert.That(project.Drawing, Is.EqualTo("G4-draw"));
                Assert.That(project.ProjectType, Is.EqualTo("G4-type"));
                Assert.That(project.Description, Is.EqualTo("G4-desc"));

                Assert.That(project.InstallerName, Is.EqualTo("G4-i-name"));
                Assert.That(project.InstallerAddress, Is.EqualTo("G4-i-addr"));
                Assert.That(project.InstallerCity, Is.EqualTo("G4-i-city"));
                Assert.That(project.InstallerZipCode, Is.EqualTo("G4-i-zip"));
                Assert.That(project.InstallerCountry, Is.EqualTo("G4-i-country"));
                Assert.That(project.InstallerPhone, Is.EqualTo("G4-i-phone"));
                Assert.That(project.InstallerMobilePhone, Is.EqualTo("G4-i-mobile"));
                Assert.That(project.InstallerEmail, Is.EqualTo("G4-i-email"));

                Assert.That(project.CustomerName, Is.EqualTo("G4-c-name"));
                Assert.That(project.CustomerAddress, Is.EqualTo("G4-c-addr"));
                Assert.That(project.CustomerCity, Is.EqualTo("G4-c-city"));
                Assert.That(project.CustomerZipCode, Is.EqualTo("G4-c-zip"));
                Assert.That(project.CustomerCountry, Is.EqualTo("G4-c-country"));
                Assert.That(project.CustomerPhone, Is.EqualTo("G4-c-phone"));
                Assert.That(project.CustomerMobilePhone, Is.EqualTo("G4-c-mobile"));
                Assert.That(project.CustomerEmail, Is.EqualTo("G4-c-email"));
            });
        }

        // Byte-level known answer for the expanded-details write path: exact-line matches pin the A1 semantics —
        // attributes in DTD-declared order (never fill/property order), unset fields OMITTED (never =""), and
        // escaping & → &amp;, " → &quot;, æøå → raw Latin-1 (readable again after Latin-1 decode).
        [Test]
        public async Task CreateNew_MetadataSerializesInDtdOrder_EscapedAndOmitted()
        {
            var app = new ProjectAppService(Settings);
            Project project = app.CreateNew(new ProjectDetails("prog-æøå", "instNavn", "Danmark")
            {
                Drawing = "dr&aw",
                Description = "de\"sc",           // ProjectNumber / ProjectType left unset ⇒ omitted
                InstallerCity = "instBy",         // remaining installer fields unset ⇒ omitted
                CustomerName = "kundeNavn",
                CustomerPhone = "kundeTlf",       // remaining customer fields unset ⇒ omitted
            });

            using var ms = new MemoryStream();
            await app.Save(project, ms, ProjectSaveOptions.PreserveExistingMetadata);
            string text = Encoding.Latin1.GetString(ms.ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain(
                    "   <customer_info name=\"kundeNavn\" phone=\"kundeTlf\"/>"));
                Assert.That(text, Does.Contain(
                    "   <installer_info name=\"instNavn\" city=\"instBy\" country=\"Danmark\"/>"));
                Assert.That(text, Does.Contain(
                    "   <project_info programmer=\"prog-æøå\" drawing=\"dr&amp;aw\" description=\"de&quot;sc\"/>"));
            });
        }
    }
}
