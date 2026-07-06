using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Exercises the read-only <see cref="ResourceSummary"/> projections on <see cref="ProductDefinition"/> and
    /// <see cref="FunctionBlockDefinition"/> — the typed read-back a GUI binds a definition preview to without walking
    /// the raw <see cref="ProjectElement"/> tree. These run against hand-built definition bodies (no builder, no
    /// catalog, no install dir), so they are real runnable tests independent of the Stage-1 builder stubs.
    /// </summary>
    public class DefinitionProjectionTests
    {
        private static ProjectElement El(string tag, string name, params ProjectElement[] children) =>
            new ProjectElement(
                tag,
                null,
                ImmutableArray.Create<(string, string)>(("name", name)),
                children.Length == 0 ? ImmutableArray<ProjectElement>.Empty : ImmutableArray.Create(children));

        private static ProjectElement ElId(string tag, string name, ElementId id, params ProjectElement[] children) =>
            new ProjectElement(
                tag,
                id,
                ImmutableArray.Create<(string, string)>(("name", name)),
                children.Length == 0 ? ImmutableArray<ProjectElement>.Empty : ImmutableArray.Create(children));

        [Test]
        public void ProductResources_ProjectResourceChildren_AndExcludeScenesContainer()
        {
            var product = new ProductDefinition("_0x2101", "Tryk 2 tast", "01. Tryk",
                El("product_dataline", "Tryk 2 tast",
                    El("dataline_input", "Tryk (venstre)"),
                    El("dataline_input", "Tryk (højre)"),
                    El("dataline_output", "Udgang"),
                    El("scenes", "Scenarier")));

            var resources = product.Resources;

            Assert.Multiple(() =>
            {
                Assert.That(resources.Select(r => r.Tag),
                    Is.EqualTo(new[] { "dataline_input", "dataline_input", "dataline_output" }));
                Assert.That(resources.Select(r => r.Name),
                    Is.EqualTo(new[] { "Tryk (venstre)", "Tryk (højre)", "Udgang" }));
            });
        }

        [Test]
        public void ProductResources_EmptyBody_IsEmpty()
        {
            var product = new ProductDefinition("_0x2101", "Tom", "01. Tryk",
                El("product_dataline", "Tom"));

            Assert.That(product.Resources, Is.Empty);
        }

        [Test]
        public void ProductResources_ExcludeStructuralChildren_AndProjectIds()
        {
            // A "med logning" product body: real resources interleaved with structural children — an embedded
            // enum_definition (a typedef block) and a settings container (with a nested pin). Only the real resources
            // are resources; the structural children must not surface, and the projected ids must round-trip.
            var product = new ProductDefinition("_0x9f03", "Sensor", "03. Sensor",
                El("product_dataline", "Sensor",
                    El("enum_definition", "logtype"),
                    ElId("resource_temperature", "Measure 1", new ElementId(2, 0)),
                    ElId("resource_input", "Alarm flag", new ElementId(4, 0)),
                    El("settings", "Settings", El("dataline_input", "Sensor pin")),
                    El("scenes", "Scenarier")));

            var resources = product.Resources;

            Assert.Multiple(() =>
            {
                Assert.That(resources.Select(r => r.Tag),
                    Is.EqualTo(new[] { "resource_temperature", "resource_input" }));
                Assert.That(resources.Select(r => r.Id?.ToToken()),
                    Is.EqualTo(new[] { new ElementId(2, 0).ToToken(), new ElementId(4, 0).ToToken() }));
            });
        }

        [Test]
        public void ProductResources_ExcludeFamilySettingsContainers()
        {
            // Family-specific config containers — sms_modem_settings (rs485 modems), dimmer_settings (airlink dimmers)
            // — are structural like the generic 'settings' container, not I/O resources, so the resource preview must
            // exclude them too. Any '_settings'-suffixed container is treated as structural, so a new family's settings
            // block cannot leak in as a bogus resource (a hardcoded {settings} list silently missed these).
            var product = new ProductDefinition("_0x9f06", "SMS Modem", "05. Modem",
                El("product_rs485_sms_modem", "SMS Modem",
                    El("sms_modem_settings", "Pincode", El("sms_modem_pincode", "1234")),
                    El("sms_modem_settings", "Numbers 1-3", El("sms_modem_phonenumber", "1")),
                    El("dimmer_settings", "Dimmer config"),                 // any *_settings container is structural
                    ElId("resource_flag", "Status flag", new ElementId(2, 0))));

            var resources = product.Resources;

            Assert.Multiple(() =>
            {
                Assert.That(resources.Select(r => r.Tag), Is.EqualTo(new[] { "resource_flag" }));
                Assert.That(resources.Select(r => r.Name), Is.EqualTo(new[] { "Status flag" }));
            });
        }

        [Test]
        public void ProductResources_ChannelBasedFamily_SurfacesChannelAsResource_WithoutFlatteningInnerPins()
        {
            // An RS485 LED dimmer body (the one vendor family whose product root nests a sub-product container): the
            // root's direct resource children are a flag plus a rs485_led_dimmer_channel CONTAINER that is itself a
            // family resource and nests the real controls (increase/dimming) plus its own structural blocks
            // (dimmer_settings/scenes). Resources is a shallow direct-children preview: the channel surfaces as one
            // resource entry (a family resource, NOT structural), and the inner control pins are deliberately not
            // flattened — a false-positive guard that also documents the nested-channel contract.
            var product = new ProductDefinition("_0x4409", "LED Dimmer 2 kanaler", "05. Dimmer",
                El("product_rs485_led_dimmer", "LED Dimmer 2 kanaler",
                    ElId("resource_flag", "Status", new ElementId(2, 0)),
                    ElId("rs485_led_dimmer_channel", "Kanal 1", new ElementId(3, 0),
                        El("airlink_dimmer_increase", "Op"),
                        El("airlink_dimming", "Lys niveau"),
                        El("dimmer_settings", "Indstillinger"))));

            var resources = product.Resources;

            Assert.Multiple(() =>
            {
                // Exactly the flag and the channel container surface — the channel's own inner pins/settings
                // (airlink_dimmer_increase/airlink_dimming/dimmer_settings) do not, proving the shallow contract.
                Assert.That(resources.Select(r => r.Tag),
                    Is.EqualTo(new[] { "resource_flag", "rs485_led_dimmer_channel" }));
                Assert.That(resources.Select(r => r.Id?.ToToken()),
                    Is.EqualTo(new[] { new ElementId(2, 0).ToToken(), new ElementId(3, 0).ToToken() }));
            });
        }

        [Test]
        public void FunctionBlockProjections_ReadEachNamedContainer()
        {
            var fb = new FunctionBlockDefinition(
                "1.1.01", "e", "Kip tænd sluk", "1.1.01.e. Kip tænd sluk", "00. Foretrukne",
                El("functionblock", "1.1.01.e. Kip tænd sluk",
                    El("inputs", "inputs", El("resource_input", "Kip"), El("resource_input", "Sluk")),
                    El("outputs", "outputs", El("resource_output", "Udgang")),
                    El("settings", "settings", El("resource_timer", "Timer")),
                    El("internalsettings", "internalsettings"),
                    El("programs", "programs")));

            Assert.Multiple(() =>
            {
                Assert.That(fb.Inputs.Select(r => r.Name), Is.EqualTo(new[] { "Kip", "Sluk" }));
                Assert.That(fb.Outputs.Select(r => r.Name), Is.EqualTo(new[] { "Udgang" }));
                Assert.That(fb.Settings.Select(r => r.Name), Is.EqualTo(new[] { "Timer" }));
                Assert.That(fb.InternalVariables, Is.Empty);   // container present but empty
            });
        }

        [Test]
        public void FunctionBlockProjections_MissingContainer_IsEmpty()
        {
            // A block body that omits the outputs/settings/internalsettings containers entirely exercises the
            // absent-container branch (FindChild → null); each such projection must be empty, not throw.
            var fb = new FunctionBlockDefinition(
                "1.1.01", "e", "X", "1.1.01.e. X", "cat",
                El("functionblock", "1.1.01.e. X",
                    El("inputs", "inputs", El("resource_input", "In"))));

            Assert.Multiple(() =>
            {
                Assert.That(fb.Inputs.Select(r => r.Name), Is.EqualTo(new[] { "In" }));
                Assert.That(fb.Outputs, Is.Empty);
                Assert.That(fb.Settings, Is.Empty);
                Assert.That(fb.InternalVariables, Is.Empty);
            });
        }
    }
}
