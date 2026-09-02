using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// BL-2 — generic, schema-validated attribute editing through the id-addressed <see cref="ElementRef"/> handle:
    /// the backend of the F2 "Egenskaber" properties panel (rename, re-address, metadata, icon). Reads any
    /// attribute (<see cref="ElementRef.GetAttribute"/>), writes a declared one honoring omit-if-default and
    /// enum-range (<see cref="ElementRef.SetAttribute"/>), and enumerates an element's editable attributes for a
    /// property grid (<see cref="ElementRef.EditableAttributes"/>). Fidelity is pinned against
    /// <c>Project1-SimpelWired.vis</c> (widest attribute variety among the small oracles).
    /// </summary>
    public class AttributeEditingTests
    {
        private const string Oracle = "Project1-SimpelWired.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/projects/" + Oracle);

        private static ElementRef FirstProduct(ProjectEditor editor, Project project)
        {
            ElementId id = project.Root.Descendants().First(e => e.Tag == "product_dataline").Id!.Value;
            editor.TryResolve(id, out ElementRef? handle);
            return handle!;
        }

        [Test]
        public async Task GetAttribute_ReadsAnyAttribute_NullWhenAbsent()
        {
            Project project = await LoadOracle();
            ElementRef product = FirstProduct(project.Edit(), project);

            Assert.Multiple(() =>
            {
                Assert.That(product.GetAttribute("name"), Is.EqualTo("LK FUGA Tryk 2 tast"));
                Assert.That(product.GetAttribute("locked"), Is.EqualTo("yes"));
                Assert.That(product.GetAttribute("no_such_attribute"), Is.Null);
            });
        }

        [Test]
        public async Task SetAttribute_ChangesValue_AndChainsHandle()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementRef product = FirstProduct(editor, project);

            ElementRef returned = product.SetAttribute("note", "revised note");

            Assert.That(returned, Is.SameAs(product), "SetAttribute returns the same handle for chaining");
            Assert.That(editor.ToProject().FindById(product.Id)!.GetAttribute("note"), Is.EqualTo("revised note"));
        }

        [Test]
        public async Task SetAttribute_ToDeclaredDefault_IsOmittedOnCommit()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementRef product = FirstProduct(editor, project);

            product.SetAttribute("note", "temporary");
            Assert.That(editor.ToProject().FindById(product.Id)!.GetAttribute("note"), Is.EqualTo("temporary"),
                "a non-default value is present");

            product.SetAttribute("note", "");   // "" is the declared default of note
            Assert.That(editor.ToProject().FindById(product.Id)!.GetAttribute("note"), Is.Null,
                "omit-if-default: a value equal to the DTD default is dropped on serialize");
        }

        [Test]
        public async Task SetAttribute_Icon_AssignsIcon()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId groupId = project.Groups.First().Id!.Value;
            editor.TryResolve(groupId, out ElementRef? group);

            group!.SetAttribute("icon", "_0x22");

            Assert.That(editor.ToProject().FindById(groupId)!.GetAttribute("icon"), Is.EqualTo("_0x22"));
        }

        [Test]
        public async Task SetAttribute_OutsideEnumRange_Throws()
        {
            Project project = await LoadOracle();
            ElementRef product = FirstProduct(project.Edit(), project);

            Assert.Throws<ArgumentException>(() => product.SetAttribute("locked", "maybe"),
                "locked is enumerated (yes | no)");
            Assert.DoesNotThrow(() => product.SetAttribute("locked", "no"));
        }

        [Test]
        public async Task SetAttribute_UndeclaredAttribute_Throws()
        {
            Project project = await LoadOracle();
            ElementRef product = FirstProduct(project.Edit(), project);

            Assert.Throws<ArgumentException>(() => product.SetAttribute("bogus_attr", "x"));
        }

        [Test]
        public async Task SetAttribute_ElementId_Throws()
        {
            Project project = await LoadOracle();
            ElementRef product = FirstProduct(project.Edit(), project);

            Assert.Throws<ArgumentException>(() => product.SetAttribute("id", "_0x999"),
                "the element id is identity, not an editable property");
        }

        [Test]
        public async Task EditableAttributes_ProjectsSchema_ExcludesId()
        {
            Project project = await LoadOracle();
            ElementRef product = FirstProduct(project.Edit(), project);

            IReadOnlyList<AttrInfo> attrs = product.EditableAttributes();
            IReadOnlyList<string> names = attrs.Select(a => a.Name).ToList();
            AttrInfo locked = attrs.First(a => a.Name == "locked");
            AttrInfo pid = attrs.First(a => a.Name == "product_identifier");

            Assert.Multiple(() =>
            {
                Assert.That(names, Does.Not.Contain("id"), "the element id is not an editable property");
                Assert.That(names, Does.Contain("name"));
                Assert.That(names, Does.Contain("note"));
                Assert.That(names, Does.Contain("position"));

                Assert.That(locked.Kind, Is.EqualTo(AttrRequirement.Defaulted));
                Assert.That(locked.Default, Is.EqualTo("no"));
                Assert.That(locked.AllowedValues, Is.EqualTo(new[] { "yes", "no" }));

                Assert.That(pid.Kind, Is.EqualTo(AttrRequirement.Required), "product_identifier is #REQUIRED");
                Assert.That(pid.Default, Is.Null);
                Assert.That(pid.AllowedValues, Is.Empty);
            });
        }

        // A-22/US-068: toggling a "Log …" row's log mark flips its Logning state off the "Off" value and round-trips.
        [Test]
        public async Task LogMark_RoundTrips()
        {
            var app = new ProjectAppService(Settings);
            Project project = app.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
            ProductDefinition sensor = app.GetAvailableProducts()
                .First(p => p.DisplayName.Contains("Temperatur sensor med logning"));
            string room = project.Groups.First().GetAttribute("name")!;

            ProjectEditor editor = project.Edit();
            editor.Group(room).AddProduct(sensor);
            ProjectElement logRow = editor.ToProject().Root.DescendantsAndSelf()
                .First(e => e.Tag == "resource_enum" && (e.GetAttribute("name") ?? string.Empty).StartsWith("Log", StringComparison.Ordinal));
            ElementId logId = logRow.Id!.Value;
            string offToken = logRow.GetAttribute("inivalue")!;

            editor.ToggleLogMark(logId);
            string markedToken = editor.ToProject().FindById(logId)!.GetAttribute("inivalue")!;

            using var ms = new MemoryStream();
            await app.Save(editor.ToProject(), ms);
            ms.Position = 0;
            Project reloaded = await app.Load(ms);

            Assert.Multiple(() =>
            {
                Assert.That(markedToken, Is.Not.EqualTo(offToken), "toggling moves the log state off Off");
                Assert.That(reloaded.FindById(logId)!.GetAttribute("inivalue"), Is.EqualTo(markedToken),
                    "the log mark round-trips through save/reload");
            });
        }

        // A-23/US-012: the product's end-user-report flag survives a save/reload round-trip.
        [Test]
        public async Task EndUserReport_RoundTrips()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementRef product = FirstProduct(editor, project);
            ElementId pid = product.Id;

            product.SetAttribute("enduser_report", "yes");

            var app = new ProjectAppService(Settings);
            using var ms = new MemoryStream();
            await app.Save(editor.ToProject(), ms);
            ms.Position = 0;
            Project reloaded = await app.Load(ms);

            Assert.That(reloaded.FindById(pid)!.GetAttribute("enduser_report"), Is.EqualTo("yes"),
                "the end-user-report flag round-trips through save/reload");
        }

        [Test]
        public async Task SetAttribute_SetThenRestore_RoundTripsByteIdentical()
        {
            byte[] original = TestData.ReadBytes("projects/" + Oracle);
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementRef product = FirstProduct(editor, project);

            string name = product.GetAttribute("name")!;
            product.SetAttribute("name", name + " (temp)").SetAttribute("name", name);

            var app = new ProjectAppService(Settings);
            using var ms = new MemoryStream();
            await app.Save(editor.ToProject(), ms, ProjectSaveOptions.PreserveExistingMetadata);

            TestData.AssertBytesIdentical(original, ms.ToArray(), "BL-2 set-then-restore round-trip");
        }
    }
}
