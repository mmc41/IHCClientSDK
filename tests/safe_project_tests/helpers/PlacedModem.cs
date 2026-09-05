using System;
using System.Linq;
using System.Threading.Tasks;

using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// One freshly inserted SMS modem, open in a session, with its PIN field resolved.
    ///
    /// <para>The modem's <i>Pin Kode</i> is the one shipped product field whose catalog element declares numeric
    /// bounds, so every fixture asking what the engine does with a declared bound — the bound checks themselves,
    /// the non-numeric refusal, the unreadable-declaration refusal — needs exactly this arrangement. Shared
    /// rather than repeated, because a second copy is how the two come to place different products and answer
    /// different questions while appearing to ask one.</para>
    ///
    /// <para>Distinct from <see cref="ModemDialogOracle"/>, which fixes a CLOCK as well because it compares
    /// saved bytes. Nothing here depends on when it runs, so nothing here fixes one.</para>
    /// </summary>
    internal static class PlacedModem
    {
        /// <summary>The SMS modem's catalog <c>product_identifier</c>.</summary>
        public const string ProductIdentifier = ModemDialogOracle.ModemProductId;

        /// <summary>The automation-id suffix of the PIN field — the bounded one.</summary>
        public const string PinFieldSuffix = "indstillinger.pinkode";

        /// <summary>
        /// A session over the corpus project with one SMS modem inserted, and the PIN field its dialog offers.
        /// </summary>
        public static async Task<(ProjectDocumentSession Session, ElementId ProductId, DialogDescriptorField Pin)> Open()
        {
            ProjectAppService app = new(TestSetup.Settings);
            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            ProductDefinition modem = app.GetAvailableProducts()
                .First(p => p.ProductIdentifier == ProductIdentifier);

            ProjectDocumentSession session = new();
            session.Open(project);
            ElementId id = session.Apply(new AddProduct(locality, modem)).Value;

            DialogDescriptorField pin = app.GetProductDialog(session.Current!, id)
                .AllFields.Single(f => f.AutomationId.EndsWith(PinFieldSuffix, StringComparison.Ordinal));
            return (session, id, pin);
        }
    }
}
