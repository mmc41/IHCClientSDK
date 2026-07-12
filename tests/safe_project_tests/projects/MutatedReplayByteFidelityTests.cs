namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The mutation-session byte-fidelity gate against the authentic vendor oracle
    /// <c>project3-KompleksWired-mutated.vis</c> (IHC Visual 03.04.72.03 after three recorded editing actions on
    /// <c>project3-KompleksWired.vis</c>, single save). The SDK loads the original, reproduces the vendor's one-time
    /// load-time enum re-hoist (<see cref="ProjectEditor.NormalizeCatalogEnums"/> — Action 0), replays the three
    /// mutations in allocation order — A: insert catalog product "SMS Modem" (<c>_0x3103</c>) into Garage, whose four
    /// element types are absent from project3's DTD, so this is the one oracle exercising DTD block <b>generation</b>
    /// (not passthrough); B: author the empty enum <c>MutOracleEnum</c>; C: delete the FUGA product owning all three
    /// follow-link from-ends, cascading the paired link rows and regenerating the DTD — then restamps to the oracle's
    /// clock and asserts byte-identity. The product definition comes from the SDK-embedded catalog, so the replay
    /// runs unconditionally (no install dir).
    /// </summary>
    public class MutatedReplayByteFidelityTests
    {
        private const string Original = "project3-KompleksWired.vis";
        private const string MutatedOracle = "project3-KompleksWired-mutated.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        // ---- Full replay: Action 0 → A (SMS Modem insert) → B (empty enum) → C (FUGA delete) → byte-identity ----

        [Test]
        public async Task Mutations_ReplayProject3MutatedOracle_ByteIdentical()
        {
            ProductDefinition smsModem = new ProjectAppService(Settings).GetAvailableProducts()
                .Single(p => p.ProductIdentifier == "_0x3103");   // "SMS Modem" (inserted live via menu command 24773)

            // id2=_0x40c1836 decodes to day 4 / hour 12 / min 24 / sec 54; <modified> is minute-precision (12:24),
            // so the second (54) lives only in id2 and must be supplied to the restamp clock.
            await ReplayOracle.AssertReplaysByteIdentical(Original, MutatedOracle,
                new DateTimeOffset(2026, 7, 4, 12, 24, 54, TimeSpan.Zero),
                editor =>
                {
                    editor.Group("Garage").AddProduct(smsModem);  // A: _0x57a.._0x59d (36 ids) + 4 generated DTD blocks
                    editor.AddEnumDefinition("MutOracleEnum");    // B: empty def _0x59e47, appended to enum_definitions
                    editor.DeleteById(TestData.Id("_0x5153"));    // C: FUGA delete → paired-link cascade + DTD regen
                });
        }
    }
}
