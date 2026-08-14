using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The metamorphic law of the product properties dialog: pressing OK once with N fields changed must leave the
    /// same project as changing those N fields one at a time and pressing OK after each. The installer's pace is
    /// not supposed to be visible in the file.
    /// <para>
    /// It is a law worth stating because the two routes take genuinely different code paths through
    /// <see cref="ApplyProductDialog"/>: a batch composes the descriptor ONCE, against the pre-edit project, and
    /// validates every edit against that one snapshot, while a sequence re-composes it per submit. The pre-edit
    /// composition is exactly what makes the batch cheap — and exactly what could let it accept an edit the
    /// sequence would have re-judged.
    /// </para>
    /// <para>
    /// Follows the pattern set by <c>CompositeCommandMetamorphicTests</c> — mutable carrier, explicit
    /// byte-comparing <c>equal:</c>, independence built into the generator, <c>threads: 1</c> — which is stated
    /// there and applied here through <see cref="ProductDialogHarness"/>. This file covers ONE family; the
    /// whole-catalog sweep runs the same harness over every placeable product.
    /// </para>
    /// </summary>
    public class ProductDialogMetamorphicTests
    {
        // A wired dataline product — the plainest family, and the one the ApplyProductDialog behaviour tests
        // already use, so a divergence here is about the batch/sequence split and not about an exotic dialog.
        private const string DatalineProduct = "_0x2101";

        [Test]
        public async Task OneSubmitOfNFields_EqualsNSingleFieldSubmits()
        {
            PlacedProduct placed = await ProductDialogHarness.PlaceAsync(DatalineProduct);
            Assert.That(placed.Placed, Is.True, "precondition: " + placed.UnplaceableReason);

            int exercised = ProductDialogHarness.CheckBatchEqualsSequence(placed, iter: 100);

            Assert.That(exercised, Is.GreaterThan(1),
                "the law is only meaningful if the dialog offers several writable fields to spread across submits");
        }

        /// <summary>
        /// The D09 boundary: two edits to the SAME field are DEPENDENT — a batch validates both against the pre-edit
        /// dialog while a sequence re-judges the second against what the first wrote — and this pins what that
        /// costs.
        /// <para>What it does NOT do is arm the byte comparison. For a plain text field the two routes converge on
        /// the same project (last write wins either way), so this case is invisible to the equality the property
        /// runs on; the armed detector for that comparison lives in
        /// <c>CompositeCommandMetamorphicTests.DependentParts_DivergeBetweenTheTwoPaths_AndTheComparisonSeesIt</c>,
        /// where a delete-then-rename does separate the two paths. The difference dependent edits make HERE is in
        /// the undo history, and that is what is asserted — which is also why the generator hands out distinct
        /// fields rather than trusting that repeats happen to converge.</para>
        /// </summary>
        [Test]
        public async Task TwoEditsToOneField_ConvergeOnTheProject_ButCostOneUndoStepPerSubmit()
        {
            PlacedProduct placed = await ProductDialogHarness.PlaceAsync(DatalineProduct);
            // An unconstrained free-text field, chosen EXPLICITLY: the literals below are written straight into it,
            // so a numeric, checkbox or rule-carrying field would refuse them and this would be pinning a refusal
            // while looking like it pinned a write. All three text kinds qualify — this family's writable fields
            // are a multi-line note and a suggesting combo, neither of them a plain Text box.
            DialogDescriptorField field = placed.EditableFields.First(f => f.Rule is null
                && f.Control is DialogControlKind.Text or DialogControlKind.TextMultiline or DialogControlKind.ComboSuggest);

            ImmutableArray<ProductDialogEdit> pair =
            [
                new ProductDialogEdit(field.Target, field.Attribute, "første"),
                new ProductDialogEdit(field.Target, field.Attribute, "anden"),
            ];

            var asOneSubmit = new ProjectDocumentSession();
            asOneSubmit.Open(placed.Project);
            asOneSubmit.Apply(new ApplyProductDialog(placed.ProductId, pair));

            var oneAtATime = new ProjectDocumentSession();
            oneAtATime.Open(placed.Project);
            foreach (ProductDialogEdit edit in pair)
            {
                oneAtATime.Apply(new ApplyProductDialog(placed.ProductId, [edit]));
            }

            Assert.Multiple(() =>
            {
                Assert.That(asOneSubmit.Current!.FindById(field.Target)!.GetAttribute(field.Attribute),
                    Is.EqualTo("anden"), "last write wins within one submit");
                Assert.That(oneAtATime.Current!.FindById(field.Target)!.GetAttribute(field.Attribute),
                    Is.EqualTo("anden"), "…and across submits");
                // The projects agree, so the property's own comparison cannot tell these two routes apart — stated
                // as an assertion rather than left implied, since the class doc turns on it.
                Assert.That(asOneSubmit.Current!.FindById(field.Target)!.GetAttribute(field.Attribute),
                    Is.EqualTo(oneAtATime.Current!.FindById(field.Target)!.GetAttribute(field.Attribute)),
                    "a plain text field's value does not change what the dialog offers, so the two routes converge");
                // The undo history is where the difference is real.
                Assert.That(oneAtATime.CanUndo, Is.True);
                oneAtATime.Undo();
                Assert.That(oneAtATime.Current!.FindById(field.Target)!.GetAttribute(field.Attribute),
                    Is.EqualTo("første"),
                    "one undo steps back ONE submit, where the batch's single undo would have reversed both");
            });
        }
    }
}
