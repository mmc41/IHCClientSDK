using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsCheck;
using Ihc;
using Ihc.App;

namespace Ihc.Tests
{
    /// <summary>
    /// Culture invariance, stated as a law rather than as examples.
    ///
    /// <para>The SDK renders values that a person reads and that other tools parse, and NONE of that
    /// rendering may depend on the culture the host process happens to be running under. The example tests
    /// beside this file pin what a particular value looks like; this pins the property that makes those
    /// examples reproducible on someone else's machine — <b>the same input produces the same text under any
    /// culture</b>. It is a metamorphic law: it needs no expected string, only that the outputs agree.</para>
    ///
    /// <para>The cultures are chosen for the ways they DIFFER from the invariant one, not for coverage:
    /// da-DK writes a decimal comma and sorts Æ/Ø/Å after Z; sv-SE writes its negative sign as U+2212 MINUS
    /// rather than ASCII hyphen; en-US is the common CI default and orders dates month-first.</para>
    ///
    /// <para>NonParallelizable, and every case restores the culture it found: CurrentCulture is per-thread,
    /// and NUnit hands threads back to a pool that later tests run on.</para>
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class CultureInvariancePropertyTests
    {
        private static readonly CultureInfo[] Cultures =
        [
            CultureInfo.GetCultureInfo("da-DK"),
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("sv-SE"),
            CultureInfo.InvariantCulture,
        ];

        /// <summary>Runs <paramref name="render"/> once per culture and returns the distinct texts produced.
        /// A culture-independent renderer yields exactly one.</summary>
        private static string[] UnderEveryCulture(Func<string> render)
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                return Cultures.Select(c =>
                {
                    CultureInfo.CurrentCulture = c;
                    return render();
                }).Distinct(StringComparer.Ordinal).ToArray();
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        // ── The value space FormatResult claims to handle ────────────────────────────────────────────────

        private static readonly Gen<object> Integers = Gen.OneOf<object>(
            Gen.Int.Select(x => (object)x), Gen.Long.Select(x => (object)x),
            Gen.Short.Select(x => (object)x), Gen.Byte.Select(x => (object)x),
            Gen.UInt.Select(x => (object)x), Gen.ULong.Select(x => (object)x),
            Gen.UShort.Select(x => (object)x), Gen.SByte.Select(x => (object)x));

        private static readonly Gen<object> Reals = Gen.OneOf<object>(
            Gen.Long.Select(bits => (object)BitConverter.Int64BitsToDouble(bits)),
            Gen.Int.Select(bits => (object)BitConverter.Int32BitsToSingle(bits)),
            Gen.Int.Select(i => (object)(i / 100m)),
            // Half and BigInteger are IFormattable but named by no case in FormatResult, so they exercise
            // the fallback rather than a branch.
            Gen.Int[-10000, 10000].Select(i => (object)(Half)(i / 8.0f)),
            Gen.Long.Select(l => (object)new System.Numerics.BigInteger(l)),
            Gen.OneOfConst<object>(double.NaN, double.PositiveInfinity, double.NegativeInfinity,
                                   float.NaN, 0d, -0d, double.MaxValue, double.MinValue, decimal.MinValue));

        /// <summary>DateTime.MaxValue.Ticks — the whole representable range, so the boundary values a
        /// formatter is most likely to get wrong are reachable.</summary>
        private const long MaxDateTicks = 3_155_378_975_999_999_999L;

        /// <summary>The largest offset generated below. A DateTimeOffset's UTC instant is (local − offset) and
        /// the constructor REFUSES one outside DateTime's range, so the tick range it draws from is inset by
        /// this much at both ends. Without the inset the GENERATOR throws near either boundary — rarely enough
        /// to read as a flake, and reported against this fixture, so it reads as a culture defect rather than
        /// as a bad generator.</summary>
        private const long MaxOffsetTicks = TimeSpan.TicksPerHour * 12;

        private static readonly Gen<object> Temporal = Gen.OneOf<object>(
            Gen.Long[0, MaxDateTicks].Select(t => (object)new DateTime(t, DateTimeKind.Utc)),
            Gen.Select(Gen.Long[MaxOffsetTicks, MaxDateTicks - MaxOffsetTicks], Gen.Int[-12, 12])
               .Select(t => (object)new DateTimeOffset(new DateTime(t.Item1, DateTimeKind.Unspecified),
                                                       TimeSpan.FromHours(t.Item2))),
            Gen.Long.Select(t => (object)new TimeSpan(t)));

        private static readonly Gen<object> Others = Gen.OneOf<object>(
            Gen.Bool.Select(b => (object)b),
            Gen.Byte.Array[0, 40].Select(a => (object)a),
            Gen.OneOfConst<object>("plain", "", "1,5", "æøå", Guid.Empty, new Guid("0f8fad5b-d9cb-469f-a165-70867728950e")));

        private static readonly Gen<object> AnyScalar = Gen.OneOf(Integers, Reals, Temporal, Others);

        /// <summary>The same value space, wrapped one level deep, so the element path is sampled as widely as
        /// the scalar path. FormatResult renders a collection by formatting each element.</summary>
        private static readonly Gen<object> AnyCollection =
            AnyScalar.Array[0, 6].Select(items => (object)items);

        /// <summary>
        /// The law: <see cref="LabAppService.FormatResult"/> renders any value it accepts to ONE text,
        /// whatever culture the host is under. This is the generic statement of the three defects the
        /// example tests pin individually — a DateTimeOffset element, a bool element, and a formattable the
        /// scalar path does not name — and it holds for values nobody thought to write a case for.
        /// </summary>
        [Test]
        public void FormatResult_RendersTheSameTextUnderEveryCulture()
        {
            Gen.OneOf(AnyScalar, AnyCollection).Sample(value =>
            {
                string[] texts = UnderEveryCulture(() => LabAppService.FormatResult(value, value?.GetType() ?? typeof(object)));
                Assert.That(texts, Has.Length.EqualTo(1),
                    $"{value?.GetType().Name} renders differently by culture: [{string.Join(" | ", texts)}]");
                return true;
            }, iter: 10_000);
        }

        /// <summary>
        /// The same law for user ordering: the SIGN of a comparison is a property of the SDK, not of the
        /// host. A CurrentCulture comparison satisfies this only by accident — it fails the moment two names
        /// collate differently in two cultures, which is exactly what Æ/Ø/Å do against Z.
        /// </summary>
        [Test]
        public void CompareTo_OrdersTwoUsernamesTheSameUnderEveryCulture()
        {
            Gen<string> name = Gen.OneOfConst(
                "anna", "Bo", "zebra", "Zoe", "æble", "Ærø", "øst", "Ødegaard", "århus", "Aage", "aage",
                "", " ", "10", "2", "élan", "Ostergaard");

            Gen.Select(name, name).Sample(pair =>
            {
                var a = new IhcUser { Username = pair.Item1 };
                var b = new IhcUser { Username = pair.Item2 };
                string[] signs = UnderEveryCulture(() => Math.Sign(a.CompareTo(b)).ToString(CultureInfo.InvariantCulture));
                return signs.Length == 1;
            }, iter: 5_000);
        }
    }
}
