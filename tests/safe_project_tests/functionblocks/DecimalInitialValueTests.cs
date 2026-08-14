using System.Globalization;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Alignment F-41/F-44 (IHCReverseEnginneredInfo tmp/align-campaign-2026-08-10.md): the DECIMAL-valued resource
    /// types store a fixed two-fraction-digit number, so the value payload needs a real representation.
    ///
    /// <para>Measured 2026-08-11 by driving the reference application's own dialogs and reading the bytes it saved
    /// (<c>t33-decimals.vis</c> / <c>t33-decimals2.vis</c>): typing <c>1,555</c> into kW stores
    /// <c>inivalue="1.55"</c>; <c>2,25</c> into kWh stores <c>2.25</c>; <c>3,75</c> into Kommatal stores
    /// <c>3.75</c>; <c>55,5</c> into Fugtighed stores <c>55.50</c>; <c>-12,5</c> into Temperatur stores
    /// <c>-12.50</c>. The separator on disk is always a <b>period</b> — the comma belongs to the screen — and the
    /// fraction is always <b>two</b> digits whatever precision the type displays.</para>
    ///
    /// <para><b>W and Wh belong here too</b>, which is F-44: their dialog field shows a whole number and rounds
    /// (typing <c>42,7</c> yields a row reading <c>43W</c>), but the value serialises through the same decimal
    /// writer — the saved bytes are <c>inivalue="43.00"</c> and <c>"7.00"</c>, never <c>"43"</c>. Their DTD default
    /// is <c>"0.00"</c>, exactly like kW's, while the genuinely integer types (Tal, Tæller, Lys, Lysniveau) declare
    /// <c>"0"</c> and store <c>17</c> / <c>42</c> plainly.</para>
    /// </summary>
    public class DecimalInitialValueTests
    {
        private static IhcSettings Settings => TestSetup.Settings;
        private static ProjectAppService App => new(Settings);

        private static async Task<(ProjectDocumentSession session, ElementId variable)> WithVariable(string tag)
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId block = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") != "yes").Id!.Value;
            session.Apply(new AddVariable(block, "settings", tag, "Måler"));
            ElementId variable = session.Current!.FindById(block)!.Descendants()
                .First(e => e.Tag == tag && e.GetAttribute("name") == "Måler").Id!.Value;
            return (session, variable);
        }

        /// <summary>Every measured cell, type by type — the campaign's rule that a set-valued dimension needs each
        /// member resolved individually. The two W/Wh rows are F-44: the reference application writes two fraction
        /// digits for them as well.</summary>
        [TestCase("kW", 1.5, "1.50")]
        [TestCase("kWh", 2.25, "2.25")]
        [TestCase("resource_floating_point", 3.75, "3.75")]
        [TestCase("resource_humidity_level", 55.5, "55.50")]
        [TestCase("resource_temperature", -12.5, "-12.50")]
        [TestCase("W", 43, "43.00")]
        [TestCase("Wh", 7, "7.00")]
        public async Task Decimal_WritesTwoFractionDigitsWithAPeriod(string tag, double value, string expected)
        {
            (ProjectDocumentSession session, ElementId variable) = await WithVariable(tag);

            session.Apply(new SetResourceInitialValue(variable, ResourceInitialValue.OfDecimal(value)));

            Assert.That(session.Current!.FindById(variable)!.GetAttribute("inivalue"), Is.EqualTo(expected),
                $"{tag}: the file stores a period-separated two-decimal number");
        }

        /// <summary>
        /// A THIRD decimal is not stored: kW displays three but the format keeps two. Both measured cases are here
        /// because together they identify the ROUNDING RULE, and each alone would admit a wrong one.
        ///
        /// <para><c>1,125</c> saved <c>1.13</c> — that value is exactly representable in binary, so the tie went
        /// AWAY FROM ZERO, ruling out the to-even <c>1.12</c>. <c>1,555</c> saved <c>1.55</c> — that literal is
        /// really 1.55499999…, below the midpoint, so the rounding is of the EXACT binary value and not of the
        /// shortest decimal that round-trips. .NET's own <c>ToString("0.00")</c> passes the first and fails the
        /// second (it yields <c>1.56</c>), which is exactly why the writer does not use it.</para>
        /// </summary>
        [TestCase(1.555, "1.55")]
        [TestCase(1.125, "1.13")]
        [TestCase(-1.125, "-1.13")]
        public async Task Decimal_RoundsTheExactValueWithTiesAwayFromZero(double value, string expected)
        {
            (ProjectDocumentSession session, ElementId variable) = await WithVariable("kW");

            session.Apply(new SetResourceInitialValue(variable, ResourceInitialValue.OfDecimal(value)));

            Assert.That(session.Current!.FindById(variable)!.GetAttribute("inivalue"), Is.EqualTo(expected));
        }

        /// <summary>The culture of the machine must not reach the file. The screen shows a comma; the bytes are
        /// invariant.</summary>
        [Test]
        public async Task Decimal_IsNotWrittenInTheCurrentCulture()
        {
            (ProjectDocumentSession session, ElementId variable) = await WithVariable("resource_temperature");
            CultureInfo previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");
            try
            {
                session.Apply(new SetResourceInitialValue(variable, ResourceInitialValue.OfDecimal(21.5)));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            Assert.That(session.Current!.FindById(variable)!.GetAttribute("inivalue"), Is.EqualTo("21.50"),
                "a Danish machine must not write 21,50 into a .vis");
        }

        /// <summary>Zero is the DTD default (<c>"0.00"</c>) for every type in this family, so it is omitted — the
        /// omit-if-default rule that keeps a project byte-identical to the reference application's output.</summary>
        [TestCase("kW")]
        [TestCase("W")]
        [TestCase("resource_temperature")]
        public async Task Decimal_OmitsTheDefaultZero(string tag)
        {
            (ProjectDocumentSession session, ElementId variable) = await WithVariable(tag);

            session.Apply(new SetResourceInitialValue(variable, ResourceInitialValue.OfDecimal(0)));

            Assert.That(session.Current!.FindById(variable)!.GetAttribute("inivalue"), Is.Null,
                $"{tag}: 0.00 is the declared default and is not written");
        }

        /// <summary>
        /// A non-finite value is not a number this format can store: <c>inivalue</c> is declared CDATA, so nothing
        /// downstream stops the text "NaN" or "Infinity" from being written into a <c>.vis</c> file that the
        /// reference application would then have to read. The value is refused where the caller's mistake is, at
        /// construction, so no such payload can exist to be written (D02).
        /// </summary>
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Decimal_NonFinite_IsRefusedAtConstruction(double value)
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => ResourceInitialValue.OfDecimal(value),
                    "the factory refuses it");
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => new ResourceInitialValue(ResourceValueKind.Decimal, false, 0, 0, 0, 0, 0, Decimal: value),
                    "and so does the record's own constructor, which is public");
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => _ = ResourceInitialValue.None with { Kind = ResourceValueKind.Decimal, Decimal = value },
                    "and so does a with-expression, the third way to reach the field");
            });
        }

        /// <summary>The payload is one flat record, so a new kind that leaked into another's write path would
        /// corrupt unrelated variables.</summary>
        [Test]
        public async Task Decimal_WritesNothingElse()
        {
            (ProjectDocumentSession session, ElementId variable) = await WithVariable("kW");

            session.Apply(new SetResourceInitialValue(variable, ResourceInitialValue.OfDecimal(1.5)));

            ProjectElement written = session.Current!.FindById(variable)!;
            Assert.Multiple(() =>
            {
                Assert.That(written.GetAttribute("hour"), Is.Null, "a meter has no time fields");
                Assert.That(written.GetAttribute("day"), Is.Null, "nor a date");
            });
        }
    }
}
