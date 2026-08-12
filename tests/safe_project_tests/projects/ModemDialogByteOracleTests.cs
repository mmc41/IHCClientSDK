using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Characterization: the exact <c>.vis</c> bytes a full 30-slot SMS-modem dialog edit produces through the
    /// bespoke <c>UpdateModem</c> command (retired in T031).
    /// <para>
    /// This is not a vendor oracle — <c>ModemDialog30Slots.vis</c> is a snapshot of this repository's OWN output,
    /// recorded while <c>UpdateModem</c> was still the only write-back path. Its job is to give the generic
    /// dialog write-back that replaced it a byte target that PREDATES the swap, so the
    /// replacement can be proven to change nothing about the file. Regenerating it to make a failing test pass
    /// would destroy exactly the evidence it exists to hold: if these bytes move, something about how a modem edit
    /// is serialized moved with them, and that is the finding.
    /// </para>
    /// </summary>
    public class ModemDialogByteOracleTests
    {
        // The companion test that drove the same edit through the bespoke UpdateModem is gone with the command
        // (T031) — and the oracle is precisely why deleting it was safe. These bytes were produced BY that
        // command, so the replay below still measures against its output. A recorded byte target keeps its
        // evidentiary value after its producer is deleted; a same-run comparison between two live commands could
        // not have. That is the concrete argument for recording bytes rather than diffing implementations.

        /// <summary>
        /// T025 — the swap is byte-neutral. The SAME modem edit, driven through the GENERIC
        /// <see cref="ApplyProductDialog"/> instead of the bespoke command, must produce the
        /// oracle recorded in T002 before either path could see it.
        /// <para>This is the test the metadata engine is finally answerable to. Everything else about the swap is
        /// judgement — captions, layout, which control kind a field gets — but the FILE either comes out identical
        /// or the engine has quietly changed what an installer's project contains. Comparing against a RECORDED
        /// oracle rather than against a live <c>UpdateModem</c> run in the same test is deliberate: a same-run
        /// comparison would still pass if both commands broke in the same way, whereas an oracle written before
        /// the new code existed cannot drift toward it.</para>
        /// </summary>
        [Test]
        public async Task ApplyProductDialog_ProducesTheSameBytesAsUpdateModem()
        {
            ProjectAppService app = ModemDialogOracle.App();
            (Project project, ElementId modemId) = ModemDialogOracle.NewProjectWithModem(app);
            var session = new ProjectDocumentSession();
            session.Open(project);

            ProductDialogDescriptor dialog = app.GetProductDialog(session.Current!, modemId);
            EditOutcome outcome = session.Apply(
                new ApplyProductDialog(modemId, ModemDialogOracle.EditsFor(dialog)));
            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed), "refusal reason: " + outcome.Reason);

            byte[] actual = await ModemDialogOracle.SaveBytes(app, session.Current!);
            TestData.AssertBytesIdentical(
                TestData.ReadBytes(ModemDialogOracle.OracleFile), actual, "ApplyProductDialog 30-slot edit");
        }

        /// <summary>
        /// The replay is only evidence if it actually wrote the whole edit. A triple list that silently dropped the
        /// cabling or the PIN could still match the oracle if those values happened to equal their defaults — so
        /// the resulting element is inspected directly as well.
        /// </summary>
        [Test]
        public async Task TheGenericReplay_WritesEveryFieldTheBespokeCommandDoes()
        {
            ProjectAppService app = ModemDialogOracle.App();
            (Project project, ElementId modemId) = ModemDialogOracle.NewProjectWithModem(app);
            var session = new ProjectDocumentSession();
            session.Open(project);
            ProductDialogDescriptor dialog = app.GetProductDialog(session.Current!, modemId);

            session.Apply(new ApplyProductDialog(modemId, ModemDialogOracle.EditsFor(dialog)));

            ProjectElement modem = session.Current!.FindById(modemId)!;
            Assert.Multiple(() =>
            {
                Assert.That(modem.GetAttribute("note"), Is.EqualTo("Modem i teknikskab, SIM fra TDC"));
                Assert.That(modem.GetAttribute("documentation_tag"), Is.EqualTo("MOD-01"));
                Assert.That(modem.GetAttribute("cablecolour_0V"), Is.EqualTo("Sort"));
                Assert.That(modem.GetAttribute("cablecolour_RS485Plus"), Is.EqualTo("Gul"));
                Assert.That(modem.Descendants().First(e => e.Tag == "sms_modem_pincode").GetAttribute("value"),
                    Is.EqualTo(ModemDialogOracle.PinCode));
                Assert.That(modem.Descendants().Count(e => e.Tag == "sms_modem_phonenumber"
                                                           && e.GetAttribute("phonenumber") is { Length: > 0 }),
                    Is.EqualTo(30), "all thirty slots carry a number");
            });
        }

        // The oracle is only a useful byte target if it actually carries the whole edit. A payload silently
        // truncated to the four slots the current dialog renders (F-52) would still produce stable bytes and still
        // pass the comparison above — and would then let the generic command "match" while writing 26 fewer fields.
        [Test]
        public async Task TheOracle_CarriesAllThirtyPhoneNumbers()
        {
            ProjectAppService app = ModemDialogOracle.App();
            (Project project, ElementId modemId) = ModemDialogOracle.NewProjectWithModem(app);
            var session = new ProjectDocumentSession();
            session.Open(project);
            IReadOnlyList<string> expected = ModemDialogOracle.PhoneNumbers();
            ProductDialogDescriptor dialog = app.GetProductDialog(session.Current!, modemId);

            session.Apply(new ApplyProductDialog(modemId, ModemDialogOracle.EditsFor(dialog)));

            ProjectElement modem = session.Current!.FindById(modemId)!;
            List<ProjectElement> slots = modem.Descendants()
                .Where(e => e.Tag == "sms_modem_phonenumber")
                .OrderBy(e => int.Parse(e.GetAttribute("address")!, CultureInfo.InvariantCulture))
                .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(slots, Has.Count.EqualTo(ModemDialogOracle.PhoneSlots),
                    "the catalog product declares 30 slots");
                Assert.That(slots.Select(s => s.GetAttribute("phonenumber")), Is.EqualTo(expected).AsCollection,
                    "every slot carries its own value, in address order");
                Assert.That(
                    modem.Descendants().First(e => e.Tag == "sms_modem_pincode").GetAttribute("value"),
                    Is.EqualTo(ModemDialogOracle.PinCode));
            });
        }
    }
}
