using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Products;
using Ihc.Vis.Session;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The shared fixture behind the SMS-modem properties-dialog byte oracle: one deterministic project holding a
    /// single freshly inserted SMS modem, the full 30-slot edit payload its dialog can produce, and the save that
    /// turns the two into bytes.
    /// <para>
    /// It exists so the SAME edit can be driven through two different commands and compared byte-for-byte: the
    /// bespoke <c>UpdateModem</c> (retired in T031) and the generic dialog write-back that replaced it. Everything
    /// that could vary between the two runs is fixed here — the creation clock, the catalog, the insertion point
    /// and the payload — so a byte difference can only come from the command under test.
    /// </para>
    /// </summary>
    internal static class ModemDialogOracle
    {
        /// <summary>The committed oracle, relative to the testdata root.</summary>
        public const string OracleFile = "projects/Synthetic/ModemDialog30Slots.vis";

        /// <summary>The SMS modem's catalog <c>product_identifier</c>.</summary>
        public const string ModemProductId = "_0x3103";

        /// <summary>The slot count the catalog product declares (three <c>sms_modem_settings</c> groups of ten).</summary>
        public const int PhoneSlots = 30;

        /// <summary>The PIN the edit writes, distinct from the catalog default 1234 so the write is observable.</summary>
        public const string PinCode = "4711";

        // A fixed creation moment makes id1/id2/modified deterministic; the save writes metadata verbatim on top of
        // that, so nothing in the produced bytes depends on when the test runs.
        private static readonly DateTimeOffset Created = new(2026, 8, 11, 9, 30, 0, TimeSpan.Zero);

        /// <summary>A service over the SDK-embedded catalog and the fixed clock — no IHC Visual install needed.</summary>
        public static ProjectAppService App() =>
            new(TestSetup.Settings, new BuiltInCatalog(), new FakeTimeProvider(Created));

        /// <summary>
        /// A new project with one SMS modem inserted into its first locality through the ordinary
        /// <see cref="AddProduct"/> command — the state a user is in the moment the properties dialog opens.
        /// </summary>
        public static (Project Project, ElementId ModemId) NewProjectWithModem(ProjectAppService app)
        {
            Project created = app.CreateNew(new ProjectDetails("Morten Christensen", "Morten", "Danmark"));
            ElementId locality = created.Groups.First().Id!.Value;
            ProductDefinition modem = new BuiltInCatalog().Product(ModemProductId);

            var session = new ProjectDocumentSession();
            session.Open(created);
            EditOutcome<ElementId> inserted = session.Apply(new AddProduct(locality, modem));
            Assert.That(inserted.Status, Is.EqualTo(EditStatus.Committed),
                "the fixture cannot pin a dialog edit if inserting the modem itself did not commit");

            return (session.Current!, inserted.Value);
        }

        // Payload() — the bespoke ModemPropertiesResult this fixture used to build — is gone with the command it
        // fed (T031). What it recorded survives where it counts: the committed .vis oracle was produced BY that
        // command, before the swap, so comparing today's generic replay against the FILE is still a comparison
        // against UpdateModem's output. That is exactly why the target was recorded as BYTES rather than as a
        // same-run comparison between the two commands — the target outlives the code that made it.
        //
        // Its two deliberate choices still shape EditsFor below, and still matter: Navn was the value AddProduct
        // had already stamped (a no-op, so a dialog presenting it read-only produces the same bytes), and Position
        // was empty (the DTD default, dropped on serialize). Neither appears in the generic replay, and the bytes
        // are identical because of those two facts and not by luck.

        /// <summary>The 30 slot values, slot <c>n</c> ending in <c>n</c> so a mis-ordered write is visible.</summary>
        public static IReadOnlyList<string> PhoneNumbers() =>
            Enumerable.Range(1, PhoneSlots)
                .Select(slot => "+457010" + slot.ToString("D4", CultureInfo.InvariantCulture))
                .ToArray();

        /// <summary>
        /// The full 30-slot edit, expressed as the generic write-back's pre-resolved triples.
        /// <para>Built from the composed descriptor rather than hand-listed, because that is how a caller builds
        /// it: the descriptor has already resolved every binding to an element id, so the two commands differed
        /// only in how the values travel — not in which elements they reach.</para>
        /// <para>Two fields the retired bespoke payload carried are deliberately ABSENT here, and saying why is
        /// what makes the byte comparison meaningful rather than lucky. <c>Navn</c> is read-only on this family, so
        /// the generic command refuses it — and the payload only ever re-wrote the name it had been handed, a
        /// no-op. <c>Position</c> is empty, which is that attribute's DTD default and therefore dropped on
        /// serialize, so writing it and not writing it produce identical bytes.</para>
        /// </summary>
        public static ImmutableArray<ProductDialogEdit> EditsFor(ProductDialogDescriptor dialog)
        {
            var byId = dialog.AllFields.ToDictionary(f => f.AutomationId);
            var edits = ImmutableArray.CreateBuilder<ProductDialogEdit>();

            void Set(string automationId, string value)
            {
                DialogDescriptorField field = byId[automationId];
                edits.Add(new ProductDialogEdit(field.Target, field.Attribute, value));
            }

            Set("dlg.identitet.note", "Modem i teknikskab, SIM fra TDC");
            Set("dlg.identitet.idkode", "MOD-01");
            Set("dlg.kabling.lf0v", "Sort");
            Set("dlg.kabling.lf24v", "Rød");
            Set("dlg.kabling.lfmin", "Grøn");
            Set("dlg.kabling.lfplus", "Gul");
            Set("dlg.indstillinger.pinkode", PinCode);

            IReadOnlyList<string> phones = PhoneNumbers();
            for (int slot = 1; slot <= PhoneSlots; slot++)
            {
                Set("dlg.telefonnumre.nummer." + slot.ToString(CultureInfo.InvariantCulture), phones[slot - 1]);
            }
            return edits.ToImmutable();
        }

        /// <summary>Serializes verbatim — the fixed creation stamps survive, so the bytes are reproducible.</summary>
        public static async Task<byte[]> SaveBytes(ProjectAppService app, Project project)
        {
            using var ms = new MemoryStream();
            await app.Save(project, ms, ProjectSaveOptions.PreserveExistingMetadata);
            return ms.ToArray();
        }
    }
}
