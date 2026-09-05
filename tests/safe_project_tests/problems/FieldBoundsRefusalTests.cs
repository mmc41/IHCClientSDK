using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// RF-10 / D05: the bound refusals a product dialog raises say what their catalogue rows say.
    ///
    /// <para>ONE entry used to govern every out-of-bounds refusal, declaring <c>{field}</c>, <c>{minimum}</c> and
    /// <c>{maximum}</c> — but a field constrained on ONE side has no value for the other slot, so the site could
    /// not bind the row and authored four sentences of its own instead. The catalogue then described a sentence
    /// no user ever saw, which is the opposite of the catalogue being the truth.</para>
    ///
    /// <para>D05 splits the code by REACHABLE BOUND SHAPE: both bounds, minimum only, maximum only. Each row then
    /// has one template whose declared slots always bind, the vendor-measured numbers stay in the user's sentence,
    /// and the site chooses a code instead of composing prose. The fourth arm the site used to carry — neither
    /// bound — was unreachable: the caller returns before it when both bounds are absent.</para>
    /// </summary>
    [TestFixture]
    public sealed class FieldBoundsRefusalTests
    {
        /// <summary>The Danish words that only a range refusal uses, in the forms the site used to author.</summary>
        private static readonly string[] RangeWording =
            ["skal være mellem", "skal være mindst", "skal være højst", "uden for sit tilladte område"];

        private static string CommandSource()
        {
            string path = Path.Combine(
                TestRepository.RequireRoot(), "ihcclient", "src", "vis", "session", "ProductDialogCommands.cs");
            Assert.That(File.Exists(path), Is.True, $"the raising site is missing at {path}");
            return File.ReadAllText(path, Encoding.UTF8);
        }

        /// <summary>
        /// No range sentence is authored at the raise site any more. The site names a CODE; the words belong to
        /// the row, and to the one owner the drift test below compares against it.
        /// </summary>
        [Test]
        public void NoRangeSentenceIsAuthoredInTheCommand()
        {
            string source = CommandSource();

            string[] authored = [.. RangeWording.Where(w => source.Contains(w, StringComparison.Ordinal))];

            Assert.That(authored, Is.Empty,
                "these range sentences are still written at the raise site instead of coming from the catalogue "
                + "row: " + string.Join(", ", authored.Select(w => $"\"{w}\"")));
        }

        /// <summary>
        /// The drift gate for the three rows. The refusing site sits below the validation engine and may not read
        /// the catalogue, so each sentence is necessarily written twice — once on the entry, once on the owner
        /// beside the codes. This requires the two copies to be the same words.
        /// </summary>
        [Test]
        public void EachBoundRowsTemplateIsTheOwnersCopy()
        {
            (ProblemCode Code, string Owned)[] rows =
            [
                (EditRefusalCodes.FieldOutOfRange, EditRefusalProblems.FieldOutOfRangeRefusal),
                (EditRefusalCodes.FieldBelowMinimum, EditRefusalProblems.FieldBelowMinimumRefusal),
                (EditRefusalCodes.FieldAboveMaximum, EditRefusalProblems.FieldAboveMaximumRefusal),
                (EditRefusalCodes.FieldNotANumber, EditRefusalProblems.FieldNotANumberRefusal),
            ];

            Assert.Multiple(() =>
            {
                foreach ((ProblemCode code, string owned) in rows)
                {
                    Assert.That(ProblemCatalog.Current.TryGet(code, out ProblemCatalogEntry entry), Is.True,
                        $"{code.Value} has no catalogue entry");
                    Assert.That(entry.MessageTemplate, Is.EqualTo(owned), code.Value);
                }
            });
        }

        /// <summary>
        /// Each reachable bound shape earns its OWN code and a sentence with every declared slot filled. This is
        /// the assertion the old single row could not make: a one-sided field had no value for the other slot.
        /// </summary>
        [Test]
        public void EachBoundShapeRefusesUnderItsOwnCodeWithEverySlotBound()
        {
            (string Case, int? Min, int? Max, int Value, ProblemCode Code, string Message)[] cases =
            [
                ("both bounds, below", 10, 20, 5,
                    EditRefusalCodes.FieldOutOfRange, "Feltet 'SIM-pinkode' skal være mellem 10 og 20."),
                ("both bounds, above", 10, 20, 25,
                    EditRefusalCodes.FieldOutOfRange, "Feltet 'SIM-pinkode' skal være mellem 10 og 20."),
                ("minimum only", 10, null, 5,
                    EditRefusalCodes.FieldBelowMinimum, "Feltet 'SIM-pinkode' skal være mindst 10."),
                ("maximum only", null, 20, 25,
                    EditRefusalCodes.FieldAboveMaximum, "Feltet 'SIM-pinkode' skal være højst 20."),
            ];

            Assert.Multiple(() =>
            {
                foreach ((string name, int? min, int? max, int value, ProblemCode code, string message) in cases)
                {
                    (ProblemCode Code, string Message)? refusal =
                        EditRefusalProblems.FieldBounds("SIM-pinkode", min, max, value);

                    Assert.That(refusal, Is.Not.Null, name);
                    Assert.That(refusal!.Value.Code, Is.EqualTo(code), name);
                    Assert.That(refusal.Value.Message, Is.EqualTo(message), name);
                    Assert.That(refusal.Value.Message, Does.Not.Contain("{"),
                        $"{name}: every declared slot of the chosen row binds");
                }
            });
        }

        /// <summary>
        /// The not-a-number row binds both its slots, so no placeholder survives into the sentence a user sees.
        /// </summary>
        [Test]
        public void TheNotANumberRowBindsTheFieldAndTheSubmittedText()
        {
            (ProblemCode code, string message) = EditRefusalProblems.FieldNotANumber("SIM-pinkode", "abc");

            Assert.Multiple(() =>
            {
                Assert.That(code, Is.EqualTo(EditRefusalCodes.FieldNotANumber));
                Assert.That(message, Does.Contain("SIM-pinkode"));
                Assert.That(message, Does.Contain("abc"));
                Assert.That(message, Does.Not.Contain("{"), "every declared slot binds");
            });
        }

        /// <summary>A value inside its bounds is not refused — the control every bound check needs.</summary>
        [Test]
        public void AValueWithinItsBoundsIsNotRefused()
        {
            Assert.Multiple(() =>
            {
                Assert.That(EditRefusalProblems.FieldBounds("SIM-pinkode", 10, 20, 15), Is.Null, "between");
                Assert.That(EditRefusalProblems.FieldBounds("SIM-pinkode", 10, null, 15), Is.Null, "at or above min");
                Assert.That(EditRefusalProblems.FieldBounds("SIM-pinkode", null, 20, 15), Is.Null, "at or below max");
                Assert.That(EditRefusalProblems.FieldBounds("SIM-pinkode", null, null, 15), Is.Null,
                    "a field with no bound cannot be outside them");
            });
        }
    }
}
