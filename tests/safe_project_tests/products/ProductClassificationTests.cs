using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// US-012/013/014: the shared SDK product classifier. The known catalog families classify exactly; the
    /// open-world predicates route undocumented tags for the UI (airlink-before-modem precedence); and — the
    /// Finding-5 correction, as amended by RL-2c/G7 — an open-world tag the predicates recognise is
    /// documented by the installation report, yet never inherits a known family's fields or its closed
    /// special-products section.
    /// </summary>
    public class ProductClassificationTests
    {
        // ----- Classify: the known catalog families (exact) -----

        [TestCase("product_dataline", ProductFamily.Dataline)]
        [TestCase("product_airlink", ProductFamily.Airlink)]
        [TestCase("product_rs485_led_dimmer", ProductFamily.Rs485LedDimmer)]
        [TestCase("product_rs485_modem", ProductFamily.Rs485Modem)]
        [TestCase("product_rs485_sms_modem", ProductFamily.Rs485SmsModem)]
        public void Classify_KnownFamilies_MatchExactly(string tag, ProductFamily expected) =>
            Assert.That(ProductClassifier.Classify(tag), Is.EqualTo(expected));

        // T043: `product_rs485_modem` (the non-SMS RS485 modem) is an INTENTIONAL open-world tag — it has no attested
        // vendor type code, so it is deliberately absent from the TypeCode registry (inventing a byte could collide),
        // YET the classifier and the report still recognise it (as an RS485 modem / a modem). This pins that contract.
        [Test]
        public void ProductRs485Modem_HasNoBuiltInTypeCode_ButStillClassifiesAsAModem()
        {
            Assert.Multiple(() =>
            {
                Assert.That(TypeCode.ForTag("product_rs485_modem"), Is.Null, "no built-in TypeCode (open-world tag)");
                Assert.That(ProductClassifier.Classify("product_rs485_modem"), Is.EqualTo(ProductFamily.Rs485Modem));
                Assert.That(ProductClassifier.IsModem("product_rs485_modem"), Is.True, "the report path treats it as a modem");
                // Contrast: its SMS sibling DOES carry a built-in type code.
                Assert.That(TypeCode.ForTag("product_rs485_sms_modem"), Is.EqualTo(0x56));
            });
        }

        // ----- Classify: open-world fallback + precedence -----

        [TestCase("product_x_airlink", ProductFamily.Airlink)]
        [TestCase("product_x_modem", ProductFamily.Rs485Modem)]
        [TestCase("product_x_airlink_modem", ProductFamily.Airlink)]   // airlink-before-modem tie-break
        [TestCase("product_dataline_switch", ProductFamily.Other)]      // an unknown product with no family marker
        [TestCase("group", ProductFamily.Other)]                         // a non-product tag
        public void Classify_OpenWorld_FollowsStatedPrecedence(string tag, ProductFamily expected) =>
            Assert.That(ProductClassifier.Classify(tag), Is.EqualTo(expected));

        // ----- Predicates -----

        [Test]
        public void Predicates_PartitionProductTags()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProductClassifier.IsProduct("product_dataline"), Is.True);
                Assert.That(ProductClassifier.IsProduct("dataline_input"), Is.False, "a non-product_ tag is not a product");
                Assert.That(ProductClassifier.IsModem("product_x_modem"), Is.True, "open-world modem classifies for the UI");
                Assert.That(ProductClassifier.IsWireless("product_x_airlink"), Is.True, "open-world airlink classifies for the UI");
                Assert.That(ProductClassifier.IsUnlinkedWireless("product_airlink", ""), Is.True);
                Assert.That(ProductClassifier.IsUnlinkedWireless("product_airlink", "_0x0"), Is.True);
                Assert.That(ProductClassifier.IsUnlinkedWireless("product_airlink", "SN123"), Is.False);
                Assert.That(ProductClassifier.IsUnlinkedWireless("product_dataline", ""), Is.False, "only wireless can be unlinked");
            });
        }

        // ----- An open-world tag the predicates admit is documented, but never inherits another family -----

        /// <summary>
        /// Since RL-2c (finding G7) an unrecognised product root IS documented — as a generic component block
        /// of the three shared rows. What stays closed is which FAMILY's fields a root can be given: the
        /// classifier's substring notion of "modem"/"airlink" is an open-world UI convenience, and it must
        /// never hand a rogue root the modem's four wire-colour rows, nor hoist it into the closed
        /// "Specielle Produkter" table. Before RL-2c that separation was kept by excluding such roots from
        /// the report entirely; it is now kept by the generic block carrying no family rows at all.
        /// </summary>
        [Test]
        public async System.Threading.Tasks.Task Report_OpenWorldRoots_AreDocumented_ButNeverInheritAFamily()
        {
            ProjectElement openModem = Element("product_x_modem", ("name", "Rogue modem"));
            ProjectElement openAirlink = Element("product_x_airlink", ("name", "Rogue airlink"));
            ProjectElement group = new("group", null,
                ImmutableArray.Create(("name", "Room")), ImmutableArray.Create(openModem, openAirlink));
            ProjectElement groups = new("groups", null,
                ImmutableArray.Create(("name", "L")), ImmutableArray.Create(group));
            ProjectElement root = new("utcs_project", null,
                ImmutableArray<(string, string)>.Empty, ImmutableArray.Create(groups));
            // T017: membership is observed through the NEW pipeline — the generated installation report.
            using var output = new System.IO.MemoryStream();
            await new ProjectAppService(TestSetup.Settings).GenerateReport(new Project(root),
                ReportKind.Installation, ReportMode.Standard, ReportMimeTypes.PlainText, output);
            string report = System.Text.Encoding.UTF8.GetString(output.ToArray());

            Assert.Multiple(() =>
            {
                // RL-2c: both are documented, as generic component blocks.
                Assert.That(report, Does.Contain("Rogue modem").And.Contain("Rogue airlink"));
                // ...with no family's fields — those two labels appear only as component-block field rows.
                Assert.That(report, Does.Not.Contain("Identifikationskode").And.Not.Contain("Serie nummer"));
                // ...and neither hoists into the closed special-products table, though the UI-facing
                // predicate does call one of them a modem.
                Assert.That(ReportProbe.TableRowCount(report, "Specielle Produkter"), Is.Zero);
                Assert.That(ProductClassifier.IsModem("product_x_modem"), Is.True);
                Assert.That(ProductClassifier.IsWireless("product_x_airlink"), Is.True);
            });
        }

        private static ProjectElement Element(string tag, params (string, string)[] attrs) =>
            new(tag, null, attrs.ToImmutableArray(), ImmutableArray<ProjectElement>.Empty);
    }
}
