#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

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
        /// The families whose dialog the original titles <c>"&lt;name&gt; Egenskaber"</c>. Every other family is
        /// titled with the bare product name — measured across all 100 catalog products (2026-08-11), where only
        /// the modem carries the suffix. A single rule would have been wrong for 99 or for 1.
        /// </summary>
        private static bool TitleTakesEgenskaberSuffix(string rootTag) => rootTag == "product_rs485_sms_modem";

        /// <summary>
        /// Composes the dialog for <paramref name="productId"/> in <paramref name="project"/>.
        /// </summary>
        /// <param name="project">The project holding the placed product.</param>
        /// <param name="productId">The placed product's element id.</param>
        /// <param name="preset">Its family's preset, from <see cref="ProductDialogPresets.ForRootTag"/>.</param>
        /// <param name="displayName">The product's catalog type name — what the original titles the dialog with,
        /// rather than the element's possibly-renamed <c>name</c>.</param>
        public static ProductDialogDescriptor Compose(
            Project project, ElementId productId, ProductDialogModel preset, string displayName)
        {
            ProjectElement product = project.FindById(productId)
                ?? throw new ArgumentException($"No element with id {productId.ToToken()}.", nameof(productId));

            string title = TitleTakesEgenskaberSuffix(product.Tag) ? displayName + " Egenskaber" : displayName;

            // An unknown family still opens a dialog: the four attributes every known family declares, composed
            // from the same fragments. Insert is never blocked by a product the SDK has not met.
            if (preset.IsEmpty)
            {
                preset = MinimalFallback;
            }

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

            var groups = new List<DialogDescriptorGroup>(preset.Groups.Length);
            foreach (DialogGroupModel group in preset.Groups)
            {
                // A group the family declares only for members that have the thing (the jalousi travel times).
                // Checked before composing anything in it, so its fields are never resolved for a product that
                // does not carry the tag — the alternative, letting each binding fail, renders identically but
                // hides a mistyped tag from the descriptor gate (T119).
                if (group.PresenceTag is { } required
                    && !product.DescendantsAndSelf().Any(e => e.Tag == required))
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
                            if (Resolve(project, product, field.Binding) is { } resolved)
                            {
                                fields.Add(ComposeField(project, group, field, resolved, lockedElement));
                            }
                            break;

                        case DialogRepeatModel repeat:
                            fields.AddRange(ExpandRepeat(project, product, group, repeat, lockedElement));
                            break;

                        case DialogWidgetModel widget when AppliesTo(product, widget):
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

        /// <summary>
        /// Which field a locked product greys: the one bound to its own <c>name</c>, and no other. Expressed
        /// against the BINDING rather than a field id, so a preset that names the field differently still gets the
        /// rule and a preset that binds something else to `name` cannot escape it.
        /// </summary>
        private static bool GatedByLocked(DialogBinding binding) =>
            binding is DialogBinding.RootAttribute { Name: "name" };

        // A widget slot renders when it names no presence tag, or when the element actually carries one.
        //
        // The settings grid is the exception, and its presence rule belongs to the KIND rather than to a
        // tag: a setting is any resource marked `setting="yes"`, whatever its resource type (the sensors
        // use resource_temperature, resource_humidity and resource_light). Unlike the terminal grids --
        // which the vendor shows ALWAYS, empty or not (US-012) -- it draws Indstillinger only where there
        // are settings, measured across the products that have none (T070).
        private static bool AppliesTo(ProjectElement product, DialogWidgetModel widget) =>
            widget.Kind == DialogWidgetKind.SettingsGrid
                ? product.DescendantsAndSelf().Any(IsSetting)
                : widget.PresenceTag is not { } tag
                  || product.DescendantsAndSelf().Any(e => e.Tag == tag);

        /// <summary>A configurable setting: a resource the catalog marked <c>setting="yes"</c>.</summary>
        internal static bool IsSetting(ProjectElement element) =>
            element.GetAttribute("setting") == "yes";

        private static DialogDescriptorField ComposeField(
            Project project, DialogGroupModel group, DialogFieldModel field,
            (ProjectElement Element, string Attribute) resolved, bool lockedElement)
        {
            (int? min, int? max) = NumericRange(project, resolved.Element, field.Control);
            return new DialogDescriptorField(
                AutomationId(group.Id, field.Id),
                field.Caption,
                field.Control,
                resolved.Element.Id!.Value,
                resolved.Attribute,
                ReadValue(project, resolved.Element, resolved.Attribute),
                field.ReadOnly || (lockedElement && GatedByLocked(field.Binding)),
                field.Rule,
                min,
                max,
                Suggestions(project, field.Control, resolved.Attribute))
            {
                ColumnSpan = field.ColumnSpan,
            };
        }

        /// <summary>
        /// The typing suggestions a <see cref="DialogControlKind.ComboSuggest"/> field offers: every value already
        /// used for that attribute ANYWHERE IN THE OPEN PROJECT, de-duplicated and sorted.
        /// <para>The project, deliberately, and not a machine-local history (D07). The original's combos remember
        /// what was typed on that installation, which means the same project offers different suggestions on a
        /// colleague's machine and none at all on a fresh one. Reading the project makes the list a property of
        /// the work rather than of the workstation, and makes it reproducible in a test.</para>
        /// <para>Always an open combo: the list is a typing aid, never a constraint. A value used nowhere yet is
        /// still typeable, which is what keeps a suggestion list from silently becoming an enumeration.</para>
        /// </summary>
        private static ImmutableArray<string> Suggestions(Project project, DialogControlKind control, string attribute)
        {
            if (control != DialogControlKind.ComboSuggest)
            {
                return ImmutableArray<string>.Empty;
            }
            return
            [
                .. project.Root.DescendantsAndSelf()
                    .Select(e => e.GetAttribute(attribute))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(v => v, StringComparer.Ordinal),
            ];
        }

        /// <summary>
        /// Expands a repeat over the element's matching DESCENDANTS, ordered by the numeric value of the key.
        /// <para>Descendants, not children: the modem's 30 <c>sms_modem_phonenumber</c> elements hang off three
        /// <c>sms_modem_settings</c> containers, so a child-scoped walk finds NONE of them and the dialog silently
        /// loses its whole telephone section. Numeric order, not string order: string order puts slot 10 straight
        /// after slot 1.</para>
        /// </summary>
        private static IEnumerable<DialogDescriptorField> ExpandRepeat(
            Project project, ProjectElement product, DialogGroupModel group, DialogRepeatModel repeat,
            bool lockedElement)
        {
            foreach (ProjectElement item in product.DescendantsAndSelf()
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
                    ReadValue(project, item, repeat.ValueAttribute),
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
            Project project, ProjectElement product, DialogBinding binding) => binding switch
            {
                DialogBinding.RootAttribute root => (product, root.Name),
                DialogBinding.DescendantAttribute d =>
                    product.DescendantsAndSelf().FirstOrDefault(e => e.Tag == d.Tag && e.Id is not null) is { } found
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
        private static string? ReadValue(Project project, ProjectElement element, string attribute)
        {
            string? effective = project.View(element).Effective(attribute);
            if (attribute == "note" && IsUnresolvedResourceKey(effective))
            {
                return string.Empty;
            }
            string? declaredDefault = project.SchemaView.TryGet(element.Tag)?.FindAttr(attribute)?.Default;
            return effective is not null && effective == declaredDefault && IsNumeric(effective)
                ? string.Empty
                : effective;
        }

        /// <summary>
        /// Whether a note is one of the vendor's own LOCALISATION KEYS rather than text to show.
        /// <para>Exactly one catalog product has one: the S0 device's <c>.def</c> says
        /// <c>note="PRODUCT_2315_NOTE"</c>, and nothing in the IHC Visual install resolves that key — so the
        /// original's Note box is empty, while OpenVisual printed the token at the installer (T131). The key
        /// is deliberately still WRITTEN: a vendor-authored <c>.vis</c> stores it verbatim, so hiding it is a
        /// presentation rule and touching the stored value would break byte fidelity.</para>
        /// <para><b>The predicate was measured, not guessed.</b> Across all 100 catalog notes exactly two are
        /// all-capitals — <c>PIR</c> and <c>PRODUCT_2315_NOTE</c> — and the separator is what tells a key from
        /// prose. Keyed on shape rather than on the literal string so a second <c>.def</c> written to the same
        /// convention is handled, and confined to <c>note</c> because that is where the catalog puts keys: a
        /// documentation tag like <c>A_1</c> is a legitimate value of exactly this shape.</para>
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
            ElementView view = project.View(element);
            return (ParseBound(view.Effective("minimum")), ParseBound(view.Effective("maximum")));
        }

        private static int? ParseBound(string? raw) =>
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : null;

        /// <summary>The stable id a renderer stamps on the control: <c>dlg.&lt;groupId&gt;.&lt;fieldId&gt;</c>.</summary>
        public static string AutomationId(string groupId, string fieldId) =>
            AutomationIdPrefix + "." + groupId + "." + fieldId;
    }
}
