using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// <c>ProjectAppService.GetProductDialog</c> — the composer that turns a family preset plus one placed element
    /// into a fully resolved descriptor. Everything conditional happens here once, so a renderer and a write-back
    /// can both be family-agnostic; these tests pin each of those resolutions against a real placed product.
    /// </summary>
    public class ProductDialogComposerTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        /// <summary>A fresh project with one product of the given catalog id placed in its first locality.</summary>
        private static async Task<(Project Project, ElementId Id)> Placed(string productIdentifier)
        {
            ProjectAppService app = App;
            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            ProductDefinition definition = app.GetAvailableProducts()
                .First(p => p.ProductIdentifier == productIdentifier);
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId id = session.Apply(new AddProduct(locality, definition)).Value;
            return (session.Current!, id);
        }

        private static DialogDescriptorField Field(ProductDialogDescriptor d, string automationIdSuffix) =>
            d.AllFields.Single(f => f.AutomationId.EndsWith(automationIdSuffix, System.StringComparison.Ordinal));

        // ── the repeat expansion (the gate) ─────────────────────────────────────────────────────────

        /// <summary>
        /// THE gate of T022. The modem's 30 telephone slots hang off three <c>sms_modem_settings</c> containers,
        /// so they are GRANDCHILDREN of the product. A child-scoped expansion finds none of them and the dialog
        /// silently loses its entire telephone section — which is why the count, not merely "more than four", is
        /// what is asserted.
        /// </summary>
        [Test]
        public async Task Modem_ExpandsToThirtyResolvedPhoneTargets()
        {
            (Project project, ElementId id) = await Placed("_0x3103");

            ProductDialogDescriptor dialog = App.GetProductDialog(project, id);

            var phones = dialog.Groups.Single(g => g.Id == "telefonnumre").Fields;
            Assert.Multiple(() =>
            {
                Assert.That(phones, Has.Length.EqualTo(30), "a child-scoped expansion would yield zero");
                Assert.That(phones.Select(f => f.Target).Distinct().Count(), Is.EqualTo(30),
                    "each slot resolves to its OWN element, not thirty references to one");
                Assert.That(phones.All(f => f.Attribute == "phonenumber"), Is.True);
            });
        }

        /// <summary>Ordered by the NUMERIC key: string order would put slot 10 immediately after slot 1.</summary>
        [Test]
        public async Task Modem_PhoneSlotsAreInNumericKeyOrder()
        {
            (Project project, ElementId id) = await Placed("_0x3103");

            var captions = App.GetProductDialog(project, id)
                .Groups.Single(g => g.Id == "telefonnumre").Fields.Select(f => f.Caption).ToArray();

            Assert.That(captions, Is.EqualTo(
                Enumerable.Range(1, 30).Select(n => $"Nummer {n}").ToArray()).AsCollection);
        }

        // ── binding resolution ──────────────────────────────────────────────────────────────────────

        /// <summary>A descendant binding resolves to the descendant's own id, not to the product's.</summary>
        [Test]
        public async Task ADescendantBinding_ResolvesToTheDescendantNotTheProduct()
        {
            (Project project, ElementId id) = await Placed("_0x3103");

            ProductDialogDescriptor dialog = App.GetProductDialog(project, id);
            DialogDescriptorField pin = Field(dialog, "indstillinger.pinkode");

            Assert.Multiple(() =>
            {
                Assert.That(pin.Target, Is.Not.EqualTo(id), "the PIN lives on sms_modem_pincode, not on the product");
                Assert.That(project.FindById(pin.Target)!.Tag, Is.EqualTo("sms_modem_pincode"));
                Assert.That(pin.Attribute, Is.EqualTo("value"));
            });
        }

        [Test]
        public async Task ARootBinding_ResolvesToTheProductItself()
        {
            (Project project, ElementId id) = await Placed("_0x2101");

            DialogDescriptorField placering = Field(App.GetProductDialog(project, id), "identitet.placering");

            Assert.Multiple(() =>
            {
                Assert.That(placering.Target, Is.EqualTo(id));
                Assert.That(placering.Attribute, Is.EqualTo("position"));
            });
        }

        // ── derived numeric range ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// The PIN's bounds come from the placed element's own <c>minimum</c>/<c>maximum</c> (the catalog seeds
        /// 0–9999), never from the preset — which declares no rule at all for it.
        /// </summary>
        [Test]
        public async Task ANumericFieldDerivesItsRangeFromTheElement()
        {
            (Project project, ElementId id) = await Placed("_0x3103");

            DialogDescriptorField pin = Field(App.GetProductDialog(project, id), "indstillinger.pinkode");

            Assert.Multiple(() =>
            {
                Assert.That(pin.Minimum, Is.EqualTo(0));
                Assert.That(pin.Maximum, Is.EqualTo(9999));
                Assert.That(pin.Rule, Is.Null, "the range is derived, not declared");
            });
        }

        /// <summary>A non-numeric field carries no range at all — the bounds are not a universal property.</summary>
        [Test]
        public async Task ATextFieldHasNoDerivedRange()
        {
            (Project project, ElementId id) = await Placed("_0x2101");

            DialogDescriptorField note = Field(App.GetProductDialog(project, id), "identitet.note");

            Assert.Multiple(() =>
            {
                Assert.That(note.Minimum, Is.Null);
                Assert.That(note.Maximum, Is.Null);
            });
        }

        // ── effective reads, and the blank-at-default rule ──────────────────────────────────────────

        /// <summary>
        /// The blanking rule must not swallow REAL values. The catalog seeds the PIN with 1234, which is not the
        /// declared default, so it is shown as-is.
        /// </summary>
        [Test]
        public async Task ANumericFieldAwayFromItsDefault_ShowsItsValue()
        {
            (Project project, ElementId id) = await Placed("_0x3103");
            ProjectElement pinElement = project.FindById(id)!.DescendantsAndSelf()
                .First(e => e.Tag == "sms_modem_pincode");
            Assert.That(pinElement.GetAttribute("value"), Is.EqualTo("1234"), "precondition: the catalog seeds 1234");

            DialogDescriptorField pin = Field(App.GetProductDialog(project, id), "indstillinger.pinkode");

            Assert.That(pin.Value, Is.EqualTo("1234"));
        }

        /// <summary>
        /// The SIM PIN's declared default is <c>0</c> and the original shows an EMPTY box for "no PIN" — rendering
        /// a literal 0 would read as a PIN of zero. A numeric field sitting at its declared default presents blank.
        /// <para>Driven by actually writing the default through the command, rather than by reading a freshly
        /// inserted product, because the catalog seeds 1234 and the rule would otherwise never fire in a test.</para>
        /// </summary>
        [Test]
        public async Task ANumericFieldAtItsDeclaredDefault_PresentsBlank()
        {
            (Project project, ElementId id) = await Placed("_0x3103");
            var session = new ProjectDocumentSession();
            session.Open(project);
            // The "no PIN" state, written the way the dialog writes it: an empty box committed through the generic
            // write-back, which stores the DTD default 0.
            DialogDescriptorField emptied = Field(App.GetProductDialog(session.Current!, id), "indstillinger.pinkode");
            session.Apply(new ApplyProductDialog(id,
                [new ProductDialogEdit(emptied.Target, emptied.Attribute, string.Empty)]));
            ProjectElement pinElement = session.Current!.FindById(id)!.DescendantsAndSelf()
                .First(e => e.Tag == "sms_modem_pincode");
            // Read EFFECTIVELY: writing the DTD default drops the attribute on serialize (omit-if-default), so the
            // no-PIN state is stored as the attribute's ABSENCE and only the effective read shows the 0.
            Assert.That(session.Current!.View(pinElement).Effective("value"), Is.EqualTo("0"),
                "precondition: the no-PIN state is the declared default 0");

            DialogDescriptorField pin = Field(App.GetProductDialog(session.Current!, id), "indstillinger.pinkode");

            Assert.That(pin.Value, Is.Empty, "0 is 'no PIN', and the original shows an empty box for it");
        }

        /// <summary>A field reads its effective value, which includes a DTD default the element omits.</summary>
        [Test]
        public async Task AFieldReadsTheEffectiveValue()
        {
            (Project project, ElementId id) = await Placed("_0x2101");

            DialogDescriptorField navn = Field(App.GetProductDialog(project, id), "identitet.navn");

            Assert.That(navn.Value, Is.EqualTo("LK FUGA Tryk 2 tast"),
                "AddProduct stamps the catalog display name, and the field reads it back");
        }

        // ── read-only ───────────────────────────────────────────────────────────────────────────────

        /// <summary>The declared flag alone makes a field read-only, even on a family with no `locked` attribute.</summary>
        [Test]
        public async Task ADeclaredReadOnlyField_IsReadOnly()
        {
            (Project project, ElementId id) = await Placed("_0x3103");

            Assert.That(Field(App.GetProductDialog(project, id), "identitet.navn").ReadOnly, Is.True);
        }

        [Test]
        public async Task AnEditableField_IsNotReadOnly()
        {
            (Project project, ElementId id) = await Placed("_0x3103");

            Assert.That(Field(App.GetProductDialog(project, id), "identitet.note").ReadOnly, Is.False);
        }

        // ── automation ids and titles ───────────────────────────────────────────────────────────────

        [Test]
        public async Task EveryFieldCarriesAUniqueDlgAutomationId()
        {
            (Project project, ElementId id) = await Placed("_0x3103");

            var ids = App.GetProductDialog(project, id).AllFields.Select(f => f.AutomationId).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(ids, Is.Unique);
                Assert.That(ids, Is.All.StartWith("dlg."));
                Assert.That(ids, Does.Contain("dlg.identitet.navn"));
                Assert.That(ids, Does.Contain("dlg.telefonnumre.nummer.30"),
                    "a repeat's expansion appends its key, so thirty slots are thirty distinct ids");
            });
        }

        /// <summary>
        /// Only the modem's dialog is titled "&lt;name&gt; Egenskaber"; every other family is titled with the bare
        /// product name. Measured across all 100 products — a single rule would be wrong for 99 or for 1.
        /// </summary>
        [TestCase("_0x3103", "SMS Modem Egenskaber")]
        [TestCase("_0x2101", "LK FUGA Tryk 2 tast")]
        [TestCase("_0x4409", "IHC LED Dimmer 2 kanaler")]
        [TestCase("_0x2313", "S0 Device")]
        public async Task TheTitleUsesTheMeasuredPerFamilyForm(string productIdentifier, string expectedTitle)
        {
            (Project project, ElementId id) = await Placed(productIdentifier);

            Assert.That(App.GetProductDialog(project, id).Title, Is.EqualTo(expectedTitle));
        }

        // ── ComboSuggest options (T023) ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Suggestions come from the OPEN PROJECT's distinct values for the bound attribute (D07) — not from a
        /// machine-local history, which would make the same project suggest different things on two machines.
        /// </summary>
        [Test]
        public async Task AComboSuggestField_OffersTheProjectsOwnValuesForThatAttribute()
        {
            (Project project, ElementId id) = await Placed("_0x2101");
            var session = new ProjectDocumentSession();
            session.Open(project);
            // Two other products get positions; a third repeats one of them.
            ElementId[] others = [.. session.Current!.Root.Descendants()
                .Where(e => ProductClassifier.IsProduct(e.Tag) && e.Id is not null && e.Id.Value != id)
                .Take(3).Select(e => e.Id!.Value)];
            SetPosition(session, others[0], "I loft");
            SetPosition(session, others[1], "Ved dør");
            SetPosition(session, others[2], "I loft");

            DialogDescriptorField placering =
                Field(App.GetProductDialog(session.Current!, id), "identitet.placering");

            Assert.Multiple(() =>
            {
                Assert.That(placering.SuggestionsOrEmpty, Does.Contain("I loft").And.Contain("Ved dør"));
                Assert.That(placering.SuggestionsOrEmpty.Count(s => s == "I loft"), Is.EqualTo(1),
                    "distinct: a value used twice is offered once");
                Assert.That(placering.SuggestionsOrEmpty,
                    Is.EqualTo(placering.SuggestionsOrEmpty.OrderBy(s => s, System.StringComparer.Ordinal)).AsCollection,
                    "a stable order, so the list does not reshuffle between opens");
            });
        }

        /// <summary>Writes one product's <i>Placering</i> through the same write-back the dialog uses, so the
        /// suggestion list is built from values that got there the way the installer's would.</summary>
        private static void SetPosition(ProjectDocumentSession session, ElementId productId, string position)
        {
            DialogDescriptorField field =
                Field(App.GetProductDialog(session.Current!, productId), "identitet.placering");
            session.Apply(new ApplyProductDialog(productId,
                [new ProductDialogEdit(field.Target, field.Attribute, position)]));
        }

        /// <summary>A plain text field offers nothing — suggestions belong to the combo kind, not to every field.</summary>
        [Test]
        public async Task ANonComboField_OffersNoSuggestions()
        {
            (Project project, ElementId id) = await Placed("_0x2101");

            Assert.That(Field(App.GetProductDialog(project, id), "identitet.lysgruppe").SuggestionsOrEmpty,
                Is.Empty, "Lysgruppe is plain text (D07 keeps it so)");
        }

        /// <summary>
        /// The suggestion list must never become a constraint: a combo field carries no rule that would reject a
        /// value simply because the project has not used it yet.
        /// </summary>
        [Test]
        public async Task SuggestionsDoNotConstrainTheValue()
        {
            (Project project, ElementId id) = await Placed("_0x2101");

            DialogDescriptorField placering = Field(App.GetProductDialog(project, id), "identitet.placering");

            Assert.Multiple(() =>
            {
                Assert.That(placering.Control, Is.EqualTo(DialogControlKind.ComboSuggest));
                Assert.That(placering.Rule, Is.Null, "an open combo, not an enumeration");
            });
        }

        // ── pruning and the minimal fallback (T023) ─────────────────────────────────────────────────

        /// <summary>
        /// A group whose every part turned out to be absent is DROPPED, not rendered as an empty titled box. The
        /// wireless family is the natural case: its preset has one group, and the dataline preset's terminal group
        /// disappears for a family with no terminals.
        /// </summary>
        [Test]
        public async Task AGroupWithNothingInIt_IsDropped()
        {
            (Project wirelessProject, ElementId wirelessId) = await Placed("_0x4101");

            ProductDialogDescriptor dialog = App.GetProductDialog(wirelessProject, wirelessId);

            Assert.Multiple(() =>
            {
                Assert.That(dialog.Groups.Select(g => g.Id), Is.EqualTo(new[] { "identitet" }).AsCollection);
                Assert.That(dialog.Groups.All(g => g.Fields.Length > 0 || g.Widgets.Length > 0), Is.True,
                    "no group survives composition empty");
            });
        }

        /// <summary>Every composed group of every catalog product carries something — the sweep behind the rule.</summary>
        [Test]
        public async Task NoCatalogProductComposesAnEmptyGroup()
        {
            ProjectAppService app = App;
            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            var empties = new System.Collections.Generic.List<string>();

            foreach (ProductDefinition definition in app.GetAvailableProducts())
            {
                var session = new ProjectDocumentSession();
                session.Open(project);
                ElementId id = session.Apply(new AddProduct(locality, definition)).Value;
                empties.AddRange(app.GetProductDialog(session.Current!, id).Groups
                    .Where(g => g.Fields.IsEmpty && g.Widgets.IsEmpty)
                    .Select(g => $"{definition.ProductIdentifier}/{g.Id}"));
            }

            Assert.That(empties, Is.Empty);
        }

        /// <summary>
        /// A REAL open-world product — imported from a <c>.def</c> whose root tag the SDK has never seen — carries
        /// the empty preset, which is what routes it to the minimal fallback. Exercised through the actual import
        /// path rather than by handing <c>ForRootTag</c> a made-up string, so it proves the whole chain
        /// (read → classify → resolve) reaches the open-world case.
        /// </summary>
        [Test]
        public void AnImportedUnknownFamily_CarriesTheEmptyPreset()
        {
            ProjectAppService app = App;
            app.ImportCatalogFile(TestData.PathOf("products", "synthetic", "synthetic_9f14_unknownfamily.def"));

            ProductDefinition imported = app.GetAvailableProducts()
                .Single(p => p.ProductIdentifier == "_0x9f14");

            Assert.Multiple(() =>
            {
                Assert.That(imported.Body.Tag, Is.EqualTo("product_unknown_family"),
                    "precondition: a root tag no preset knows");
                Assert.That(ProductClassifier.Classify(imported.Body.Tag), Is.EqualTo(ProductFamily.Other));
                Assert.That(ProductDialogPresets.ForRootTag(imported.Body.Tag).IsEmpty, Is.True,
                    "an unrecognised family resolves to the empty preset — the fallback's entry condition");
            });
        }

        /// <summary>
        /// The dialog layer is PRODUCT-only, and importing a function block must not have acquired any of it.
        /// Exercised through the same synthetic-import route as the product above: a function block read from an
        /// <c>.ifb</c> is a first-class catalog component, and the obvious regression is someone copying
        /// <c>ProductDefinition</c>'s new <c>Dialog</c> member onto it because the two look alike.
        /// </summary>
        [Test]
        public void AnImportedFunctionBlock_CarriesNoDialogMetadata()
        {
            ProjectAppService app = App;
            app.ImportCatalogFile(TestData.PathOf("functionblocks", "synthetic", "synthetic_fb01_toggle.ifb"));

            Assert.Multiple(() =>
            {
                Assert.That(app.GetAvailableFunctionBlocks(), Is.Not.Empty, "precondition: the import path works");
                Assert.That(typeof(FunctionBlockDefinition).GetProperties().Select(p => p.PropertyType),
                    Has.None.EqualTo(typeof(ProductDialogModel)),
                    "a function block has no properties dialog, so it must carry no dialog metadata");
            });
        }

        /// <summary>
        /// An unknown family still opens a usable dialog: the four attributes every known family declares, with
        /// Danish captions from the same shared fragments. Insert is never blocked by an unrecognised product.
        /// </summary>
        [Test]
        public void TheMinimalFallback_OffersTheFourUniversalFields()
        {
            // Composed directly against the empty preset — the state ForRootTag returns for an unknown root tag.
            ProductDialogModel unknown = ProductDialogPresets.ForRootTag("product_from_the_future");
            Assert.That(unknown.IsEmpty, Is.True, "precondition: an unknown family has no preset");

            Project project = new ProjectAppService(TestSetup.Settings)
                .CreateNew(new ProjectDetails("P", "I", "DK"));
            ElementId locality = project.Groups.First().Id!.Value;
            ProductDefinition dataline = App.GetAvailableProducts().First(p => p.ProductIdentifier == "_0x2101");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId id = session.Apply(new AddProduct(locality, dataline)).Value;

            // Composed through the internal composer directly: GetProductDialog resolves the preset from the root
            // tag, and every one of the 100 catalog products HAS a preset, so the fallback is unreachable through
            // the public door until an unknown family actually exists. Testing it here is the only way to have it
            // covered before that day rather than after it.
            ProductDialogDescriptor fallback = ProductDialogComposer.Compose(session.Current!, id, unknown, "Ukendt");

            Assert.Multiple(() =>
            {
                Assert.That(fallback.AllFields.Select(f => f.Caption),
                    Is.EqualTo(new[] { "Navn", "Placering", "Note", "Identifikationskode" }).AsCollection);
                Assert.That(fallback.Title, Is.EqualTo("Ukendt"), "an unknown family is titled with its own name");
                Assert.That(fallback.AllFields.All(f => f.Caption.Length > 0), Is.True,
                    "Danish captions, never raw attribute names");
            });
        }

        // ── totality ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Every catalog product composes without throwing, and every field it produces names a target that
        /// actually resolves. A binding that pointed at a tag the family lacks would otherwise surface only when
        /// some installer opened that one dialog.
        /// </summary>
        [Test]
        public async Task EveryCatalogProduct_ComposesWithResolvableTargets()
        {
            ProjectAppService app = App;
            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            var broken = new System.Collections.Generic.List<string>();

            foreach (ProductDefinition definition in app.GetAvailableProducts())
            {
                var session = new ProjectDocumentSession();
                session.Open(project);
                ElementId id = session.Apply(new AddProduct(locality, definition)).Value;
                ProductDialogDescriptor dialog = app.GetProductDialog(session.Current!, id);

                if (dialog.AllFields.Any(f => session.Current!.FindById(f.Target) is null))
                    broken.Add($"{definition.ProductIdentifier} has an unresolvable target");
                if (dialog.Title.Length == 0)
                    broken.Add($"{definition.ProductIdentifier} has no title");
            }

            Assert.That(broken, Is.Empty);
        }

        /// <summary>An open-world product still opens a dialog — the empty preset, not an exception.</summary>
        [Test]
        public async Task AFamilyWithNoPreset_StillComposes()
        {
            ProjectAppService app = App;
            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            ProjectElement anyProduct = project.Root.Descendants()
                .First(e => ProductClassifier.IsProduct(e.Tag) && e.Id is not null);

            // The dataline family HAS a preset, so this asserts the mechanism rather than the empty case; the
            // empty case is covered by ForRootTag's own test. What matters here is that composing never throws.
            Assert.That(() => app.GetProductDialog(project, anyProduct.Id!.Value), Throws.Nothing);
        }

        // ── widget presence is DATA, whatever the kind ──────────────────────────────────────────────

        /// <summary>
        /// A widget slot renders when its declared presence rule says so — for every kind, including the settings
        /// grid. The composer used to answer the settings grid's presence from its KIND and never consult the rule
        /// the preset supplied, so a rule stated for that slot was silently discarded: this composes a product that
        /// HAS settings against a preset whose settings slot is gated on a tag the product does not carry, and the
        /// slot must not appear.
        /// </summary>
        [Test]
        public async Task AWidgetsDeclaredPresenceIsHonoured_EvenForTheSettingsGrid()
        {
            // A sensor with real settings — so the slot would appear if presence were answered from the kind.
            (Project project, ElementId id) = await Placed("_0x2124");
            ProductDialogModel gated = ProductDialogFragments.Dialog(
                ProductDialogFragments.Group("terminaler", null, 1,
                    ProductDialogFragments.Widget("indstillinger", DialogWidgetKind.SettingsGrid,
                        ProductDialogFragments.Carrying("a_tag_this_product_does_not_have"))));

            ProductDialogDescriptor composed = ProductDialogComposer.Compose(project, id, gated, "Føler");

            Assert.That(composed.Groups.SelectMany(g => g.Widgets), Does.Not.Contain(DialogWidgetKind.SettingsGrid),
                "the slot's own presence rule decides, not its kind");
        }

        // ── an unresolved vendor resource key (T131) ────────────────────────────────────────────────

        /// <summary>
        /// The S0 device's catalog note is <c>PRODUCT_2315_NOTE</c> — a localisation KEY the vendor's own
        /// <c>.def</c> carries and that nothing in the install resolves. IHC Visual shows the Note box EMPTY;
        /// OpenVisual showed the raw token (measured on product 097, 2026-08-12).
        /// <para>The key is still STORED: a vendor-authored <c>.vis</c> has
        /// <c>note="PRODUCT_2315_NOTE"</c> on its <c>s0_device</c>, so the vendor resolves at display time and
        /// our insert bytes were already right. Only the presentation was wrong, which is why this is a read
        /// rule and not a catalog change.</para>
        /// </summary>
        [Test]
        public async Task AnUnresolvedResourceKeyNote_PresentsBlank()
        {
            (Project project, ElementId id) = await Placed("_0x2313");

            DialogDescriptorField note = Field(App.GetProductDialog(project, id), "identitet.note");

            Assert.Multiple(() =>
            {
                Assert.That(note.Value, Is.Empty, "the vendor shows nothing for a key it cannot resolve");
                Assert.That(project.FindById(id)!.GetAttribute("note"), Is.EqualTo("PRODUCT_2315_NOTE"),
                    "and the key is left in the file, exactly as the vendor leaves it");
            });
        }

        /// <summary>
        /// The rule must not eat a real note. <c>PIR</c> is the catalog's other all-capitals note, and the
        /// separator is what tells them apart: a resource key carries an underscore, prose does not.
        /// <para>Verified across all 100 catalog notes rather than assumed — exactly two are all-capitals and
        /// exactly one of those has an underscore. That scan is the evidence for the predicate; without it
        /// this is the third invented rule of the campaign (T099's lesson).</para>
        /// </summary>
        [Test]
        public async Task AnAllCapitalsNoteThatIsNotAKey_IsShownAsWritten()
        {
            (Project project, ElementId id) = await Placed("_0x210e");   // PIR, note "PIR"

            Assert.That(Field(App.GetProductDialog(project, id), "identitet.note").Value, Is.EqualTo("PIR"));
        }

        // ── the shutter travel times (T119) ─────────────────────────────────────────────────────────

        /// <summary>
        /// The two jalousi products get a second group, <i>Persienne egenskaber</i>, holding the travel
        /// times the vendor shows as spin boxes (measured on product 085, 2026-08-12).
        /// <para>Their range is DERIVED, as every numeric field's is: the catalog seeds each element with
        /// its own <c>minimum</c>/<c>maximum</c> (0–240), so the preset states no bounds and cannot go
        /// stale against a catalog that changes them.</para>
        /// </summary>
        [TestCase("_0x4501")]
        [TestCase("_0x4502")]
        public async Task AJalousiProduct_OffersItsTravelTimes(string identifier)
        {
            (Project project, ElementId id) = await Placed(identifier);

            DialogDescriptorGroup shutter =
                App.GetProductDialog(project, id).Groups.Single(g => g.Id == "persienne");

            Assert.Multiple(() =>
            {
                Assert.That(shutter.Caption, Is.EqualTo("Persienne egenskaber"));
                Assert.That(shutter.Columns, Is.EqualTo(2));
                Assert.That(shutter.Fields.Select(f => f.Caption), Is.EqualTo(new[]
                {
                    "Vandringstid fra bund til top [sekunder]",
                    "Vandringstid fra top til bund [sekunder]",
                }).AsCollection);
                Assert.That(shutter.Fields.All(f => f.Control == DialogControlKind.Number), Is.True);
                Assert.That(shutter.Fields.Select(f => f.Value), Is.All.EqualTo("120"),
                    "the catalog seeds both at 120 s, which is what the vendor shows");
                Assert.That(shutter.Fields.Select(f => (f.Minimum, f.Maximum)),
                    Is.All.EqualTo((0, 240)), "bounds read off the element, never declared in the preset");
                Assert.That(shutter.Fields.Select(f => f.Target).Distinct().Count(), Is.EqualTo(2),
                    "up and down are two different elements, not one written twice");
            });
        }

        /// <summary>
        /// And every OTHER wireless product gets no such group — gated by the BINDING rather than by a
        /// flag: a field whose descendant tag is absent does not resolve, an all-unresolved group is
        /// dropped, and the 22 wireless products without a shutter never see it. The same mechanism that
        /// keeps the preset shared by all 24 (T008).
        /// </summary>
        [TestCase("_0x4101")]
        [TestCase("_0x4303")]
        public async Task AWirelessProductWithNoShutter_HasNoTravelTimeGroup(string identifier)
        {
            (Project project, ElementId id) = await Placed(identifier);

            Assert.That(App.GetProductDialog(project, id).Groups.Any(g => g.Id == "persienne"), Is.False);
        }

        // ── the end-user-report checkbox (T098) ─────────────────────────────────────────────────────

        /// <summary>
        /// The vendor offers <i>Inkluder produktet i slutbruger rapport</i> on a USER-DEFINABLE product —
        /// measured on product 064, <c>Brugerdefineret indgangsprodukt</c>, where it is drawn checked at the
        /// bottom of <i>Produkt egenskaber</i>.
        /// </summary>
        [Test]
        public async Task AUserDefinableProduct_OffersTheEndUserReportCheckbox()
        {
            (Project project, ElementId id) = await Placed("_0x2701");

            DialogDescriptorField flag =
                Field(App.GetProductDialog(project, id), "identitet.slutbrugerrapport");

            Assert.Multiple(() =>
            {
                Assert.That(flag.Caption, Is.EqualTo("Inkluder produktet i slutbruger rapport"));
                Assert.That(flag.Control, Is.EqualTo(DialogControlKind.Checkbox));
                Assert.That(flag.Attribute, Is.EqualTo("enduser_report"));
                Assert.That(flag.Target, Is.EqualTo(id), "the flag lives on the product's own root");
                Assert.That(flag.ReadOnly, Is.False, "toggling it is the whole point");
                Assert.That(flag.Value, Is.EqualTo("yes"),
                    "and it reads the placed value the catalog seeded — the vendor draws it CHECKED");
            });
        }

        /// <summary>
        /// And on NO other product — these five are the ones that falsified the rules the gate was nearly
        /// built on, so they are the ones worth pinning (T099):
        /// <list type="bullet">
        /// <item><c>_0x107</c> Diode — UNLOCKED in the project (its <c>.def</c> misspells the attribute as
        /// <c>loced</c>) and still no checkbox: "unlocked products get it" is false.</item>
        /// <item><c>_0x2702</c>/<c>_0x2703</c>/<c>_0x2705</c> — all three declare <c>locked="no"</c> in their
        /// <c>.def</c>: "the seven user-definable products get it" is false.</item>
        /// <item><c>_0x2706</c> — <c>locked="no"</c> AND <c>enduser_report</c> defaulting to yes, a
        /// <c>.def</c> root identical to 064's but for its name and two logging children, and still no
        /// checkbox: the conjunction is false too, and nothing at the product root separates them.</item>
        /// </list>
        /// </summary>
        [TestCase("_0x107")]
        [TestCase("_0x2702")]
        [TestCase("_0x2703")]
        [TestCase("_0x2705")]
        [TestCase("_0x2706")]
        [TestCase("_0x2101")]
        public async Task EveryOtherProduct_OffersNoCheckbox(string identifier)
        {
            (Project project, ElementId id) = await Placed(identifier);

            var captions = App.GetProductDialog(project, id).AllFields.Select(f => f.Caption).ToList();

            Assert.That(captions, Does.Not.Contain("Inkluder produktet i slutbruger rapport"));
        }

        /// <summary>The checkbox preset is the wired one plus the flag — not a retyped second copy of it.</summary>
        [Test]
        public void TheCheckboxPresetIsTheWiredOnePlusTheFlag()
        {
            var wired = ProductDialogPresets.Dataline.Groups[0].Parts.Select(p => p.Id).ToList();
            var withFlag =
                ProductDialogPresets.DatalineEndUserReport.Groups[0].Parts.Select(p => p.Id).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(withFlag, Is.EqualTo(wired.Append("slutbrugerrapport")).AsCollection,
                    "same fields in the same order, with the flag appended — the vendor draws it last");
                Assert.That(ProductDialogPresets.DatalineEndUserReport.Groups.Skip(1),
                    Is.EqualTo(ProductDialogPresets.Dataline.Groups.Skip(1)).AsCollection,
                    "and the terminal/settings group is the SAME group, not a retyped copy");
            });
        }

        /// <summary>
        /// Committing the checkbox writes the flag. The write path validates against a dialog it composes
        /// itself, so this also pins that the two agree the field exists at all.
        /// </summary>
        [Test]
        public async Task TheCheckboxCommitsThroughTheGenericWriteBack()
        {
            (Project project, ElementId id) = await Placed("_0x2701");
            var session = new ProjectDocumentSession();
            session.Open(project);

            session.Apply(new ApplyProductDialog(id,
                [new ProductDialogEdit(id, "enduser_report", "no")]));

            Assert.Multiple(() =>
            {
                // EFFECTIVE, not raw: "no" is the project DTD's default for this flag, so the canonicalizer
                // drops the attribute rather than writing it — which is exactly the vendor's own file shape.
                Assert.That(session.Current!.View(session.Current!.FindById(id)!).Effective("enduser_report"),
                    Is.EqualTo("no"));
                Assert.That(Field(App.GetProductDialog(session.Current!, id), "identitet.slutbrugerrapport").Value,
                    Is.EqualTo("no"), "and the dialog shows what was stored");
            });
        }

        [Test]
        public async Task ANonProductId_IsRefusedRatherThanComposedIntoNonsense()
        {
            ProjectAppService app = App;
            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;

            Assert.That(() => app.GetProductDialog(project, locality),
                Throws.ArgumentException.With.Message.Contains("not a product"));
        }
    }
}
