#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
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
        ImmutableArray<ProductDialogEdit> Edits,
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
            if (Edits.IsDefaultOrEmpty)
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

            var subtree = product.DescendantsAndSelf().Select(e => e.Id).Where(id => id is not null).ToHashSet();

            foreach (ProductDialogEdit edit in Edits)
            {
                if (context.Index.FindById(edit.Target) is null)
                {
                    return EditVerdict.Refuse("Et af felterne peger på et element, der ikke findes længere.");
                }
                if (!subtree.Contains(edit.Target))
                {
                    // Not a technicality: without it, a dialog could be handed an id belonging to a DIFFERENT
                    // product and would edit that one instead, reporting success either way.
                    return EditVerdict.Refuse("Et af felterne peger på et element uden for produktet.");
                }
                if (!offered.TryGetValue((edit.Target, edit.Attribute), out DialogDescriptorField? field))
                {
                    return EditVerdict.Refuse($"Produktets dialog har ikke feltet '{edit.Attribute}'.");
                }
                if (field.ReadOnly)
                {
                    return EditVerdict.Refuse($"Feltet '{field.Caption}' kan ikke redigeres.");
                }
                if (field.Rule is { } rule && !rule.IsSatisfiedBy(edit.Value))
                {
                    return EditVerdict.Refuse(rule.Refusal);
                }
            }
            return EditVerdict.Allow;
        }

        internal override void Execute(ProjectEditor editor)
        {
            foreach (ProductDialogEdit edit in Edits.IsDefaultOrEmpty ? [] : Edits)
            {
                editor.Resolve(edit.Target, "felt").SetAttribute(edit.Attribute, Stored(editor, edit));
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
