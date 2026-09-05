using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ihc.Vis.Addressing;
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
    /// One terminal's addressing and documentation, as edited during a product-dialog visit.
    /// </summary>
    /// <param name="PinId">The terminal the values belong to.</param>
    /// <param name="Values">What the terminal editor would have committed.</param>
    public readonly record struct ProductDialogTerminalEdit(ElementId PinId, PinPropertiesResult Values);

    /// <summary>
    /// One configurable SETTING's new value, as edited in <i>Rediger konstant</i> during a product-dialog visit.
    /// </summary>
    /// <param name="SettingId">The flagged setting resource the value belongs to.</param>
    /// <param name="Value">
    /// Its new initial value, TYPED — the same payload the variable command takes, so every resource kind a
    /// setting can be is expressible and there is one writer for all of them.
    /// <para>Omit-if-default needs nothing here: a value equal to the type's declared default is elided by the
    /// serializer (<c>AttrSchema.OmitsOnWrite</c>), so returning a calibration to zero removes the attribute and
    /// the file goes back to the bytes it had.</para>
    /// </param>
    public readonly record struct ProductDialogSettingEdit(ElementId SettingId, ResourceInitialValue Value);

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
    /// <para>The widget action is CARRIED, never executed. The composite sub-dialogs keep their own
    /// commands and flows (D05); this slot only tells the caller which one the installer asked for, so the
    /// decision travels with the commit instead of beside it.</para>
    /// </summary>
    public sealed record ApplyProductDialog(
        ElementId ProductId,
        EquatableArray<ProductDialogEdit> Edits,
        ProductDialogWidgetAction? WidgetAction = null) : ProjectCommand
    {
        /// <summary>
        /// The terminals this VISIT addressed, committed by the same invocation as the field edits above.
        /// <para>One command, so one undo entry: the installer performed one act — opened the dialog, stepped
        /// into a terminal, came back out through OK — and <i>Fortryd</i> takes back all of it. Empty for a
        /// dialog visit that never stepped into a terminal, which is most of them.</para>
        /// </summary>
        /// <remarks>
        /// An INIT-ONLY property rather than a fourth primary-constructor parameter. The three-parameter
        /// constructor and the matching <c>Deconstruct</c> are shipped public API; widening the primary
        /// constructor replaces both signatures, which breaks every existing caller for no gain — an added
        /// member does the same job additively.
        /// </remarks>
        public EquatableArray<ProductDialogTerminalEdit> TerminalEdits { get; init; }

        /// <summary>
        /// The constants this VISIT edited, committed by the same invocation — the settings half of the same rule
        /// the terminals follow: one act, one undo entry.
        /// </summary>
        /// <remarks>
        /// An init-only property for the same reason <see cref="TerminalEdits"/> is one: the three-parameter
        /// constructor and its <c>Deconstruct</c> are shipped public API.
        /// </remarks>
        public EquatableArray<ProductDialogSettingEdit> SettingEdits { get; init; }

        internal override string Describe(Project project) => "Rediger produkt";

        internal override EditVerdict Evaluate(EditContext context)
        {
            EditVerdict exists = context.RequireExists(ProductId, "Produktet");
            if (!exists.Ok)
            {
                return exists;
            }
            if (Edits.IsEmpty && TerminalEdits.IsEmpty && SettingEdits.IsEmpty)
            {
                // Nothing to validate at all, and nothing to walk the subtree for.
                return EditVerdict.Allow;
            }

            ProjectElement product = context.Index.FindById(ProductId)!;

            // ONE walk of the product's subtree for all three halves of the visit. "Does this id belong to this
            // product" is the same question whether it is asked of a field, a terminal or a constant, so the
            // answer is built once here rather than per half.
            var subtree = new HashSet<ElementId>();
            ProjectTreeOps.CollectIds(product, subtree);

            // BEFORE the empty-edits shortcut below: a visit that changed no product FIELD may still have
            // addressed a terminal, and that half has to be validated either way.
            EditVerdict terminals = EvaluateTerminals(context, subtree);
            if (!terminals.Ok)
            {
                return terminals;
            }
            EditVerdict settings = EvaluateSettings(context, subtree);
            if (!settings.Ok)
            {
                return settings;
            }

            if (Edits.IsEmpty)
            {
                // OK without touching a field is an ordinary act — and the commonest one, since a just-inserted
                // product raises its dialog. There is nothing to validate against, so the descriptor (a whole-
                // project compose) is never built for it.
                return EditVerdict.Allow;
            }

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
        /// The terminal half of the visit: every edited terminal must still exist, belong to THIS product, and
        /// carry an address the data line can express.
        /// </summary>
        /// <remarks>
        /// The subtree test is the same one the field edits get, and for the same reason: without it a visit
        /// could address a terminal belonging to a different product and report success either way. The address
        /// check is the terminal editor's own rule, restated here because this command commits what that editor
        /// would have committed — a visit must not become a way to write an address the editor would refuse.
        /// </remarks>
        private EditVerdict EvaluateTerminals(EditContext context, HashSet<ElementId> subtree)
        {
            if (TerminalEdits.IsEmpty)
            {
                return EditVerdict.Allow;
            }

            foreach (ProductDialogTerminalEdit edit in TerminalEdits)
            {
                if (context.Index.FindById(edit.PinId) is not { } pin)
                {
                    return EditVerdict.Refuse(EditRefusalCodes.TerminalMissing, "Klemmen findes ikke længere.");
                }
                if (!subtree.Contains(edit.PinId))
                {
                    return EditVerdict.Refuse(EditRefusalCodes.FieldOutsideProduct,
                        "Et af felterne peger på et element uden for produktet.");
                }
                if (!DatalineAddress.TryEncode(
                        edit.Values.DataLine, edit.Values.Terminal, pin.Tag == "dataline_output", out _))
                {
                    return EditVerdict.Refuse(EditRefusalCodes.TerminalAddressRange,
                        "Klemmenummeret ligger uden for datalinjens område.");
                }
            }
            return EditVerdict.Allow;
        }

        /// <summary>
        /// The settings half of the visit: every edited constant must still exist, belong to THIS product, and be
        /// a resource the catalog actually marked as a configurable setting.
        /// </summary>
        /// <remarks>
        /// The third test is what the terminals do not need. A terminal is identified by its tag, so a wrong id
        /// fails the subtree test or is not a pin; a setting is an ordinary resource wearing a
        /// <c>setting="yes"</c> marker, and every product is full of resources that are NOT settings. Without the
        /// marker test this command would be a way to write <c>inivalue</c> on any resource inside a product
        /// through a dialog that never offered it.
        /// </remarks>
        private EditVerdict EvaluateSettings(EditContext context, HashSet<ElementId> subtree)
        {
            if (SettingEdits.IsEmpty)
            {
                return EditVerdict.Allow;
            }

            foreach (ProductDialogSettingEdit edit in SettingEdits)
            {
                if (context.Index.FindById(edit.SettingId) is not { } setting)
                {
                    return EditVerdict.Refuse(EditRefusalCodes.FieldTargetMissing,
                        "Et af felterne peger på et element, der ikke findes længere.");
                }
                if (!subtree.Contains(edit.SettingId))
                {
                    return EditVerdict.Refuse(EditRefusalCodes.FieldOutsideProduct,
                        "Et af felterne peger på et element uden for produktet.");
                }
                if (!Schema.ProductRows.IsSetting(setting.GetAttribute(Schema.ProductRows.SettingAttribute)))
                {
                    return EditVerdict.Refuse(EditRefusalCodes.TargetWrongKind,
                        EditRefusalProblems.TargetWrongKindRefusal(SettingNoun));
                }
            }
            return EditVerdict.Allow;
        }

        /// <summary>What the wrong-kind refusal names when a settings edit points at something else.</summary>
        private const string SettingNoun = "en indstilling";

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
        /// Which coded refusal a numeric field's value earns — either because it is not a number at all, or
        /// because it falls outside the bounds its own catalog element declares. Null when neither holds.
        /// <para>
        /// The identity and the sentence BOTH come from <see cref="EditRefusalProblems"/> (D05). This site used
        /// to author four sentences of its own, one per bound shape, under a single code whose catalogue row
        /// declared a template none of them matched — so the row described words no user saw. The site now
        /// decides nothing about wording: it reports which bounds the field declares and what was submitted, and
        /// the owner beside the codes answers with the row's own sentence, bound.
        /// </para>
        /// <para>
        /// The declared bound is also what makes the field NUMERIC: it is the catalog stating the element holds
        /// a number. Text that is not one used to fall through here as "no bounds violation" and be written into
        /// the project verbatim — the dialog's own NumericUpDown cannot produce it, but the command is a public
        /// door. A BLANK value is still not this condition: blank means "at the declared default", and
        /// committing it writes the default back.
        /// </para>
        /// </summary>
        private static (ProblemCode Code, string Message)? OutsideBounds(
            DialogDescriptorField field, string? value)
        {
            if (field.Minimum is null && field.Maximum is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                return EditRefusalProblems.FieldNotANumber(field.Caption, value);
            }

            return EditRefusalProblems.FieldBounds(field.Caption, field.Minimum, field.Maximum, number);
        }

        internal override void Execute(ProjectEditor editor)
        {
            // The dialog this product shows is the authority on each field's display scale, exactly as it is the
            // authority on which fields exist — so the write-back asks it rather than carrying a second copy.
            Dictionary<(ElementId, string), int> divisors = [];
            if (!Edits.IsEmpty)
            {
                Project current = editor.ToProject();
                ProjectElement product = current.FindById(ProductId)!;
                foreach (DialogDescriptorField offered in
                    ProductDialogComposer.ComposeFor(current, product, product.Tag).AllFields)
                {
                    divisors[(offered.Target, offered.Attribute)] = offered.DisplayDivisor;
                }
            }

            foreach (ProductDialogEdit edit in Edits)
            {
                editor.Resolve(edit.Target, "Feltet").SetAttribute(
                    edit.Attribute,
                    Stored(editor, edit, divisors.GetValueOrDefault((edit.Target, edit.Attribute), 1)));
            }

            // The terminals the visit addressed, committed by the SAME command — which is what makes the visit
            // one undo entry. Fortryd after the dialog's OK takes back the addressing too, because the installer
            // performed one act.
            foreach (ProductDialogTerminalEdit edit in TerminalEdits)
            {
                WriteTerminal(editor, edit);
            }

            // The constants, by the same rule and through the SAME typed writer the variable command uses — so a
            // calibration and a variable of the same resource type produce the same bytes, and returning either
            // to its declared default leaves the attribute out of the file.
            foreach (ProductDialogSettingEdit edit in SettingEdits)
            {
                edit.Value.WriteTo(editor.Resolve(edit.SettingId, "Indstillingen"));
            }
        }

        /// <summary>Writes one terminal exactly as the terminal editor's own command would.</summary>
        private static void WriteTerminal(ProjectEditor editor, ProductDialogTerminalEdit edit)
        {
            ElementRef handle = editor.Resolve(edit.PinId, "Klemmen");
            bool isOutput = handle.Tag == "dataline_output";
            if (!DatalineAddress.TryEncode(edit.Values.DataLine, edit.Values.Terminal, isOutput, out string token))
            {
                throw new EditRefusedException(
                    EditRefusalCodes.TerminalAddressRange,
                    "Klemmenummeret ligger uden for datalinjens område.");
            }
            handle.SetAttribute("address_dataline", token);
            handle.SetAttribute("cable_colour", edit.Values.CableColour);
            handle.SetAttribute("note", edit.Values.Note);
            if (isOutput)
            {
                handle.SetAttribute("inivalue", edit.Values.InitialValueOn ? "on" : "off");
                handle.SetAttribute("backup", edit.Values.SaveOnPowerFailure ? "yes" : "no");
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
        private static string Stored(ProjectEditor editor, ProductDialogEdit edit, int displayDivisor)
        {
            if (edit.Value.Length > 0)
            {
                // The read side's exact inverse: a field whose caption is in a different unit from the file
                // showed the stored value divided, so a committed one is multiplied back. Both ends read the
                // same declaration; a scale applied at one only is a value that drifts every time the dialog
                // is opened and closed.
                return displayDivisor > 1
                    && int.TryParse(edit.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int shown)
                        ? (shown * displayDivisor).ToString(CultureInfo.InvariantCulture)
                        : edit.Value;
            }
            ProjectElement element = editor.Require(edit.Target);
            return ProductDialogComposer.NumericDeclaredDefault(editor.SchemaView, element, edit.Attribute)
                   ?? edit.Value;
        }
    }
}
