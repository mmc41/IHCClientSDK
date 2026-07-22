using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// US-012/013/014: the shared SDK product classifier. The known catalog families classify exactly; the
    /// open-world predicates route undocumented tags for the UI (airlink-before-modem precedence); and — the
    /// Finding-5 correction — an open-world tag the predicates recognise still never leaks into a closed-set
    /// installation-report section.
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

        // ----- No-leak: an open-world tag the predicates admit never reaches a closed-set report section -----

        [Test]
        public void Report_ClosedSectionMembership_ExcludesOpenWorldTags()
        {
            ProjectElement openModem = Element("product_x_modem", ("name", "Rogue modem"));
            ProjectElement openAirlink = Element("product_x_airlink", ("name", "Rogue airlink"));
            ProjectElement group = new("group", null,
                ImmutableArray.Create(("name", "Room")), ImmutableArray.Create(openModem, openAirlink));
            ProjectElement root = new("utcs_project", null,
                ImmutableArray<(string, string)>.Empty, ImmutableArray.Create(group));
            InstallationReport report = new ProjectAppService(TestSetup.Settings)
                .GenerateProjectDocumentationReport(new Project(root)).Installation;

            Assert.Multiple(() =>
            {
                // Membership stayed closed: the open-world products entered no report section.
                Assert.That(report.ProductDetails, Is.Empty);
                Assert.That(report.ModemDetails, Is.Empty);
                Assert.That(report.SpecialProducts, Is.Empty);
                // ...yet the UI-facing predicates DO recognise them.
                Assert.That(ProductClassifier.IsModem("product_x_modem"), Is.True);
                Assert.That(ProductClassifier.IsWireless("product_x_airlink"), Is.True);
            });
        }

        private static ProjectElement Element(string tag, params (string, string)[] attrs) =>
            new(tag, null, attrs.ToImmutableArray(), ImmutableArray<ProjectElement>.Empty);
    }
}
