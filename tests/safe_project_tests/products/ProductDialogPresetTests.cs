using System.Linq;
using Ihc.Vis.Products;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The family presets say what each dialog contains: group ids and captions, field ids and captions, and the
    /// order the installer reads them in. All three are CONTENT under D18, so all three are pinned.
    /// <para>Shapes come from the recorded vendor oracle (2026-08-11), not from the DTD — the DTD says which
    /// attributes exist, the dialog says which of them are offered, and those sets differ.</para>
    /// </summary>
    public class ProductDialogPresetTests
    {
        private static DialogGroupModel Group(ProductDialogModel model, string id) =>
            model.Groups.Single(g => g.Id == id);

        private static string[] FieldIds(DialogGroupModel group) =>
            [.. group.Parts.OfType<DialogFieldModel>().Select(f => f.Id)];

        private static string[] Captions(DialogGroupModel group) =>
            [.. group.Parts.OfType<DialogFieldModel>().Select(f => f.Caption)];

        // ── Dataline ────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Dataline_HasTheMeasuredGroupsInOrder()
        {
            Assert.That(ProductDialogPresets.Dataline.Groups.Select(g => (g.Id, g.Caption)),
                Is.EqualTo(new[] { ("identitet", "Produkt egenskaber"), ("terminaler", (string?)null) }).AsCollection);
        }

        [Test]
        public void Dataline_IdentityGroup_HasTheMeasuredFieldsInReadingOrder()
        {
            DialogGroupModel identity = Group(ProductDialogPresets.Dataline, "identitet");
            Assert.Multiple(() =>
            {
                Assert.That(FieldIds(identity), Is.EqualTo(
                    new[] { "navn", "placering", "note", "kabeltype", "kabelnummer", "idkode", "lysgruppe" }).AsCollection);
                Assert.That(Captions(identity), Is.EqualTo(new[]
                {
                    "Navn", "Placering", "Note", "Kabeltype", "Kabelnummer", "Identifikationskode", "Lysgruppe",
                }).AsCollection);
                Assert.That(identity.Columns, Is.EqualTo(2), "the vendor lays this group out in two columns");
            });
        }

        [Test]
        public void Dataline_CarriesTheTerminalGridsWidget()
        {
            // Two widget slots since T070: the terminal grids, which the vendor always shows, and the
            // sensors' Indstillinger grid, which it shows only where the product HAS settings. Both live
            // in this group because the vendor draws both beneath the identity box.
            var widgets = Group(ProductDialogPresets.Dataline, "terminaler")
                .Parts.OfType<DialogWidgetModel>().Select(w => w.Kind).ToList();
            Assert.That(widgets,
                Is.EqualTo(new[] { DialogWidgetKind.TerminalGrids, DialogWidgetKind.SettingsGrid }).AsCollection);
        }

        // ── Airlink ─────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Airlink_HasOneGroupWithTheMeasuredFields()
        {
            DialogGroupModel identity = Group(ProductDialogPresets.Airlink, "identitet");
            Assert.Multiple(() =>
            {
                // One box of fields that EVERY wireless product gets. The preset has two other groups, both
                // presence-gated and both applying to a handful of members: the Avanceret slot (dimmers, T030,
                // no field to type into) and Persienne egenskaber (the two jalousi products' travel times,
                // T119). The T008 measurement — "all 24 share one field set" — is a statement about the
                // ungated groups, so that is how it is asserted; the earlier form counted gated groups too and
                // would have refused any family-optional field for ever.
                Assert.That(
                    ProductDialogPresets.Airlink.Groups.Count(
                        g => g.PresenceTag is null && g.Parts.OfType<DialogFieldModel>().Any()),
                    Is.EqualTo(1), "measured: every wireless product gets one Produkt egenskaber box");
                Assert.That(identity.Caption, Is.EqualTo("Produkt egenskaber"));
                Assert.That(FieldIds(identity), Is.EqualTo(
                    new[] { "navn", "placering", "note", "idkode", "lysgruppe" }).AsCollection);
                Assert.That(identity.Columns, Is.EqualTo(2));
            });
        }

        /// <summary>
        /// The <i>Avanceret</i> slot is PRESENCE-GATED, so it reaches the dimmers and nothing else. Without the
        /// gate every airlink product — a push-button, a sensor — would offer dimmer settings it does not have.
        /// <para>The button is PARITY — the vendor draws one captioned <i>Avanceret</i> on the dimmer's own
        /// bottom row (product 080, T114, correcting an earlier claim that no capture carried that caption).
        /// What stays a registered DIFFERENCE is what pressing it does: the vendor expands in place, this
        /// opens a sub-dialog. Declared so that T030's routing did not silently delete a reachable
        /// capability; reshaping it to the in-place form is separate work.</para>
        /// </summary>
        [Test]
        public void Airlink_OffersAdvancedDimmerSettings_OnlyWhereDimmerSettingsExist()
        {
            DialogWidgetModel widget = ProductDialogPresets.Airlink.Groups
                .SelectMany(g => g.Parts).OfType<DialogWidgetModel>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(widget.Kind, Is.EqualTo(DialogWidgetKind.AdvancedDimmerButton));
                Assert.That(widget.PresenceTag, Is.EqualTo("dimmer_settings"),
                    "gated, so a wireless push-button is not offered dimmer settings");
            });
        }

        /// <summary>
        /// T008 measured that <c>serialnumber</c> is not surfaced in the vendor's wireless dialog in any form.
        /// A preset that offered it would invent a field the original does not have.
        /// </summary>
        [Test]
        public void Airlink_DoesNotOfferSerialNumber()
        {
            var bindings = ProductDialogPresets.Airlink.Groups
                .SelectMany(g => g.Parts).OfType<DialogFieldModel>()
                .Select(f => f.Binding).OfType<DialogBinding.RootAttribute>()
                .Select(b => b.Name);

            Assert.That(bindings, Does.Not.Contain("serialnumber"));
        }

        /// <summary>Wireless products have no cable, so the two cabling fields must be absent — that is the seam.</summary>
        [Test]
        public void Airlink_IsDatalineMinusTheCablingFields()
        {
            string[] wired = FieldIds(Group(ProductDialogPresets.Dataline, "identitet"));
            string[] wireless = FieldIds(Group(ProductDialogPresets.Airlink, "identitet"));

            Assert.That(wired.Except(wireless), Is.EqualTo(new[] { "kabeltype", "kabelnummer" }).AsCollection);
        }

        // ── Rs485SmsModem ───────────────────────────────────────────────────────────────────────────

        [Test]
        public void Modem_HasTheFourMeasuredGroupsInOrder()
        {
            Assert.That(ProductDialogPresets.Rs485SmsModem.Groups.Select(g => (g.Id, g.Caption, g.Columns)),
                Is.EqualTo(new[]
                {
                    ("identitet", "Modem egenskaber", 1),
                    ("kabling", "Kabling", 1),
                    ("indstillinger", "Indstillinger", 1),
                    ("telefonnumre", "Telefon numre", 3),
                }).AsCollection);
        }

        /// <summary>
        /// The modem orders its identity fields Navn · Note · Placering — <c>Note</c> BEFORE <c>Placering</c>,
        /// the opposite of every other family. Measured, and the reason this group is composed explicitly rather
        /// than reusing the wired ordering.
        /// </summary>
        [Test]
        public void Modem_OrdersNoteBeforePlacering_UnlikeEveryOtherFamily()
        {
            string[] modem = FieldIds(Group(ProductDialogPresets.Rs485SmsModem, "identitet"));
            string[] wired = FieldIds(Group(ProductDialogPresets.Dataline, "identitet"));

            Assert.Multiple(() =>
            {
                Assert.That(modem, Is.EqualTo(new[] { "navn", "note", "placering", "idkode" }).AsCollection);
                Assert.That(wired.ToList().IndexOf("placering"), Is.LessThan(wired.ToList().IndexOf("note")),
                    "the wired family is the other way round — so this is a real per-family difference");
            });
        }

        [Test]
        public void Modem_HasThirtyPhoneSlotsAsOneRepeat_InThreeColumns()
        {
            DialogGroupModel phones = Group(ProductDialogPresets.Rs485SmsModem, "telefonnumre");
            var repeat = phones.Parts.OfType<DialogRepeatModel>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(phones.Columns, Is.EqualTo(3), "the original lays the 30 slots out in three columns");
                Assert.That(phones.Parts, Has.Length.EqualTo(1),
                    "ONE repeat, not thirty declarations — the count follows the product (F-52)");
                Assert.That(repeat.CaptionPattern, Is.EqualTo("Nummer {0}"));
                Assert.That(repeat.DescendantTag, Is.EqualTo("sms_modem_phonenumber"));
                Assert.That(repeat.KeyAttribute, Is.EqualTo("address"));
                Assert.That(repeat.ValueAttribute, Is.EqualTo("phonenumber"));
                Assert.That(repeat.Rule, Is.SameAs(DialogValueRule.PhoneNumber), "the SDK's one telephone rule");
            });
        }

        /// <summary>
        /// The PIN declares no rule: its range is derived from the placed element's own minimum/maximum at compose
        /// time. A preset that hardcoded 0–9999 would go stale the moment a catalog changed them.
        /// </summary>
        [Test]
        public void Modem_PinCode_IsNumericWithNoDeclaredRange()
        {
            var pin = Group(ProductDialogPresets.Rs485SmsModem, "indstillinger")
                .Parts.OfType<DialogFieldModel>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(pin.Caption, Is.EqualTo("Pin Kode"));
                Assert.That(pin.Control, Is.EqualTo(DialogControlKind.Number));
                Assert.That(pin.Rule, Is.Null, "the range is DERIVED, not declared");
                Assert.That(pin.Binding, Is.EqualTo(new DialogBinding.DescendantAttribute("sms_modem_pincode")));
            });
        }

        [Test]
        public void Modem_Navn_IsReadOnly()
        {
            var navn = Group(ProductDialogPresets.Rs485SmsModem, "identitet")
                .Parts.OfType<DialogFieldModel>().First();
            Assert.That(navn.ReadOnly, Is.True, "measured: the original greys the modem's Navn");
        }

        // ── Rs485LedDimmer ──────────────────────────────────────────────────────────────────────────

        [Test]
        public void LedDimmer_IsThreeFieldsInOneGroup()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProductDialogPresets.Rs485LedDimmer.Groups, Has.Length.EqualTo(1));
                Assert.That(FieldIds(Group(ProductDialogPresets.Rs485LedDimmer, "identitet")),
                    Is.EqualTo(new[] { "navn", "placering", "note" }).AsCollection);
            });
        }

        /// <summary>
        /// D26 (T007, owner-ruled): the LED dimmer's per-channel advanced settings stay unreachable, because the
        /// original's dialog exposes no such surface. A widget slot here would invent one.
        /// </summary>
        [Test]
        public void LedDimmer_HasNoAdvancedSettingsSlotAndNoChannelSelector()
        {
            var parts = ProductDialogPresets.Rs485LedDimmer.Groups.SelectMany(g => g.Parts).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(parts.OfType<DialogWidgetModel>(), Is.Empty, "no widget slot at all (D26)");
                Assert.That(parts.OfType<DialogRepeatModel>(), Is.Empty, "and no per-channel repeat");
                Assert.That(parts, Has.Count.EqualTo(3), "three fields, nothing else");
            });
        }

        // ── S0Device ────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void S0_HasTheSevenMeasuredFieldsInRowMajorOrder()
        {
            DialogGroupModel identity = Group(ProductDialogPresets.S0Device, "identitet");
            Assert.Multiple(() =>
            {
                Assert.That(identity.Columns, Is.EqualTo(2));
                Assert.That(FieldIds(identity), Is.EqualTo(new[]
                {
                    "navn", "lfs0min", "idkode", "lfs0plus", "placering", "pulser", "note",
                }).AsCollection, "row-major: left field then right field, row by row");
            });
        }

        /// <summary>
        /// The vendor writes these two captions lower-case, unlike its own modem dialog's `Ledningsfarve 0V`.
        /// Caption text is data; the inconsistency is reproduced rather than tidied.
        /// </summary>
        [Test]
        public void S0_WireCaptions_AreLowerCaseAsTheVendorWritesThem()
        {
            string[] captions = Captions(Group(ProductDialogPresets.S0Device, "identitet"));
            Assert.That(captions, Does.Contain("ledningsfarve S0-").And.Contain("ledningsfarve S0+"));
        }

        /// <summary>T006: the S0's ten metering resources do not appear in the vendor dialog at all.</summary>
        [Test]
        public void S0_DoesNotOfferItsMeteringResources()
        {
            var parts = ProductDialogPresets.S0Device.Groups.SelectMany(g => g.Parts).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(parts.OfType<DialogRepeatModel>(), Is.Empty,
                    "no repeat over kWh/W resources — the original lists none");
                Assert.That(parts, Has.Count.EqualTo(7));
            });
        }

        [Test]
        public void S0_PulseCount_IsNumericBoundToTicks()
        {
            var pulses = Group(ProductDialogPresets.S0Device, "identitet")
                .Parts.OfType<DialogFieldModel>().Single(f => f.Id == "pulser");
            Assert.Multiple(() =>
            {
                Assert.That(pulses.Caption, Is.EqualTo("Antal pulser pr 1 kW"));
                Assert.That(pulses.Control, Is.EqualTo(DialogControlKind.Number));
                Assert.That(pulses.Binding, Is.EqualTo(new DialogBinding.RootAttribute("ticks")));
            });
        }

        // ── ForRootTag: ONE lookup, reached by every construction path (T020) ───────────────────────

        [TestCase("product_dataline")]
        [TestCase("product_airlink")]
        [TestCase("product_rs485_sms_modem")]
        [TestCase("product_rs485_led_dimmer")]
        [TestCase("s0_device")]
        public void EveryNamedFamilyRootTag_ResolvesToAPreset(string rootTag)
        {
            ProductDefinition built = ProductDefinitionBuilder.Create(rootTag, "_0x1", "X").Build();

            Assert.Multiple(() =>
            {
                Assert.That(built.Body.Tag, Is.EqualTo(rootTag), "the factory roots the product at that tag");
                Assert.That(ProductDialogPresets.ForRootTag(built.Body.Tag).IsEmpty, Is.False,
                    $"{rootTag} has a preset — so a product built this way composes its family dialog, not the fallback");
            });
        }

        // ── the localisation-key rule is carried by the FIELD, and reaches every family ──────────────

        /// <summary>
        /// The blank-an-unresolved-localisation-key rule reaches EVERY family's Note, not just the one product
        /// that ships such a value — because all six presets show their note through the one shared fragment that
        /// declares it. This is the pin on that reach: narrowing the rule to the S0 preset would leave the other
        /// four families printing a raw token if a catalog ever gave them one, and no other test would notice.
        /// </summary>
        [TestCase("product_dataline")]
        [TestCase("product_airlink")]
        [TestCase("product_rs485_sms_modem")]
        [TestCase("product_rs485_led_dimmer")]
        [TestCase("s0_device")]
        public void EveryFamilysNoteField_ClaimsTheLocalisationKeyRule(string rootTag)
        {
            DialogFieldModel note = ProductDialogPresets.ForRootTag(rootTag).Groups
                .SelectMany(g => g.Parts).OfType<DialogFieldModel>()
                .Single(f => f.Binding is DialogBinding.RootAttribute { Name: "note" });

            Assert.That(note.HidesUnresolvedResourceKey, Is.True);
        }

        /// <summary>
        /// And NO other field claims it. The rule blanks a value that merely LOOKS like a key — all-capitals with
        /// an underscore — so a field that legitimately holds such text must not carry it: a documentation tag of
        /// <c>A_1</c> is exactly that shape and is real installer input. Checked across every field of every
        /// family, because the claim is a per-field init property that a new fragment could pick up silently.
        /// </summary>
        [Test]
        public void NoFieldOtherThanTheNote_ClaimsTheLocalisationKeyRule()
        {
            string[] rootTags =
            [
                "product_dataline", "product_airlink", "product_rs485_sms_modem",
                "product_rs485_led_dimmer", "s0_device",
            ];

            var overreaching = rootTags
                .SelectMany(tag => ProductDialogPresets.ForRootTag(tag).Groups.SelectMany(g => g.Parts))
                .OfType<DialogFieldModel>()
                .Where(f => f.HidesUnresolvedResourceKey
                            && f.Binding is not DialogBinding.RootAttribute { Name: "note" })
                .Select(f => f.Id)
                .Distinct()
                .ToList();

            Assert.That(overreaching, Is.Empty,
                "only the note can hold a vendor localisation key — every other field's text is the installer's");
        }

        /// <summary>
        /// A family the SDK has never seen still gets a model — the empty one, which the composer turns into the
        /// minimal fallback. Returning null here would make every consumer handle the open-world case separately.
        /// </summary>
        [Test]
        public void AnUnknownRootTag_YieldsTheEmptyModelRatherThanNull()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProductDialogPresets.ForRootTag("product_from_the_future"),
                    Is.SameAs(ProductDialogModel.Empty));
                Assert.That(ProductDialogPresets.ForRootTag(null), Is.SameAs(ProductDialogModel.Empty));
            });
        }

        /// <summary>
        /// THE gate of T020: a definition authored in code and the same family read from a <c>.def</c> reach the
        /// SAME preset instance — which is the claim that matters, since the lookup is keyed on the device-root tag
        /// and the two construction paths must therefore agree on what that tag is.
        /// </summary>
        [Test]
        public void ABuiltDefinitionAndACatalogReadDefinition_ReachTheSamePreset()
        {
            ProductDefinition built = ProductDefinitionBuilder.Create("product_dataline", "_0x2101", "X").Build();
            ProductDefinition read = new BuiltInCatalog().Products
                .First(p => p.Body.Tag == "product_dataline");

            Assert.Multiple(() =>
            {
                Assert.That(ProductDialogPresets.ForRootTag(read.Body.Tag),
                    Is.SameAs(ProductDialogPresets.ForRootTag(built.Body.Tag)),
                    "one shared preset instance, not two copies");
                Assert.That(ProductDialogPresets.ForRootTag(read.Body.Tag), Is.SameAs(ProductDialogPresets.Dataline));
            });
        }

        /// <summary>
        /// Every catalog product's root tag is one the preset table KNOWS — checked across all 100 rather than on a
        /// sample, because a family whose tag no preset names would silently compose the minimal fallback for every
        /// one of its members, which looks like a working dialog with most of its fields missing.
        /// </summary>
        [Test]
        public void EveryCatalogProduct_ResolvesToARealPresetRatherThanTheFallback()
        {
            var unknown = new BuiltInCatalog().Products
                .Where(p => ProductDialogPresets.ForRootTag(p.Body.Tag).IsEmpty)
                .Select(p => $"{p.ProductIdentifier} <{p.Body.Tag}>")
                .ToList();

            Assert.That(unknown, Is.Empty);
        }

        /// <summary>The dialog is metadata about a dialog, not project content: it must never reach the body.</summary>
        [Test]
        public void TheDialogIsNotPartOfTheSerializedBody()
        {
            ProductDefinition modem = new BuiltInCatalog().Product("_0x3103");

            Assert.Multiple(() =>
            {
                Assert.That(ProductDialogPresets.ForRootTag(modem.Body.Tag).IsEmpty, Is.False,
                    "precondition: it has one");
                Assert.That(modem.Body.DescendantsAndSelf().SelectMany(e => e.Attrs).Select(a => a.Name),
                    Does.Not.Contain("dialog"));
            });
        }

        // ── the family enum ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void TheS0DeviceTagClassifiesAsItsOwnFamily()
            => Assert.That(ProductClassifier.Classify("s0_device"), Is.EqualTo(ProductFamily.S0Device));

        // ── the DRY seam itself ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A caption shared by both presets is authored ONCE. Asserted on the shared FRAGMENT instance rather than
        /// on the caption string: two identical string literals are the same instance by interning, so a
        /// caption-only check would pass even against two hand-retyped copies and prove nothing.
        /// </summary>
        [TestCase("placering")]
        [TestCase("note")]
        [TestCase("idkode")]
        [TestCase("lysgruppe")]
        public void ASharedFieldIsTheSameInstanceInBothPresets(string fieldId)
        {
            DialogFieldModel wired = Group(ProductDialogPresets.Dataline, "identitet")
                .Parts.OfType<DialogFieldModel>().Single(f => f.Id == fieldId);
            DialogFieldModel wireless = Group(ProductDialogPresets.Airlink, "identitet")
                .Parts.OfType<DialogFieldModel>().Single(f => f.Id == fieldId);

            Assert.Multiple(() =>
            {
                Assert.That(wireless, Is.SameAs(wired), "one fragment, referenced twice — not two copies");
                Assert.That(wireless.Caption, Is.SameAs(wired.Caption));
            });
        }

        /// <summary>
        /// `Navn` is a FUNCTION fragment (it varies by read-only), so the two presets hold different instances —
        /// but they must still agree on everything the fragment owns. This is the case the instance check above
        /// cannot cover, and the one where a retyped caption would actually hide.
        /// </summary>
        [Test]
        public void TheNameFragment_AgreesAcrossPresets_EvenThoughItIsParameterized()
        {
            DialogFieldModel wired = Group(ProductDialogPresets.Dataline, "identitet")
                .Parts.OfType<DialogFieldModel>().Single(f => f.Id == "navn");
            DialogFieldModel wireless = Group(ProductDialogPresets.Airlink, "identitet")
                .Parts.OfType<DialogFieldModel>().Single(f => f.Id == "navn");

            Assert.That(wireless, Is.EqualTo(wired), "same caption, control, binding and read-only state");
        }

        /// <summary>
        /// The four modem wire captions the fragment library exposes are the DIALOG's, measured 2026-08-11 — not
        /// `InstallationReportBuilder`'s, which spells the last two `RS485Minus`/`RS485Plus`. D11 asked for the
        /// report's verbatim on the assumption the two agreed; they do not, and D14 makes the measured dialog the
        /// governing default. Pinned so the conflict cannot be silently "corrected" later.
        /// </summary>
        [Test]
        public void TheModemWireFragments_UseTheCaptionsTheDialogShows()
        {
            DialogFieldModel[] wires = ProductDialogFragments.ModemWires();

            Assert.Multiple(() =>
            {
                Assert.That(wires.Select(w => w.Caption), Is.EqualTo(new[]
                {
                    "Ledningsfarve 0V",
                    "Ledningsfarve 24V",
                    "Ledningsfarve RS485 minus",   // NOT "RS485Minus", which is the REPORT's spelling
                    "Ledningsfarve RS485 plus",    // NOT "RS485Plus"
                }).AsCollection);
                Assert.That(wires.Select(w => ((DialogBinding.RootAttribute)w.Binding).Name), Is.EqualTo(new[]
                {
                    "cablecolour_0V", "cablecolour_24V", "cablecolour_RS485Minus", "cablecolour_RS485Plus",
                }).AsCollection, "the ATTRIBUTE names keep the file's own spelling — only the captions differ");
                Assert.That(wires.Select(w => w.Control), Is.All.EqualTo(DialogControlKind.ComboSuggest),
                    "measured: the vendor's cable-colour fields are editable combos, not plain boxes");
            });
        }
    }
}
