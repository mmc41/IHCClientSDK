using System.Linq;
using Ihc.Vis.Products;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The one rule record of the dialog-metadata vocabulary (D12) and its first instance, the US-013
    /// telephone-number rule.
    /// <para>The rule is authored ONCE in the SDK so the modem dialog today and the generic
    /// write-back later validate against the same value — a second copy in the GUI is how the two drift
    /// into disagreeing about what a valid number is.</para>
    /// </summary>
    public class DialogValueRuleTests
    {
        // ── the US-013 boundaries: 3–20 characters, no spaces, leading country code ──────────────────

        [TestCase("+45", true,  TestName = "3 characters is the shortest accepted")]
        [TestCase("+4", false,  TestName = "2 characters is one too few")]
        [TestCase("+4570100001", true, TestName = "an ordinary Danish number")]
        public void PhoneNumber_LengthLowerBoundary(string value, bool expected)
            => Assert.That(DialogValueRule.PhoneNumber.IsSatisfiedBy(value), Is.EqualTo(expected));

        [Test]
        public void PhoneNumber_LengthUpperBoundary()
        {
            string twenty = "+" + new string('4', 19);
            string twentyOne = "+" + new string('4', 20);
            Assert.Multiple(() =>
            {
                Assert.That(twenty, Has.Length.EqualTo(20));
                Assert.That(twentyOne, Has.Length.EqualTo(21));
                Assert.That(DialogValueRule.PhoneNumber.IsSatisfiedBy(twenty), Is.True, "20 characters is accepted");
                Assert.That(DialogValueRule.PhoneNumber.IsSatisfiedBy(twentyOne), Is.False, "21 is one too many");
            });
        }

        [TestCase("+45 70 10 00 01", TestName = "spaces between groups")]
        [TestCase("+4570100001 ",    TestName = "a trailing space")]
        [TestCase(" +4570100001",    TestName = "a leading space")]
        [TestCase("+45\t70100001",   TestName = "a tab")]
        public void PhoneNumber_RejectsWhitespace(string value)
            => Assert.That(DialogValueRule.PhoneNumber.IsSatisfiedBy(value), Is.False);

        [TestCase("4570100001",  TestName = "no plus at all")]
        [TestCase("0045701001",  TestName = "the 00 dialling prefix is not a country code here")]
        [TestCase("+",           TestName = "a plus with no digits after it")]
        [TestCase("+abc",        TestName = "a plus followed by letters")]
        public void PhoneNumber_RejectsAMissingCountryCode(string value)
            => Assert.That(DialogValueRule.PhoneNumber.IsSatisfiedBy(value), Is.False);

        /// <summary>
        /// An EMPTY slot is valid. Only 4 of the 30 slots are typically filled, and a rule that refused
        /// blank would make the dialog uncommittable the moment it opened — the rule constrains what a
        /// number looks like, not whether one is required.
        /// </summary>
        [Test]
        public void PhoneNumber_AcceptsAnEmptySlot()
            => Assert.That(DialogValueRule.PhoneNumber.IsSatisfiedBy(string.Empty), Is.True);

        /// <summary>The refusal is Danish (FR-2.6) and names the rule, not the field.</summary>
        [Test]
        public void PhoneNumber_CarriesItsDanishRefusal()
        {
            string refusal = DialogValueRule.PhoneNumber.Refusal;
            Assert.Multiple(() =>
            {
                Assert.That(refusal, Does.Contain("telefonnummer").IgnoreCase);
                Assert.That(refusal, Does.Contain("3"));
                Assert.That(refusal, Does.Contain("20"));
                Assert.That(refusal, Does.Contain("landekode"));
            });
        }

        /// <summary>
        /// One instance, not one per caller. The Phase 3 preset reuses this exact value, so a preset that
        /// reconstructed an equal-looking rule would let the two drift apart. Guards specifically against
        /// the property being rewritten as an expression body (<c>=> new()</c>), which would hand out a
        /// fresh instance on every read and still pass every other test here.
        /// </summary>
        [Test]
        public void PhoneNumber_IsASingleSharedInstance()
        {
            var first = DialogValueRule.PhoneNumber;
            var second = DialogValueRule.PhoneNumber;
            Assert.That(second, Is.SameAs(first));
        }

        // ── the record itself ────────────────────────────────────────────────────────────────────────

        /// <summary>An unconstrained rule accepts anything — the default for a plain free-text field.</summary>
        [Test]
        public void ARuleWithNoConstraints_AcceptsAnything()
        {
            var free = new DialogValueRule { Refusal = "aldrig" };
            Assert.Multiple(() =>
            {
                Assert.That(free.IsSatisfiedBy(new string('x', 5000)), Is.True);
                Assert.That(free.IsSatisfiedBy("  spaces and\ttabs  "), Is.True);
                Assert.That(free.IsSatisfiedBy(string.Empty), Is.True);
            });
        }

        /// <summary>Content-based equality, like every other model record here (T017's requirement).</summary>
        [Test]
        public void TwoIdenticallyConstructedRules_AreEqual()
        {
            var a = new DialogValueRule { MinLength = 3, MaxLength = 20, Refusal = "x" };
            var b = new DialogValueRule { MinLength = 3, MaxLength = 20, Refusal = "x" };
            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            });
        }
    }
}
