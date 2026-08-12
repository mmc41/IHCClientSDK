using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W3-8: the typed read views (PinView/ProductView/DimmerView) are the read-side peers of
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

        // ModemView_Name_ReadsThroughTheElementViewSurface (T018) is gone with the type it tested (T138).
        // It asserted that ModemView.Name delegated to the shared ElementView.Name surface rather than
        // re-typing GetAttribute("name") — a real invariant, and one the modem still enjoys: the composer
        // binds Navn to the root `name` and reads every field through project.View(...).Effective, so the
        // modem's own Navn is covered by the composer tests and by ProductAndPinView_ReadTheSameValuesAs-
        // RawAttributes above for the shared read surface. What is not kept is a test for a type nothing
        // else called; it was the last thing holding ModemView alive.

        // review H1: a loadable-but-quirky library block whose master_date_year is outside 1..9999 must read as
        // "no usable date" (the documented null contract) — not throw ArgumentOutOfRangeException out of
        // DateTime.DaysInMonth and crash the properties-dialog read. Project.Modified already guards the same range.
        [Test]
        public void FunctionBlockView_MasterDate_YearAbove9999_ReturnsNull_WithoutThrowing()
        {
            var fb = new ProjectElement("functionblock", new ElementId(0x600, 0x28),
                ImmutableArray.Create(("id", "_0x60028"), ("name", "FB"), ("master_type", "1.1.01"),
                    ("master_date_year", "10000"), ("master_date_month", "6"), ("master_date_day", "15")),
                ImmutableArray<ProjectElement>.Empty);
            var root = new ProjectElement("utcs_project", null,
                ImmutableArray.Create(("version_major", "4"), ("last_unique_id", "_0x600000")),
                ImmutableArray.Create(fb));

            var view = new FunctionBlockView(new Project(root), fb);

            Assert.That(() => view.MasterDate, Throws.Nothing, "an out-of-range year must not crash the read");
            Assert.That(view.MasterDate, Is.Null, "an unusable date reads as null");
        }
    }
}
