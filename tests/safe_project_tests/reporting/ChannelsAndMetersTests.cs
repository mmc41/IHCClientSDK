using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ihc.Tests.Shared;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// RL-5b / finding G4 (a and c): an LED dimmer's per-channel documentation and an S0 meter's
    /// <c>ticks</c> reached no report at all, though both carry real installer content — which channel
    /// drives what, its Id-kode and Lysgruppe, and the meter's pulse calibration.
    /// <para>The owner's layout ruling (2026-08-22) is the <b>combined</b> form, chosen because it is the
    /// pattern this report already uses for dataline terminals: a terminal appears compactly in its
    /// locality block AND completely in a flat cross-reference. So a channel now appears twice too — a
    /// compact three-column sub-table under the dimmer's own component block (the locality grid is a fixed
    /// three-column layout, so Id-kode and Lysgruppe cannot fit there), and the full field set in a
    /// Full-only "Kanaler og målere" section beside Terminal-forbindelser.</para>
    /// <para>Both are FULL mode only (C-3: the vendor's report showed neither), and a channel with nothing
    /// stored is still listed — the ruling is explicit that an empty channel should be visible as having
    /// nothing to say, rather than silently absent.</para>
    /// </summary>
    public class ChannelsAndMetersTests
    {
        private static readonly Lazy<ProjectAppService> Service =
            new(() => new ProjectAppService(TestSetup.Settings, new BuiltInCatalog(), ReportOracleHarness.Clock()));

        private static Project Load(string name) =>
            Service.Value.Load(new MemoryStream(TestData.ReadBytes(Path.Combine("projects", name))))
                .GetAwaiter().GetResult();

        private static async Task<string> Report(string fixture, ReportMode mode)
        {
            using var output = new MemoryStream();
            await Service.Value.GenerateReport(Load(fixture), ReportKind.Installation, mode,
                ReportMimeTypes.PlainText, output);
            return Encoding.UTF8.GetString(output.ToArray());
        }

        // The compact view: under the dimmer's own component block, in the locality section.
        [Test]
        public async Task Channels_AppearUnderTheirDimmersComponentBlock_FullOnly_G4a()
        {
            string full = await Report("project5-Dokumentation.vis", ReportMode.Full);
            string standard = await Report("project5-Dokumentation.vis", ReportMode.Standard);

            Assert.Multiple(() =>
            {
                Assert.That(ReportProbe.Renders(full, "_0x837b"), Is.True,
                    "G4a: the dimmer's first channel owns a row in its product's component block");
                Assert.That(ReportProbe.Renders(full, "_0x947b"), Is.True, "G4a: and so does the second");
                Assert.That(standard, Does.Not.Contain("LED Dimmer kanal"),
                    "C-3: Standard is the vendor-parity surface and the vendor's report showed no channels");
            });
        }

        // The complete view: its own Full-only section, carrying the fields the locality grid cannot hold.
        [Test]
        public async Task ChannelsAndMeters_HaveTheirOwnFullOnlySection_G4a_G4c()
        {
            string full = await Report("project5-Dokumentation.vis", ReportMode.Full);
            string standard = await Report("project5-Dokumentation.vis", ReportMode.Standard);

            string[] channels = ReportProbe.TableRows(full, "LED dimmer kanaler");
            string[] meters = ReportProbe.TableRows(full, "S0 målere");

            Assert.Multiple(() =>
            {
                Assert.That(full, Does.Contain("Kanaler og målere"), "the section renders");
                Assert.That(channels, Has.Length.EqualTo(2), "one row per channel");
                Assert.That(channels[0], Does.Contain("S3-01-A").And.Contain("K/12"),
                    "G4a: the Id-kode and Lysgruppe that do not fit the three-column locality grid are here");
                Assert.That(meters, Has.Length.EqualTo(1), "one row per S0 device");
                Assert.That(meters[0], Does.Contain("1000"),
                    "G4c: the meter's ticks reach a report for the first time — in a NEW table, which is "
                    + "what makes this expressible at all (a new column on the existing Common S0 table "
                    + "would have changed Standard)");
                Assert.That(standard, Does.Not.Contain("Kanaler og målere"),
                    "C-3: the whole section is Full-only");
            });
        }

        // The owner's explicit call: an empty channel is listed, so its emptiness is visible.
        [Test]
        public async Task ChannelsWithNothingStored_AreStillListed_G4a()
        {
            string full = await Report("project3-KompleksWired-enduserdoc.vis", ReportMode.Full);

            string[] channels = ReportProbe.TableRows(full, "LED dimmer kanaler");

            Assert.Multiple(() =>
            {
                Assert.That(channels, Has.Length.EqualTo(2),
                    "project3's two channels store nothing at all — they are listed anyway, so a reader can "
                    + "see the dimmer has channels that nobody documented, rather than see nothing");
                Assert.That(ReportProbe.Renders(full, "_0x5297b"), Is.True,
                    "and each still owns its row in the locality block, where the blank fields render as "
                    + "the A1 '--' placeholder");
            });
        }
    }
}
