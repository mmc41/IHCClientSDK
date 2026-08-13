using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Install-free: <strong>every</strong> product/function-block descriptor in the SDK-embedded
    /// <see cref="BuiltInCatalog"/> inserts one-at-a-time
    /// into a fresh project, saves, and re-loads structurally equal. Under the open-world model the static registry
    /// no longer needs to declare every catalog type — a type the registry does not contain is inserted using the
    /// grammar captured from its own catalog descriptor's inline DTD (merged into the project on insert), so the full
    /// ~173-descriptor catalog round-trips structurally regardless of what the (now lean) registry covers. Each
    /// inserted project also runs through <see cref="ProjectValidator"/>; all validate clean except the S0 device,
    /// whose vendor catalog data ships a malformed <c>kWh@accessibility="readwrite"</c> (outside the DTD enumeration)
    /// — a faithfully-copied source-data quirk the validator rightly flags, tolerated here but asserted to be the
    /// <em>only</em> kind of validity issue.
    /// </summary>
    public class CatalogInsertionTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static ICatalog RequireCatalog() => new BuiltInCatalog();

        [Test]
        public void EveryDiscoveredDescriptor_InsertsValidatesAndRoundTrips()
        {
            ICatalog catalog = RequireCatalog();
            var app = new ProjectAppService(Settings, catalog,
                new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 6, 27, 16, 5, 51, TimeSpan.Zero)));
            Project blank = app.CreateNew(new ProjectDetails("P", "I", "DK"));

            int inserted = 0;
            var failures = new List<string>();
            var validationQuirks = new List<string>();   // pre-existing malformed VENDOR catalog data, not machinery bugs
            var descriptorDefects = new List<string>();  // T026: the dialog-descriptor coverage gate

            void InsertOne(string name, Action<GroupRef> add)
            {
                try
                {
                    ProjectEditor editor = blank.Edit();
                    add(editor.Group("Stue"));
                    Project built = editor.ToProject();

                    // Structural round-trip — the core insert-machinery promise, required for every descriptor. For a
                    // type the registry no longer declares, the grammar comes from the descriptor's own inline DTD.
                    using var ms = new MemoryStream();
                    app.Save(built, ms, ProjectSaveOptions.PreserveExistingMetadata).GetAwaiter().GetResult();
                    Project reloaded = app.Load(new MemoryStream(ms.ToArray())).GetAwaiter().GetResult();
                    if (!reloaded.Equals(built))
                    {
                        failures.Add($"{name}: did not round-trip structurally equal");
                        return;
                    }
                    inserted++;

                    // Semantic validation — clean for all but the S0 device's malformed vendor <kWh accessibility="readwrite">.
                    ProjectValidationResult validation = app.Validate(built);
                    if (!validation.IsValid)
                    {
                        validationQuirks.AddRange(validation.Errors.Select(e => $"{name}: {e}"));
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            foreach (ProductDefinition product in catalog.Products)
            {
                InsertOne(product.DisplayName, room => room.AddProduct(product));
            }
            // The dialog-descriptor gate rides the same per-product enumeration (T026): every catalog product's
            // dialog must compose soundly, not merely insert soundly.
            descriptorDefects.AddRange(DescriptorDefectsForEveryProduct(app, blank, catalog));
            foreach (FunctionBlockDefinition block in catalog.FunctionBlocks)
            {
                InsertOne(block.DisplayName, room => room.AddFunctionBlock(block));
            }

            TestContext.Out.WriteLine($"inserted+round-tripped: {inserted}");
            TestContext.Out.WriteLine($"tolerated vendor-data validation quirks ({validationQuirks.Count}): {string.Join(" | ", validationQuirks)}");

            Assert.Multiple(() =>
            {
                Assert.That(failures, Is.Empty,
                    "every descriptor must insert + round-trip structurally (open-world: unregistered types use their own descriptor's DTD)");
                Assert.That(inserted, Is.GreaterThan(150), "the full discovered catalog (~173 descriptors) inserts + round-trips");
                Assert.That(validationQuirks.All(q => q.Contains("accessibility") && q.Contains("readwrite")), Is.True,
                    "the only tolerated validity issues are the known vendor-catalog accessibility=\"readwrite\" quirk; got: "
                    + string.Join(" | ", validationQuirks));
                Assert.That(descriptorDefects, Is.Empty,
                    "every catalog product's dialog must compose soundly: " + string.Join(" | ", descriptorDefects));
            });
        }

        // ── T026: the dialog-descriptor coverage gate ───────────────────────────────────────────────
        //
        // Rides the per-product enumeration above rather than standing alone, for the reason that enumeration
        // exists at all: a defect that affects ONE family affects every product in it, and a spot-check on a
        // representative product is exactly how such a defect survives. Each check below is one that a plausible
        // preset or composer mistake would trip, and each names the product it failed on.

        /// <summary>Composes every catalog product's dialog and returns a named defect per problem found.</summary>
        private static IEnumerable<string> DescriptorDefectsForEveryProduct(
            ProjectAppService app, Project blank, ICatalog catalog)
        {
            // A sweep that examined nothing would report no defects, which is indistinguishable from a clean run.
            if (catalog.Products.Count < 100)
            {
                yield return $"the sweep saw only {catalog.Products.Count} products — it would pass vacuously";
            }

            foreach (ProductDefinition product in catalog.Products)
            {
                ProjectEditor editor = blank.Edit();
                ElementId id = editor.Group("Stue").AddProduct(product).Id;
                Project built = editor.ToProject();

                ProductDialogDescriptor? dialog = null;
                string? threw = null;
                try
                {
                    dialog = app.GetProductDialog(built, id);
                }
                catch (Exception ex)
                {
                    threw = $"{product.ProductIdentifier}: composing threw {ex.GetType().Name}: {ex.Message}";
                }

                if (threw is not null)
                {
                    yield return threw;
                    continue;
                }

                foreach (string defect in DescriptorDefects(
                             built, id, ProductDialogPresets.ForRootTag(product.Body.Tag), dialog!))
                {
                    yield return $"{product.ProductIdentifier} <{product.Body.Tag}>: {defect}";
                }
            }
        }

        /// <summary>
        /// The checker itself, taking the preset and the composed descriptor explicitly so the ARMED CONTROL can
        /// point it at a deliberately broken preset and exercise this exact code — not a lookalike.
        /// </summary>
        internal static IEnumerable<string> DescriptorDefects(
            Project project, ElementId productId, ProductDialogModel preset, ProductDialogDescriptor dialog)
        {
            ProjectElement product = project.FindById(productId)!;

            if (dialog.Title.Length == 0)
            {
                yield return "the dialog has no title";
            }

            foreach (DialogDescriptorGroup group in dialog.Groups)
            {
                if (group.Fields.IsEmpty && group.Widgets.IsEmpty)
                {
                    yield return $"group '{group.Id}' composed empty and should have been dropped";
                }
                if (group.Columns < 1)
                {
                    yield return $"group '{group.Id}' has a column count of {group.Columns}";
                }
            }

            var automationIds = dialog.AllFields.Select(f => f.AutomationId).ToList();
            foreach (string duplicate in automationIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                // Two controls sharing an automation id makes one of them unaddressable by an assistive
                // technology, and makes a write-back ambiguous about which field it is applying.
                yield return $"duplicate automation id '{duplicate}'";
            }

            foreach (DialogDescriptorField field in dialog.AllFields)
            {
                if (field.Caption.Length == 0)
                {
                    yield return $"field '{field.AutomationId}' has no caption";
                }
                if (!Enum.IsDefined(field.Control))
                {
                    yield return $"field '{field.AutomationId}' has an unknown control kind {(int)field.Control}";
                }
                if (field.Attribute.Length == 0)
                {
                    yield return $"field '{field.AutomationId}' binds no attribute";
                }
                if (project.FindById(field.Target) is null)
                {
                    yield return $"field '{field.AutomationId}' targets an element that does not resolve";
                }
            }

            // Every part the preset declares must have produced what it promised. The composer DROPS a field whose
            // binding does not resolve, which is right for an absent widget but would silently lose a field whose
            // preset named a tag the family lacks — so absence is checked here rather than trusted.
            foreach (DialogGroupModel group in preset.Groups)
            {
                // …except a group the preset gates on a tag this product does not carry: it was never composed,
                // so its fields are legitimately absent. Checked against the DECLARED gate, which is why that
                // gate has to be declared — a group that let its fields drop themselves would be indistinguishable
                // here from a preset naming a tag by mistake (T119).
                if (!group.Presence.IsPresentIn(product.DescendantsAndSelf()))
                {
                    continue;
                }

                foreach (DialogPartModel part in group.Parts)
                {
                    switch (part)
                    {
                        case DialogFieldModel field:
                            string expected = ProductDialogComposer.AutomationId(group.Id, field.Id);
                            if (!automationIds.Contains(expected))
                            {
                                yield return $"preset field '{expected}' did not resolve against this product";
                            }
                            break;

                        case DialogRepeatModel repeat:
                            int declared = product.DescendantsAndSelf()
                                .Count(e => e.Tag == repeat.DescendantTag && e.Id is not null);
                            string prefix = ProductDialogComposer.AutomationId(group.Id, repeat.Id) + ".";
                            int expanded = automationIds.Count(x => x.StartsWith(prefix, StringComparison.Ordinal));
                            if (expanded != declared)
                            {
                                yield return $"repeat '{repeat.Id}' expanded to {expanded} field(s) "
                                           + $"but the product declares {declared} <{repeat.DescendantTag}>";
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Every known family has a preset. <see cref="ProductFamily.Other"/> is the open-world case and correctly
        /// has none; every other member resolving to an empty model would mean a family whose products all fall
        /// through to the minimal fallback without anyone noticing.
        /// </summary>
        [Test]
        public void EveryKnownProductFamily_ResolvesToANonEmptyPreset()
        {
            // The family → root tag mapping, stated here so a NEW ProductFamily member fails this test until it is
            // given both a tag and a preset.
            var rootTagOf = new Dictionary<ProductFamily, string>
            {
                [ProductFamily.Dataline] = "product_dataline",
                [ProductFamily.Airlink] = "product_airlink",
                [ProductFamily.Rs485LedDimmer] = "product_rs485_led_dimmer",
                [ProductFamily.Rs485SmsModem] = "product_rs485_sms_modem",
                [ProductFamily.S0Device] = "s0_device",
                [ProductFamily.Rs485Modem] = "product_rs485_modem",
            };

            var missing = new List<string>();
            foreach (ProductFamily family in Enum.GetValues<ProductFamily>())
            {
                if (family == ProductFamily.Other)
                {
                    continue;   // the open-world case: no preset by design
                }
                if (!rootTagOf.TryGetValue(family, out string? tag))
                {
                    missing.Add($"{family}: no root tag is mapped for it here");
                    continue;
                }
                // Rs485Modem is recognised by the classifier but has NO TypeCode and no catalog product
                // (TypeCode.cs states this deliberately), so it has no measured dialog to preset. Named rather
                // than silently skipped.
                if (family == ProductFamily.Rs485Modem)
                {
                    Assert.That(ProductDialogPresets.ForRootTag(tag).IsEmpty, Is.True,
                        "Rs485Modem has no catalog product and therefore no measured preset — if one is added, "
                        + "move it out of this exemption rather than widening it");
                    continue;
                }
                if (ProductDialogPresets.ForRootTag(tag).IsEmpty)
                {
                    missing.Add($"{family} (<{tag}>) resolves to the EMPTY preset");
                }
            }

            Assert.That(missing, Is.Empty, string.Join(" | ", missing));
        }

        /// <summary>
        /// Cross-path agreement on the five synthetic family oracles: a product READ from a <c>.def</c> carries the
        /// same dialog as the same family authored in code. One file per family, so a family wired to the wrong
        /// preset in the reader cannot hide behind the four that are right.
        /// </summary>
        [TestCase("synthetic_9f02_output.def", "product_dataline")]
        [TestCase("synthetic_9f04_wireless.def", "product_airlink")]
        [TestCase("synthetic_9f05_dimmer.def", "product_rs485_led_dimmer")]
        [TestCase("synthetic_9f06_modem.def", "product_rs485_sms_modem")]
        [TestCase("synthetic_9f07_meter.def", "s0_device")]
        public void ASyntheticFamilyOracle_CarriesTheSamePresetAsTheBuilderPath(string fixture, string expectedRootTag)
        {
            var app = new ProjectAppService(Settings);
            app.ImportCatalogFile(TestData.PathOf("products", "synthetic", fixture));
            ProductDefinition read = app.GetAvailableProducts()
                .First(p => p.Body.Tag == expectedRootTag && p.ProductIdentifier.StartsWith("_0x9f", StringComparison.Ordinal));
            ProductDefinition builtInCode = ProductDefinitionBuilder
                .Create(expectedRootTag, "_0xtest", "X").Build();

            Assert.Multiple(() =>
            {
                Assert.That(ProductDialogPresets.ForRootTag(read.Body.Tag),
                    Is.SameAs(ProductDialogPresets.ForRootTag(builtInCode.Body.Tag)),
                    "one shared preset, reached by both paths");
                Assert.That(ProductDialogPresets.ForRootTag(read.Body.Tag).IsEmpty, Is.False,
                    $"<{expectedRootTag}> has a preset");
            });
        }

        /// <summary>
        /// The armed control. The SAME checker, pointed at a deliberately broken preset, must report every defect
        /// class it is supposed to catch — otherwise the green gate above proves only that the checker is silent.
        /// </summary>
        [Test]
        public void TheDescriptorGate_IsArmed()
        {
            var app = new ProjectAppService(Settings);
            Project blank = app.CreateNew(new ProjectDetails("P", "I", "DK"));
            ProjectEditor editor = blank.Edit();
            ElementId id = editor.Group("Stue")
                .AddProduct(new BuiltInCatalog().Product("_0x3103")).Id;   // the modem: it has real descendants
            Project built = editor.ToProject();

            // Three seeded defects: a field bound to a tag no product carries, a repeat over a tag that is absent,
            // and two parts sharing an id (which composes into a duplicate automation id).
            ProductDialogModel broken = new([
                new DialogGroupModel("g", "G", 1, [
                    new DialogFieldModel("ghost", "Spøgelse", DialogControlKind.Text,
                        new DialogBinding.DescendantAttribute("tag_der_ikke_findes")),
                    new DialogFieldModel("dup", "En", DialogControlKind.Text, new DialogBinding.RootAttribute("note")),
                    new DialogFieldModel("dup", "To", DialogControlKind.Text, new DialogBinding.RootAttribute("position")),
                    new DialogRepeatModel("spor", "Spor {0}", "tag_der_heller_ikke_findes", "address", "value",
                        DialogControlKind.Text),
                ]),
            ]);

            var defects = DescriptorDefects(built, id, broken,
                ProductDialogComposer.Compose(built, id, broken, "Testtitel")).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(defects.Any(d => d.Contains("g.ghost") && d.Contains("did not resolve")), Is.True,
                    "an unresolvable field binding must be reported, not silently dropped: " + string.Join(" | ", defects));
                Assert.That(defects.Any(d => d.Contains("duplicate automation id")), Is.True,
                    "two parts sharing an id must be reported: " + string.Join(" | ", defects));
            });
        }

        /// <summary>And the control is not reporting for a trivial reason: a SOUND preset yields no defects.</summary>
        [Test]
        public void TheDescriptorGate_ReportsNothingForASoundPreset()
        {
            var app = new ProjectAppService(Settings);
            Project blank = app.CreateNew(new ProjectDetails("P", "I", "DK"));
            ProjectEditor editor = blank.Edit();
            ElementId id = editor.Group("Stue").AddProduct(new BuiltInCatalog().Product("_0x3103")).Id;
            Project built = editor.ToProject();

            var defects = DescriptorDefects(built, id, ProductDialogPresets.Rs485SmsModem,
                app.GetProductDialog(built, id)).ToList();

            Assert.That(defects, Is.Empty, string.Join(" | ", defects));
        }
    }
}
