using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W3-8: the typed read views (PinView/ProductView/ModemView/DimmerView) are the read-side peers of
    /// the write Ref handles. Characterization: each field must equal the raw attribute the dialog assembly used to
    /// read, so moving the attribute-name literals SDK-side is behavior-preserving (the documentation/cable/address
    /// attributes are #IMPLIED, so the effective read equals the old GetAttribute).
    /// </summary>
    public class DialogReadViewsTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        [Test]
        public async Task ProductAndPinView_ReadTheSameValuesAsRawAttributes()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement product = project.Root.DescendantsAndSelf()
                .First(e => ProductClassifier.IsProduct(e.Tag)
                    && e.DescendantsAndSelf().Any(c => c.Tag is "dataline_input" or "dataline_output"));
            var pv = new ProductView(project, product);

            Assert.Multiple(() =>
            {
                Assert.That(pv.Name, Is.EqualTo(product.GetAttribute("name")));
                Assert.That(pv.Note, Is.EqualTo(product.GetAttribute("note")));
                Assert.That(pv.CableType, Is.EqualTo(product.GetAttribute("cabletype")));
                Assert.That(pv.CableNumber, Is.EqualTo(product.GetAttribute("cablenumber")));
                Assert.That(pv.DocumentationTag, Is.EqualTo(product.GetAttribute("documentation_tag")));
                Assert.That(pv.PowerGroup, Is.EqualTo(product.GetAttribute("power_group")));
                Assert.That(pv.ProductIdentifier, Is.EqualTo(product.GetAttribute("product_identifier")));
                Assert.That(pv.IsWireless, Is.EqualTo(ProductClassifier.IsWireless(product.Tag)));
                Assert.That(pv.Terminals.Count(), Is.EqualTo(
                    product.DescendantsAndSelf().Count(c => c.Tag is "dataline_input" or "dataline_output")),
                    "one PinView per data-line terminal");
            });

            ProjectElement pin = product.DescendantsAndSelf().First(c => c.Tag is "dataline_input" or "dataline_output");
            var pinView = new PinView(project, pin);
            Assert.Multiple(() =>
            {
                Assert.That(pinView.Name, Is.EqualTo(pin.GetAttribute("name")));
                Assert.That(pinView.CableColour, Is.EqualTo(pin.GetAttribute("cable_colour")));
                Assert.That(pinView.Note, Is.EqualTo(pin.GetAttribute("note")));
                Assert.That(pinView.AddressToken, Is.EqualTo(pin.GetAttribute("address_dataline")));
                Assert.That(pinView.IsOutput, Is.EqualTo(pin.Tag == "dataline_output"));
                Assert.That(pinView.Id, Is.EqualTo(pin.Id));
            });
        }

        // T018: ModemView.Name reads through the shared ElementView.Name surface (project.View(element).Name),
        // not a re-typed raw GetAttribute("name"). For a named element (as every real modem is) the effective
        // read equals the element's own name.
        [Test]
        public void ModemView_Name_ReadsThroughTheElementViewSurface()
        {
            var modem = new ProjectElement("sms_modem_settings", new ElementId(0x57d, 0xcb),
                ImmutableArray.Create(("id", "_0x57dcb"), ("name", "Telephone numbers #1-#10")),
                ImmutableArray<ProjectElement>.Empty);
            var root = new ProjectElement("utcs_project", null,
                ImmutableArray.Create(("version_major", "4"), ("last_unique_id", "_0x600000")),
                ImmutableArray.Create(modem));
            var project = new Project(root);

            var view = new ModemView(project, modem);
            Assert.Multiple(() =>
            {
                Assert.That(view.Name, Is.EqualTo("Telephone numbers #1-#10"));
                Assert.That(view.Name, Is.EqualTo(project.View(modem).Name),
                    "ModemView.Name delegates to the shared ElementView.Name surface");
            });
        }
    }
}
