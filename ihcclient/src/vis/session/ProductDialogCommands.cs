#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>One field's new value, already resolved to the element that holds it.</summary>
    /// <param name="Target">The element the attribute lives on — from the descriptor, never re-derived here.</param>
    /// <param name="Attribute">The attribute to write.</param>
    /// <param name="Value">Its new value.</param>
    public readonly record struct ProductDialogEdit(ElementId Target, string Attribute, string Value);

    /// <summary>
    /// A request to open one composite sub-dialog after the commit — the terminal grids or the advanced-dimmer
    /// settings. ONE typed slot replaces the pair of ad-hoc flags the old result carried
    /// (<c>OpenAdvanced</c> plus <c>ConfigureTerminalPinId</c>), which could contradict each other.
    /// </summary>
    /// <param name="Kind">Which composite widget the installer activated.</param>
    /// <param name="Target">The element it applies to (a terminal's pin id), or null when the widget needs none.</param>
    public readonly record struct ProductDialogWidgetAction(DialogWidgetKind Kind, ElementId? Target);

    /// <summary>
    /// Applies a product properties dialog as ONE undoable commit: a flat list of pre-resolved
    /// <see cref="ProductDialogEdit"/> triples, whatever family the dialog belonged to.
    ///
    /// <para>This is the generic write-back the metadata engine exists for. It replaces the per-family
    /// <c>UpdateProduct</c>/<c>UpdateModem</c> pair, and it knows nothing about families: the composer already
    /// decided which element each value lives on, so this validates and writes.</para>
    ///
    /// <para><b>Validation is not delegated to the caller.</b> Every edit must name an element that still exists,
    /// that lies within the product's own subtree, and that the product's dialog actually offers as a writable
    /// field satisfying its rule. The rule is looked up from the family preset rather than taken from the caller —
    /// a caller that could supply its own rule could also omit it.</para>
    ///
    /// <para><see cref="WidgetAction"/> is CARRIED, never executed. The composite sub-dialogs keep their own
    /// commands and flows (D05); this slot only tells the caller which one the installer asked for, so the
    /// decision travels with the commit instead of beside it.</para>
    /// </summary>
    public sealed record ApplyProductDialog(
        ElementId ProductId,
        EquatableArray<ProductDialogEdit> Edits,
        ProductDialogWidgetAction? WidgetAction = null) : ProjectCommand
    {
        internal override string Describe(Project project) => "Rediger produkt";

        internal override EditVerdict Evaluate(EditContext context)
        {
            EditVerdict exists = context.RequireExists(ProductId, "Produktet");
            if (!exists.Ok)
            {
                return exists;
            }
            if (Edits.IsEmpty)
            {
                // OK without touching a field is an ordinary act — and the commonest one, since a just-inserted
                // product raises its dialog. There is nothing to validate against, so the descriptor (a whole-
                // project compose) is never built for it.
                return EditVerdict.Allow;
            }

            ProjectElement product = context.Index.FindById(ProductId)!;

            // The dialog this product would show, composed against the pre-edit project: the authority on which
            // (element, attribute) pairs are writable fields and what each one accepts.
            //
            // Through the SAME door the read side uses, which is what makes the two agree field for field —
            // including the one product whose dialog carries the end-user-report checkbox (T099). A command that
            // had to be TOLD which fields exist would be a caller supplying its own contract.
            var offered = ProductDialogComposer
                .ComposeFor(context.Project, product, product.Tag)
                .AllFields
                .ToDictionary(f => (f.Target, f.Attribute));

            var subtree = new HashSet<ElementId>();
            ProjectTreeOps.CollectIds(product, subtree);

            foreach (ProductDialogEdit edit in Edits)
            {
                if (context.Index.FindById(edit.Target) is null)
                {
                    return EditVerdict.Refuse(EditRefusalCodes.FieldTargetMissing, "Et af felterne peger på et element, der ikke findes længere.");
                }
                if (!subtree.Contains(edit.Target))
                {
                    // Not a technicality: without it, a dialog could be handed an id belonging to a DIFFERENT
                    // product and would edit that one instead, reporting success either way.
                    return EditVerdict.Refuse(EditRefusalCodes.FieldOutsideProduct, "Et af felterne peger på et element uden for produktet.");
                }
                if (!offered.TryGetValue((edit.Target, edit.Attribute), out DialogDescriptorField? field))
                {
                    return EditVerdict.Refuse(EditRefusalCodes.FieldNotOffered, $"Produktets dialog har ikke feltet '{edit.Attribute}'.");
                }
                if (field.ReadOnly)
                {
                    return EditVerdict.Refuse(EditRefusalCodes.FieldReadOnly, $"Feltet '{field.Caption}' kan ikke redigeres.");
                }
                if (field.Rule is { } rule && !rule.IsSatisfiedBy(edit.Value))
                {
                    (ProblemCode code, string sentence) = ValueRuleRefusal(rule, edit.Value);
                    return EditVerdict.Refuse(code, sentence);
                }
                // The bounds the composer derived from the placed element, finally read. Without this the dialog
                // knows a SIM PIN runs 0-9999, shows it, and then commits 99999 anyway — the descriptor was the
                // authority on which fields exist and what they accept, and only the second half went unused.
                //
                // A BLANK value is not out of range: a numeric field sitting at its declared default presents
                // blank, and committing blank writes the default back. Neither is an unparseable one — this
                // condition answers "is this number outside its bounds", and nothing else.
                if (OutsideBounds(field, edit.Value) is { } outside)
                {
                    return EditVerdict.Refuse(outside.Code, outside.Message);
                }
            }
            return EditVerdict.Allow;
        }

        /// <summary>
        /// Which coded identity a broken field rule refuses under, and the Danish sentence that goes with it.
        /// <para>The telephone rule has a catalogue entry OF ITS OWN, with a <c>{value}</c> slot, because the
        /// sentence this site shows is the rule's specific guidance rather than the generic entry's
        /// <i>"Feltet {field} har en ugyldig værdi."</i> — a template with no slot a value could bind to. Every
        /// other rule keeps the generic code, which is the honest answer while its sentence stays the rule's.</para>
        /// <para>The sentence is written HERE rather than read from the catalogue because
        /// <c>Ihc.Vis.Session</c> may not depend on <c>Ihc.Vis.Validation</c>. That is the established shape for
        /// a refusing site — its own copy beside its code — and a drift test keeps the copy equal to the entry's
        /// template.</para>
        /// <para>The rule is identified by IDENTITY, not by re-testing its members: <c>DialogValueRule</c> is a
        /// record, so a value comparison would also match any other rule that happened to carry the same four
        /// bounds, and this code is about the telephone field specifically.</para>
        /// </summary>
        /// <param name="rule">The field rule the submitted value broke.</param>
        /// <param name="value">The offending value, as submitted.</param>
        private static (ProblemCode Code, string Sentence) ValueRuleRefusal(DialogValueRule rule, string value) =>
            ReferenceEquals(rule, DialogValueRule.PhoneNumber)
                ? (EditRefusalCodes.FieldPhonenumberMalformed,
                    $"Telefonnummeret '{value}' skal være på 3-20 tegn uden mellemrum og begynde med en "
                    + "landekode, f.eks. +45.")
                : (EditRefusalCodes.FieldValueRule, rule.Refusal);

        /// <summary>
        /// Which coded refusal a numeric field's value earns when it falls outside the bounds its own catalog
        /// element declares, or null when it does not. Blank and unparseable values are not this condition.
        /// <para>
        /// The identity and the sentence BOTH come from <see cref="EditRefusalProblems.FieldBounds"/> (D05).
        /// This site used to author four sentences of its own, one per bound shape, under a single code whose
        /// catalogue row declared a template none of them matched — so the row described words no user saw. The
        /// site now decides nothing about wording: it reports which bounds the field declares and what was
        /// submitted, and the owner beside the codes answers with the row's own sentence, bound.
        /// </para>
        /// </summary>
        private static (ProblemCode Code, string Message)? OutsideBounds(
            DialogDescriptorField field, string? value)
        {
            if (field.Minimum is null && field.Maximum is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(value)
                || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                return null;
            }

            return EditRefusalProblems.FieldBounds(field.Caption, field.Minimum, field.Maximum, number);
        }

        internal override void Execute(ProjectEditor editor)
        {
            foreach (ProductDialogEdit edit in Edits)
            {
                editor.Resolve(edit.Target, "Feltet").SetAttribute(edit.Attribute, Stored(editor, edit));
            }
        }

        /// <summary>
        /// What a field's on-screen value MEANS in the file — the write-side inverse of the composer's
        /// blank-at-declared-default read rule.
        /// <para>The composer shows a numeric field blank when it sits at its DTD default, because the original
        /// shows an empty box for "no PIN" and a literal <c>0</c> would read as a PIN of zero. Committing that box
        /// must therefore store the DEFAULT again, not an empty string: the two rules have to be inverses, or the
        /// value the installer was shown is not the value that comes back. The retired modem command encoded the
        /// same rule as <c>IsNullOrEmpty(PinCode) ? "0" : PinCode</c>; stated here it is the family-independent
        /// version, keyed on what the schema declares rather than on a literal — and it went missing for exactly
        /// as long as it took to delete that command (T031).</para>
        /// <para>Both directions read the declared default through the SAME helper, so the pair cannot drift into
        /// two different ideas of which defaults count.</para>
        /// </summary>
        private static string Stored(ProjectEditor editor, ProductDialogEdit edit)
        {
            if (edit.Value.Length > 0)
            {
                return edit.Value;
            }
            ProjectElement element = editor.Require(edit.Target);
            return ProductDialogComposer.NumericDeclaredDefault(editor.SchemaView, element, edit.Attribute)
                   ?? edit.Value;
        }
    }
}
