using System.Text;

using Ihc.Vis.Catalog;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The catalog fidelity relation's <b>escaping</b> boundary (decision D3): <c>&amp;apos;</c> and a literal
    /// apostrophe are the same XML value (like the existing empty-element collapse), so the comparer forgives
    /// exactly that pair — one vendor file (<c>1.2.05.ifb</c>) escapes apostrophes where the rest of the corpus
    /// writes them literally. Every <em>other</em> escaping difference stays significant: the relation is
    /// byte-level, and a changed entity means a changed value or changed well-formedness.
    /// </summary>
    public class CatalogTextCompareTests
    {
        private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

        [Test]
        public void ApostropheEntity_IsEquivalentToLiteralApostrophe()
        {
            byte[] escaped = Bytes("<r note=\"PIR&apos;en styrer\" />");
            byte[] literal = Bytes("<r note=\"PIR'en styrer\" />");

            Assert.Multiple(() =>
            {
                Assert.That(CatalogTextCompare.Equivalent(escaped, literal), Is.True,
                    "&apos; in one stream must match a literal ' in the other");
                Assert.That(CatalogTextCompare.Equivalent(literal, escaped), Is.True,
                    "the forgiveness is symmetric");
            });
        }

        [Test]
        public void ApostropheEntity_MatchesOnlyTheApostrophe()
        {
            byte[] escaped = Bytes("<r note=\"PIR&apos;en\" />");
            byte[] different = Bytes("<r note=\"PIR`en\" />");

            Assert.That(CatalogTextCompare.Equivalent(escaped, different), Is.False,
                "&apos; must not match any character other than the apostrophe");
        }

        [Test]
        public void AmpersandEntity_StaysSignificant()
        {
            byte[] escaped = Bytes("<r note=\"a&amp;b\" />");
            byte[] raw = Bytes("<r note=\"a&b\" />");

            Assert.That(CatalogTextCompare.Equivalent(escaped, raw), Is.False,
                "&amp; vs a bare & is a well-formedness difference and must stay significant");
        }

        [Test]
        public void QuoteEntity_StaysSignificant()
        {
            byte[] escaped = Bytes("<r note=\"a&quot;b\" />");
            byte[] raw = Bytes("<r note=\"a\"b\" />");

            Assert.That(CatalogTextCompare.Equivalent(escaped, raw), Is.False,
                "&quot; vs a raw quote changes the quoted-region structure and must stay significant");
        }
    }
}
