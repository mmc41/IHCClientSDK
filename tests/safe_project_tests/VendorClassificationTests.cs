#nullable enable
using System;
using System.Linq;

using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Regressions for three vendor-grounded defects, each reproduced before it was fixed.
    /// <para><b>1. <c>s0_device</c> is a PRODUCT, not a resource.</b> It is the one catalog device root that carries
    /// no <c>product_</c> prefix, so every predicate keyed on that prefix answered "no" for it. The vendor's own
    /// tree-builder dispatches it down the same branch as <c>product_dataline</c> and emits the node kind
    /// <c>"product"</c>; its DTD declares <c>product_identifier</c> <c>#REQUIRED</c> alongside the documentation
    /// attributes; and it is placed directly under a <c>group</c> like any other product. A live menu-bar reading of
    /// the vendor app has it enabling <i>Slet</i>, which the prefix-keyed delete gate refused.</para>
    /// <para><b>2. The log-mark toggle must key on <c>typeid</c>, not on the display name.</b> The built-in "Logning"
    /// type carries stable per-value <c>typeid</c>s (<c>_0x17</c> = the off state, <c>_0x18</c> = the first logging
    /// mode); the vendor identifies built-in enum values by <c>typeid</c> and re-allocates element ids across save
    /// cycles. The name is the fragile key: it is user-editable, it is the one untranslated English string in an
    /// otherwise Danish table, and the catalog's own log stubs omit it entirely.</para>
    /// <para><b>3. A refusal names the enum TYPE, not its internal token.</b> Nothing in the vendor product shows an
    /// <c>_0x</c> token to an installer — every surface that names an enumeration type uses its display name — so the
    /// two ends of the value-position guard must both say <c>'Logning'</c>, never <c>'_0x4747'</c>.</para>
    /// </summary>
    public class VendorClassificationTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        // ---- 1. s0_device is a product ----

        /// <summary>The delete gate admits an S0 device, as the vendor's own menu bar does. Keyed through the shared
        /// classifier rather than the <c>product_</c> prefix, which silently excluded this one root.</summary>
        [Test]
        public void S0Device_IsDeletable()
        {
            var app = new ProjectAppService(Settings);
            Project project = WithS0Device(app, out ElementId s0Id);

            Assert.That(app.Commands.CanDelete(project, s0Id), Is.True,
                "an S0 device is an ordinary product placed in a locality — the vendor enables Slet on it");
        }

        /// <summary>An S0 device reaches the same product properties route as any other product.</summary>
        [Test]
        public void S0Device_ClassifiesAsProduct()
        {
            var app = new ProjectAppService(Settings);
            Project project = WithS0Device(app, out ElementId s0Id);
            ProjectElement s0 = project.FindById(s0Id)!;

            Assert.Multiple(() =>
            {
                Assert.That(s0.Kind, Is.EqualTo(ElementKind.Product), "coarse kind");
                Assert.That(ProductClassifier.IsProduct(s0.Tag), Is.True, "shared classifier");
                Assert.That(ProductClassifier.Classify(s0.Tag), Is.EqualTo(ProductFamily.S0Device), "family");
                Assert.That(s0.GetAttribute("product_identifier"), Is.Not.Null,
                    "the catalog product reference every product root carries (#REQUIRED in the DTD)");
            });
        }

        // ---- 2. the log-mark toggle keys on typeid ----

        /// <summary>Renaming the built-in states must not break the toggle: the vendor keys built-in enum values on
        /// their stable <c>typeid</c>, and the type's values are user-reachable through the enum manager.</summary>
        [Test]
        public void LogMark_TogglesAfterTheStateNamesChange()
        {
            var app = new ProjectAppService(Settings);
            Project project = WithLogRow(app, out ElementId logId);

            // Rename every Logning state, leaving the typeids untouched — what a translated build (or an installer
            // editing the type) produces. The old name-keyed lookup finds no "Off" here and refuses outright.
            ProjectEditor editor = project.Edit();
            foreach (ProjectElement value in LogningValues(project))
            {
                editor.TryResolve(value.Id!.Value, out ElementRef? handle);
                handle!.SetAttribute("name", "Tilstand " + value.GetAttribute("typeid"));
            }
            Project renamed = editor.ToProject();

            ProjectEditor toggling = renamed.Edit();
            toggling.ToggleLogMark(logId);
            Project toggled = toggling.ToProject();

            Assert.That(toggled.FindById(logId)!.GetAttribute("inivalue"),
                Is.EqualTo(TokenOfTypeid(toggled, "_0x18")),
                "toggling on selects the FIRST logging mode by its stable typeid, whatever it is now called");
        }

        /// <summary>Toggling on selects <c>_0x18</c> ("Kun ændringer") specifically — the mode the vendor's own worked
        /// example picks — rather than whichever value happens to sit first in document order.</summary>
        [Test]
        public void LogMark_TogglesBetweenTheOffAndFirstModeTypeids()
        {
            var app = new ProjectAppService(Settings);
            Project project = WithLogRow(app, out ElementId logId);

            ProjectEditor editor = project.Edit();
            editor.ToggleLogMark(logId);
            Project on = editor.ToProject();

            ProjectEditor back = on.Edit();
            back.ToggleLogMark(logId);
            Project off = back.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(on.FindById(logId)!.GetAttribute("inivalue"), Is.EqualTo(TokenOfTypeid(on, "_0x18")),
                    "on → the first logging mode");
                Assert.That(off.FindById(logId)!.GetAttribute("inivalue"), Is.EqualTo(TokenOfTypeid(off, "_0x17")),
                    "off → the off state");
            });
        }

        // ---- 3. the refusal names the type ----

        /// <summary>Both ends of the value-position guard report the type by its display name. The deep guard used to
        /// interpolate the definition's id token, so the same condition read either <c>'Logning'</c> or
        /// <c>'_0x4747'</c> depending on which end caught it.</summary>
        [Test]
        public void OutOfRangeValuePosition_NamesTheTypeNotItsToken()
        {
            var app = new ProjectAppService(Settings);
            Project project = app.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));

            ProjectEditor editor = project.Edit();
            EnumDefinitionRef definition = editor.EnumDefinition("Logning");

            EditRefusedException refusal =
                Assert.Throws<EditRefusedException>(() => EnumValueAddressing.At(definition, 99))!;

            Assert.Multiple(() =>
            {
                Assert.That(refusal.Message, Does.Contain("Logning"), "the type is named as the installer sees it");
                Assert.That(refusal.Message, Does.Not.Contain("_0x"),
                    "no internal id token reaches the installer — no vendor surface shows one");
            });
        }

        // ---- helpers ----

        private static Project WithS0Device(ProjectAppService app, out ElementId s0Id)
        {
            Project project = app.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
            ProductDefinition s0 = app.GetAvailableProducts().First(p => p.Body.Tag == "s0_device");
            string room = project.Groups.First().GetAttribute("name")!;

            ProjectEditor editor = project.Edit();
            editor.Group(room).AddProduct(s0);
            Project placed = editor.ToProject();

            s0Id = placed.Root.DescendantsAndSelf().First(e => e.Tag == "s0_device").Id!.Value;
            return placed;
        }

        private static Project WithLogRow(ProjectAppService app, out ElementId logId)
        {
            Project project = app.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
            ProductDefinition sensor = app.GetAvailableProducts()
                .First(p => p.DisplayName.Contains("Temperatur sensor med logning"));
            string room = project.Groups.First().GetAttribute("name")!;

            ProjectEditor editor = project.Edit();
            editor.Group(room).AddProduct(sensor);
            Project placed = editor.ToProject();

            logId = placed.Root.DescendantsAndSelf()
                .First(e => e.Tag == "resource_enum" && (e.GetAttribute("name") ?? string.Empty).StartsWith("Log"))
                .Id!.Value;
            return placed;
        }

        // The project's Logning definition values, in document order.
        private static ProjectElement[] LogningValues(Project project) =>
            project.Root.DescendantsAndSelf()
                .First(e => e.Tag == "enum_definition"
                            && e.GetAttribute("typeid") == ProjectElementRead.LogEnumTypeId)
                .Children.Where(c => c.Tag == "enum_value").ToArray();

        // The id token of the Logning value carrying the given stable typeid.
        private static string TokenOfTypeid(Project project, string typeid) =>
            LogningValues(project).First(v => v.GetAttribute("typeid") == typeid).Id!.Value.ToToken();
    }
}
