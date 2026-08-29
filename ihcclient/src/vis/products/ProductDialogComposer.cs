#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Products
{
    /// <summary>
    /// Turns a family preset plus one placed element into a <see cref="ProductDialogDescriptor"/> — the step that
    /// makes every downstream consumer family-agnostic.
    /// <para>Everything conditional happens HERE, once: repeats expand, bindings resolve to concrete ids, values
    /// are read effectively, numeric ranges are derived from the element, read-only is decided, automation ids are
    /// formed and the title is chosen. A renderer then draws what it is given and a write-back writes what it is
    /// given; neither asks a question about a family again.</para>
    /// </summary>
    internal static class ProductDialogComposer
    {
        /// <summary>The prefix every dialog automation id carries, so a screen-reader script can find dialog
        /// controls without knowing the family.</summary>
        public const string AutomationIdPrefix = "dlg";

        /// <summary>
        /// Composes the dialog of an ALREADY-RESOLVED placed product, selecting its preset from the element itself.
        /// <para>THE compose door — both the read side (<c>GetProductDialog</c>) and the write-back
        /// (<c>ApplyProductDialog</c>) come through here, which is what makes "the dialog is the contract" exact:
        /// the descriptor a commit validates against is composed by the same expression that composed the one the
        /// installer saw, so a future input to preset selection cannot reach one side only.</para>
        /// </summary>
        /// <param name="project">The project holding the placed product.</param>
        /// <param name="product">The placed product element.</param>
        /// <param name="displayName">The product's catalog type name — what the original titles the dialog with,
        /// rather than the element's possibly-renamed <c>name</c>.</param>
        public static ProductDialogDescriptor ComposeFor(
            Project project, ProjectElement product, string displayName) =>
            Compose(project, product,
                ProductDialogPresets.ForRootTag(product.Tag, product.GetAttribute("product_identifier")),
                displayName);

        /// <summary>
        /// Composes the dialog for <paramref name="productId"/> in <paramref name="project"/> against an
        /// explicitly supplied preset — the door a test uses to compose a product against a preset it did not
        /// come with. Production composes through <see cref="ComposeFor"/>, which cannot disagree with itself.
        /// </summary>
        public static ProductDialogDescriptor Compose(
            Project project, ElementId productId, ProductDialogModel preset, string displayName) =>
            Compose(
                project,
                project.FindById(productId)
                    ?? throw new ArgumentException($"No element with id {productId.ToToken()}.", nameof(productId)),
                preset,
                displayName);

        private static ProductDialogDescriptor Compose(
            Project project, ProjectElement product, ProductDialogModel preset, string displayName)
        {
            // The product's own subtree, walked ONCE: every presence gate, widget slot, repeat and binding below
            // resolves against this list rather than re-materializing the subtree per question.
            IReadOnlyList<ProjectElement> subtree = product.DescendantsAndSelf();

            // An unknown family still opens a dialog: the four attributes every known family declares, composed
            // from the same fragments. Insert is never blocked by a product the SDK has not met.
            if (preset.IsEmpty)
            {
                preset = MinimalFallback;
            }

            // The title is the catalog TYPE name plus whatever suffix the family declares — the modem's
            // " Egenskaber", and nothing for the other four. Read after the fallback swap, so an unknown family
            // is titled by the same rule rather than by a special case.
            string title = displayName + preset.TitleSuffix;

            // A locked product greys its NAME — and only its name. US-011: "Editability is gated by the placed
            // product's own `locked` attribute", said of the Name field; measured on every family, the original
            // disables Navn on a freshly inserted (locked) product while Note, Placering and the rest stay
            // editable. Applying `locked` to every field would make a just-inserted product's whole dialog
            // read-only, which is neither the story nor the original.
            //
            // A family that declares no `locked` attribute at all — the modem — cannot derive it, which is why
            // the preset ALSO carries a declared flag (proposal 4.2).
            bool lockedElement = project.SchemaView.TryGet(product.Tag)?.FindAttr("locked") is not null
                                 && project.View(product).Effective("locked") == "yes";

            FrozenDictionary<string, EquatableArray<string>> suggestions = GatherSuggestions(project, preset);

            var groups = new List<DialogDescriptorGroup>(preset.Groups.Length);
            foreach (DialogGroupModel group in preset.Groups)
            {
                // A group the family declares only for members that have the thing (the jalousi travel times).
                // Checked before composing anything in it, so its fields are never resolved for a product that
                // does not carry the tag — the alternative, letting each binding fail, renders identically but
                // hides a mistyped tag from the descriptor gate (T119).
                if (!group.Presence.IsPresentIn(subtree))
                {
                    continue;
                }

                var fields = new List<DialogDescriptorField>();
                var widgets = new List<DialogWidgetKind>();

                foreach (DialogPartModel part in group.Parts)
                {
                    switch (part)
                    {
                        case DialogFieldModel field:
                            if (Resolve(product, subtree, field.Binding) is { } resolved)
                            {
                                fields.Add(ComposeField(
                                    project, group, field, resolved, lockedElement, suggestions));
                            }
                            break;

                        case DialogRepeatModel repeat:
                            fields.AddRange(ExpandRepeat(project, subtree, group, repeat, lockedElement));
                            break;

                        case DialogWidgetModel widget when widget.Presence.IsPresentIn(subtree):
                            widgets.Add(widget.Kind);
                            break;
                    }
                }

                // An EMPTY group is dropped rather than rendered. A group can empty out legitimately: every field
                // in it bound to a tag this product lacks, or its only content was a widget slot whose presence
                // tag is absent. Rendering the leftover caption would show the installer a titled, empty box.
                if (fields.Count > 0 || widgets.Count > 0)
                {
                    groups.Add(new DialogDescriptorGroup(
                        group.Id, group.Caption, group.Columns, [.. fields], [.. widgets])
                    {
                        ColumnMajor = group.ColumnMajor,
                        Collapsible = group.Collapsible,
                    });
                }
            }

            return new ProductDialogDescriptor(title, [.. groups]);
        }

        /// <summary>
        /// The dialog an UNKNOWN family gets: the four attributes all five known families declare, captioned from
        /// the same shared fragments so the open-world case cannot drift away from the presets' wording.
        /// <para>Deliberately not a grammar walk. A fallback that captioned fields by raw attribute name would put
        /// English DTD identifiers on a Danish screen, and it could not satisfy any story that demands captioned
        /// groups. When an unknown family actually arrives, the answer is a sixth MEASURED preset — this exists so
        /// that arriving is not a crash in the meantime.</para>
        /// </summary>
        private static readonly ProductDialogModel MinimalFallback = ProductDialogFragments.Dialog(
            ProductDialogFragments.Group("identitet", null, 1,
                ProductDialogFragments.Navn(),
                ProductDialogFragments.Placering,
                ProductDialogFragments.Note,
                ProductDialogFragments.Identifikationskode));

        private static DialogDescriptorField ComposeField(
            Project project, DialogGroupModel group, DialogFieldModel field,
            (ProjectElement Element, string Attribute) resolved, bool lockedElement,
            FrozenDictionary<string, EquatableArray<string>> suggestions)
        {
            (int? min, int? max) = NumericRange(project, resolved.Element, field.Control);
            // The BOUNDS are scaled with the value. A field captioned in seconds over a millisecond attribute
            // must offer seconds bounds too, or it shows 5 in a box that refuses anything under 2000.
            if (field.DisplayDivisor > 1)
            {
                min = min / field.DisplayDivisor;
                max = max / field.DisplayDivisor;
            }
            return new DialogDescriptorField(
                AutomationId(group.Id, field.Id),
                field.Caption,
                field.Control,
                resolved.Element.Id!.Value,
                resolved.Attribute,
                Displayed(field, ReadValue(project, resolved.Element, resolved.Attribute, field.HidesUnresolvedResourceKey)),
                field.ReadOnly || (lockedElement && field.ReadOnlyWhenLocked),
                field.Rule,
                min,
                max,
                // default IS empty for the wrapper, so a field offering neither list needs no fallback of its own.
                // The two lists are different KINDS of answer sharing one carrier: a suggestion list is open and
                // gathered from the project, a fixed list is closed and IS the attribute's declaration.
                field.Control switch
                {
                    DialogControlKind.ComboSuggest => suggestions.GetValueOrDefault(resolved.Attribute),
                    DialogControlKind.ComboFixed => DeclaredTokens(project, resolved.Element, resolved.Attribute),
                    _ => default,
                })
            {
                ColumnSpan = field.ColumnSpan,
                DisplayDivisor = field.DisplayDivisor,
            };
        }

        /// <summary>
        /// The value a field SHOWS for a stored one — divided by the field's declared display divisor.
        /// <para>The exact inverse lives in the write-back (<c>ApplyProductDialog.Stored</c>) and reads the same
        /// declaration, so what the installer was shown is what comes back. A blank stays blank: it means "at the
        /// declared default", and scaling it would turn an absence into a zero.</para>
        /// </summary>
        private static string? Displayed(DialogFieldModel field, string? stored) =>
            field.DisplayDivisor > 1
            && !string.IsNullOrEmpty(stored)
            && int.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? (value / field.DisplayDivisor).ToString(CultureInfo.InvariantCulture)
                : stored;

        /// <summary>
        /// The tokens an enumerated attribute declares, in declaration order — the CLOSED list a
        /// <see cref="DialogControlKind.ComboFixed"/> field offers.
        /// <para>Read from the schema, never written down beside the field: the DTD is what decides which values
        /// the file can hold, and a second copy here would be a list that can disagree with the format.</para>
        /// </summary>
        private static EquatableArray<string> DeclaredTokens(
            Project project, ProjectElement element, string attribute) =>
            project.SchemaView.TryGet(element.Tag)?.FindAttr(attribute)?.EnumValues ?? default;

        /// <summary>
        /// The typing suggestions a <see cref="DialogControlKind.ComboSuggest"/> field offers: every value already
        /// used for that attribute ANYWHERE IN THE OPEN PROJECT, de-duplicated and sorted.
        /// <para>The project, deliberately, and not a machine-local history (D07). The original's combos remember
        /// what was typed on that installation, which means the same project offers different suggestions on a
        /// colleague's machine and none at all on a fresh one. Reading the project makes the list a property of
        /// the work rather than of the workstation, and makes it reproducible in a test.</para>
        /// <para>Always an open combo: the list is a typing aid, never a constraint. A value used nowhere yet is
        /// still typeable, which is what keeps a suggestion list from silently becoming an enumeration.</para>
        /// <para>Gathered in ONE pass over the project for ALL of the preset's suggesting attributes at once, keyed
        /// by attribute. The modem offers seven such fields and a project walk materializes the whole tree, so
        /// asking per field re-walked a 1300-element project seven times to open one dialog.</para>
        /// </summary>
        private static FrozenDictionary<string, EquatableArray<string>> GatherSuggestions(
            Project project, ProductDialogModel preset)
        {
            var wanted = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (DialogGroupModel group in preset.Groups)
            {
                foreach (DialogPartModel part in group.Parts)
                {
                    if (part is DialogFieldModel { Control: DialogControlKind.ComboSuggest } field)
                    {
                        wanted[field.Binding.AttributeName] = new SortedSet<string>(StringComparer.Ordinal);
                    }
                }
            }

            // No suggesting field in the preset — four of the five families — so the project walk never starts.
            if (wanted.Count == 0)
            {
                return FrozenDictionary<string, EquatableArray<string>>.Empty;
            }

            // Attribute-bag-outer, wanted-inner: GetAttribute is a linear scan of the bag, so asking it once per
            // wanted attribute re-scanned each element's bag N times. One pass per element regardless of N.
            foreach (ProjectElement element in project.Root.DescendantsAndSelf())
            {
                foreach ((string name, string value) in element.Attrs)
                {
                    if (wanted.TryGetValue(name, out SortedSet<string>? values) && !string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }
            }

            return wanted.ToFrozenDictionary(
                entry => entry.Key,
                entry => (EquatableArray<string>)ImmutableArray.CreateRange(entry.Value),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// Expands a repeat over the element's matching DESCENDANTS, ordered by the numeric value of the key.
        /// <para>Descendants, not children: the modem's 30 <c>sms_modem_phonenumber</c> elements hang off three
        /// <c>sms_modem_settings</c> containers, so a child-scoped walk finds NONE of them and the dialog silently
        /// loses its whole telephone section. Numeric order, not string order: string order puts slot 10 straight
        /// after slot 1.</para>
        /// </summary>
        private static IEnumerable<DialogDescriptorField> ExpandRepeat(
            Project project, IReadOnlyList<ProjectElement> subtree, DialogGroupModel group, DialogRepeatModel repeat,
            bool lockedElement)
        {
            foreach (ProjectElement item in subtree
                         .Where(e => e.Tag == repeat.DescendantTag && e.Id is not null)
                         .OrderBy(e => KeyOrder(e.GetAttribute(repeat.KeyAttribute))))
            {
                string key = item.GetAttribute(repeat.KeyAttribute) ?? string.Empty;
                (int? min, int? max) = NumericRange(project, item, repeat.Control);
                yield return new DialogDescriptorField(
                    AutomationId(group.Id, repeat.Id + "." + key),
                    string.Format(CultureInfo.InvariantCulture, repeat.CaptionPattern, key),
                    repeat.Control,
                    item.Id!.Value,
                    repeat.ValueAttribute,
                    // A repeat expands over value-bearing descendants — the modem's phone numbers — and no
                    // catalog ships a localisation key in one, so no repeat claims the rule.
                    ReadValue(project, item, repeat.ValueAttribute, hidesUnresolvedResourceKey: false),
                    lockedElement,
                    repeat.Rule,
                    min,
                    max);
            }
        }

        // An unparseable or absent key sorts last rather than throwing — a foreign file may carry one, and losing
        // the whole dialog over it would be worse than showing that row at the end.
        private static int KeyOrder(string? key) =>
            int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : int.MaxValue;

        private static (ProjectElement Element, string Attribute)? Resolve(
            ProjectElement product, IReadOnlyList<ProjectElement> subtree, DialogBinding binding) => binding switch
            {
                DialogBinding.RootAttribute root => (product, root.Name),
                DialogBinding.DescendantAttribute d =>
                    subtree.FirstOrDefault(e => e.Tag == d.Tag && e.Id is not null) is { } found
                        ? (found, d.Attribute)
                        : null,
                _ => null,
            };

        /// <summary>
        /// The value to SHOW. Effective (the attribute, or its DTD default) — with one presentation rule: a numeric
        /// field sitting at its DTD default shows BLANK.
        /// <para>That rule exists for the SIM PIN, whose DTD default is <c>0</c>: the original shows an empty box
        /// for "no PIN", and rendering a literal 0 would read as a PIN of zero. The rule is expressed against the
        /// declared default rather than against the literal "0", so it stays right if a catalog changes it.</para>
        /// </summary>
        private static string? ReadValue(
            Project project, ProjectElement element, string attribute, bool hidesUnresolvedResourceKey)
        {
            string? effective = project.View(element).Effective(attribute);
            if (hidesUnresolvedResourceKey && IsUnresolvedResourceKey(effective))
            {
                return string.Empty;
            }
            return effective is not null
                   && effective == NumericDeclaredDefault(project.SchemaView, element, attribute)
                ? string.Empty
                : effective;
        }

        /// <summary>
        /// The attribute's declared DTD default when that default is NUMERIC — the single datum the blank-at-default
        /// read rule above and its write-side inverse (<c>ApplyProductDialog.Stored</c>) both key on. Null when the
        /// attribute declares no default, or declares a non-numeric one.
        /// <para>Shared deliberately: the two rules have to be exact inverses, or the value the installer was shown
        /// is not the value that comes back. Stated twice, nothing structural kept them so.</para>
        /// </summary>
        internal static string? NumericDeclaredDefault(
            ProjectSchemaView schema, ProjectElement element, string attribute)
        {
            string? declaredDefault = schema.TryGet(element.Tag)?.FindAttr(attribute)?.Default;
            return declaredDefault is { Length: > 0 } && IsNumeric(declaredDefault) ? declaredDefault : null;
        }

        /// <summary>
        /// Whether a value LOOKS like one of the vendor's own localisation keys rather than text to show. Asked
        /// only of a field that declares it can hold one (<see cref="DialogFieldModel.HidesUnresolvedResourceKey"/>,
        /// carried by the shared <c>Note</c> fragment) — shape alone never decides, because a documentation tag
        /// like <c>A_1</c> is a legitimate value of exactly this shape.
        /// <para>The key is deliberately still WRITTEN: a vendor-authored <c>.vis</c> stores it verbatim, so
        /// hiding it is a presentation rule and touching the stored value would break byte fidelity.</para>
        /// <para><b>The predicate was measured, not guessed.</b> Across all 100 catalog notes exactly two are
        /// all-capitals — <c>PIR</c> and <c>PRODUCT_2315_NOTE</c> — and the separator is what tells a key from
        /// prose. Keyed on shape rather than on the literal string, so a second <c>.def</c> written to the same
        /// convention is handled.</para>
        /// </summary>
        private static bool IsUnresolvedResourceKey(string? value) =>
            value is { Length: > 0 }
            && value.Contains('_')
            && value.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_');

        private static bool IsNumeric(string value) =>
            value.Length > 0 && value.All(char.IsAsciiDigit);

        /// <summary>
        /// The numeric bounds a <see cref="DialogControlKind.Number"/> field enforces, read off the TARGET
        /// element's own <c>minimum</c>/<c>maximum</c> attributes. Derived, never declared in a preset: the
        /// catalog seeds the modem's PIN with 0–9999, and a preset that hardcoded that would go stale silently the
        /// moment a catalog changed it.
        /// </summary>
        private static (int? Min, int? Max) NumericRange(Project project, ProjectElement element, DialogControlKind control)
        {
            if (control != DialogControlKind.Number)
            {
                return (null, null);
            }
            return project.View(element).DeclaredBounds;
        }

        /// <summary>The stable id a renderer stamps on the control: <c>dlg.&lt;groupId&gt;.&lt;fieldId&gt;</c>.</summary>
        public static string AutomationId(string groupId, string fieldId) =>
            AutomationIdPrefix + "." + groupId + "." + fieldId;
    }
}
