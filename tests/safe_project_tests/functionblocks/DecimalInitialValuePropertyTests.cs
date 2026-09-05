using System.Globalization;
using System.Text.RegularExpressions;
using CsCheck;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The property-based half of the decimal initial-value laws (F-41/F-44). <see cref="DecimalInitialValueTests"/>
    /// pins the measured vendor cells one at a time; this pins the SHAPE law that must hold for every finite value
    /// a caller can supply — the file always receives a plain two-fraction-digit number with a period.
    ///
    /// <para>The shape law is what catches the whole class of defect T004 fixed: the writer formats through
    /// <c>double</c>, so anything the format cannot express (NaN, infinity) or fails to convert (a magnitude beyond
    /// <c>decimal</c>'s range) leaks out as text rather than as a number. Sampling therefore has to reach the ends
    /// of the double range — subnormals, <c>double.MaxValue</c>, negative zero — not just plausible meter readings,
    /// which is why the generator draws raw bit patterns alongside realistic values.</para>
    /// </summary>
    public class DecimalInitialValuePropertyTests
    {
        /// <summary>A plain decimal number with exactly two fraction digits and a period. No exponent, no
        /// culture separator, no words.</summary>
        private static readonly Regex TwoDecimalShape =
            new(@"^-?[0-9]+\.[0-9]{2}$", RegexOptions.CultureInvariant);

        /// <summary>Realistic meter readings, the values the dialogs actually produce.</summary>
        private static readonly Gen<double> Realistic = Gen.Double[-100_000, 100_000];

        /// <summary>Any finite double at all, drawn as a raw bit pattern so subnormals and the huge exponents are
        /// reached — a uniform numeric range never visits them.</summary>
        private static readonly Gen<double> AnyFiniteBitPattern =
            Gen.Long.Select(BitConverter.Int64BitsToDouble).Where(double.IsFinite);

        /// <summary>Subnormals — exponent bits all zero. A uniform bit draw lands here about once in two thousand,
        /// far too rarely to count as covered, and they are exactly where the conversion to <c>decimal</c> gives up
        /// and the fallback formatter takes over.</summary>
        private static readonly Gen<double> Subnormal = Gen.Select(Gen.Long[1, (1L << 52) - 1], Gen.Bool)
            .Select(t => t.Item2 ? -BitConverter.Int64BitsToDouble(t.Item1) : BitConverter.Int64BitsToDouble(t.Item1));

        /// <summary>
        /// The named edges, which random sampling will not hit on its own — so they are not sampled. They used to
        /// be a fourth branch of the generator below, which meant the most valuable inputs in the file were reached
        /// by chance: at a quarter of the draws spread over eleven values, a short run could miss
        /// <c>double.MaxValue</c> entirely and still report as a pass. That made the iteration count load-bearing
        /// for a reason nothing stated, and it is why the count was high.
        /// <para>Enumerated instead, they are reached on EVERY run, at a cost that does not scale with anything —
        /// which is what lets the sampled branches below run shorter without losing them.</para>
        /// </summary>
        private static readonly double[] NamedEdges =
        [
            0d, -0d, double.Epsilon, -double.Epsilon, double.MaxValue, double.MinValue,
            1d / 3d, -1d / 3d, 0.005, -0.005, 0.994999999999,
        ];

        /// <summary>
        /// What is left to SAMPLE once the named edges are enumerated: the regions too large to name a
        /// representative of. Iterations here buy coverage of those regions and nothing else.
        /// </summary>
        private static readonly Gen<double> AnyFinite =
            Gen.OneOf(Realistic, AnyFiniteBitPattern, Subnormal);

        /// <summary>How many values each sampled law draws. Lower than it was, because the eleven values that
        /// previously justified a high count are no longer drawn at all.</summary>
        private const int SampledValues = 300;

        private static ProjectAppService App => new(TestSetup.Settings);

        /// <summary>A session holding one kW variable, reused across a sample run: each apply overwrites the same
        /// <c>inivalue</c>, so the attribute read back is always the value just written.</summary>
        private static async Task<(ProjectDocumentSession session, ElementId variable)> WithMeter()
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId block = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") != "yes").Id!.Value;
            session.Apply(new AddVariable(block, "settings", "kW", "Måler"));
            ElementId variable = session.Current!.FindById(block)!.Descendants()
                .First(e => e.Tag == "kW" && e.GetAttribute("name") == "Måler").Id!.Value;
            return (session, variable);
        }

        private static string? Written(ProjectDocumentSession session, ElementId variable, double value)
        {
            session.Apply(new SetResourceInitialValue(variable, ResourceInitialValue.OfDecimal(value)));
            return session.Current!.FindById(variable)!.GetAttribute("inivalue");
        }

        /// <summary>
        /// The law: whatever finite value is set, the file receives two fraction digits and a period — or nothing
        /// at all, which is the omit-if-default rule for the declared <c>"0.00"</c> and is itself well-formed.
        /// </summary>
        [Test]
        public async Task Decimal_AlwaysWritesTwoFractionDigits_ForAnyFiniteValue()
        {
            (ProjectDocumentSession session, ElementId variable) = await WithMeter();

            AnyFinite.Sample(value =>
            {
                string? written = Written(session, variable, value);
                return written is null || TwoDecimalShape.IsMatch(written);
            }, iter: SampledValues, threads: 1);
        }

        /// <summary>
        /// The attribute is absent for exactly the values that round to the declared default, and present
        /// otherwise. Without this, the shape law above could be satisfied by a writer that simply stopped writing.
        /// </summary>
        [Test]
        public async Task Decimal_OmitsTheAttribute_OnlyWhenTheValueRoundsToZero()
        {
            (ProjectDocumentSession session, ElementId variable) = await WithMeter();

            AnyFinite.Sample(value =>
            {
                string? written = Written(session, variable, value);
                bool roundsToZero = Math.Abs(value) < 0.005;
                return written is null == roundsToZero;
            }, iter: SampledValues, threads: 1);
        }

        /// <summary>
        /// Both laws above, at every named edge, on every run. One test rather than eleven cases because the
        /// eleven share a session and a single load: the values are the subject, not the fixtures. Both laws are
        /// asserted together here for the same reason they are separate above — a single concrete value has one
        /// answer for each, so checking both says strictly more per value, where a SAMPLED run wants each law
        /// named so a shrunk counter-example says which one broke.
        /// <para><see cref="Assert.Multiple"/> so a formatter that breaks several edges at once reports all of
        /// them, and each message carries the value: <c>R</c> round-trips every double exactly, so the failure
        /// names an input that can be pasted back in.</para>
        /// </summary>
        [Test]
        public async Task Decimal_AtEveryNamedEdge_KeepsBothLaws()
        {
            (ProjectDocumentSession session, ElementId variable) = await WithMeter();

            Assert.Multiple(() =>
            {
                foreach (double value in NamedEdges)
                {
                    string? written = Written(session, variable, value);
                    string edge = value.ToString("R", CultureInfo.InvariantCulture);

                    Assert.That(written is null || TwoDecimalShape.IsMatch(written), Is.True,
                        $"edge {edge}: wrote '{written}', which is neither omitted nor two-fraction-digit shape");
                    Assert.That(written is null, Is.EqualTo(Math.Abs(value) < 0.005),
                        $"edge {edge}: wrote '{written}', but omission must mean exactly 'rounds to zero'");
                }
            });
        }

        /// <summary>
        /// The anchors. A shape law alone is blind to the VALUE — a writer that always stored <c>0.00</c> would
        /// satisfy it — so the four cells measured from the reference application ride along with the property,
        /// and each rules out a different wrong rounding rule (see <see cref="DecimalInitialValueTests"/>).
        /// </summary>
        [TestCase(1.125, "1.13")]     // an exact binary tie goes away from zero, not to even
        [TestCase(1.555, "1.55")]     // …but this literal is 1.5549999…, so it rounds down
        [TestCase(43d, "43.00")]      // a whole number still stores two fraction digits
        [TestCase(55.5, "55.50")]     // …and one displayed decimal stores two
        public async Task Decimal_MeasuredVendorCells_StillHold(double value, string expected)
        {
            (ProjectDocumentSession session, ElementId variable) = await WithMeter();

            Assert.That(Written(session, variable, value), Is.EqualTo(expected));
        }
    }
}
