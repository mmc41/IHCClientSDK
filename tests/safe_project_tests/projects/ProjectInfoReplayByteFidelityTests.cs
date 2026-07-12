namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The Projektinfo/metadata authoring byte-fidelity gate (G4, US-039) for
    /// <see cref="ProjectEditor.SetProjectInfo"/> / <see cref="ProjectEditor.SetCustomerInfo"/> /
    /// <see cref="ProjectEditor.SetInstallerInfo"/> against the authentic vendor oracle
    /// <c>project3-KompleksWired-projektinfo.vis</c> (IHC Visual 03.04.72.03 after one recorded Dokumentation ▸
    /// Projektinfo dialog session on <c>project3-KompleksWired.vis</c>, single save). The SDK loads the original,
    /// reproduces the vendor's one-time load-time enum re-hoist (<see cref="ProjectEditor.NormalizeCatalogEnums"/> —
    /// Action 0), replays the dialog — every field filled with its unique A1 marker, customer "Mobil telefon"
    /// deliberately blank — then restamps to the oracle's clock and asserts byte-identity. Pinned vendor semantics
    /// (ENG-A1): attribute order is DTD-declared order (never fill order); a blank field ⇒ the attribute is
    /// OMITTED (never <c>=""</c>); escaping <c>&amp;</c> → <c>&amp;amp;</c>, <c>"</c> → <c>&amp;quot;</c>, æøå →
    /// raw Latin-1 bytes; <c>udf</c> never written; metadata edits allocate nothing (<c>last_unique_id</c> stays
    /// <c>_0x579</c>). All verbs are catalog-free, so these run unconditionally.
    /// </summary>
    public class ProjectInfoReplayByteFidelityTests
    {
        private const string Original = "project3-KompleksWired.vis";
        private const string ProjektinfoOracle = "project3-KompleksWired-projektinfo.vis";

        // ---- Full replay: Action 0 → P (Projektinfo dialog) → byte-identity ----

        [Test]
        public async Task EnterProjektinfo_ReplaysProject3InfoOracle_ByteIdentical() =>
            // id2=_0xb0d001e decodes to day 11 / hour 13 / min 0 / sec 30; <modified> is minute-precision (13:00),
            // so the second (30) lives only in id2 and must be supplied to the restamp clock.
            await ReplayOracle.AssertReplaysByteIdentical(Original, ProjektinfoOracle,
                new DateTimeOffset(2026, 7, 11, 13, 0, 30, TimeSpan.Zero),
                ApplyProjektinfoDialog);             // P: the one recorded dialog session

        // ---- Composition isolation: the id-less metadata blocks own no ids and mint none ----

        [Test]
        public async Task MetadataEdits_AllocateNoIds()
        {
            Project original = await ReplayOracle.LoadProject(Original);

            ProjectEditor editor = original.Edit();
            ApplyProjektinfoDialog(editor);
            Project after = editor.ToProject();

            Assert.That(after.LastUniqueId, Is.EqualTo(original.LastUniqueId),
                "metadata edits allocate nothing (ENG-A1: last_unique_id unchanged)");
        }

        // The dialog fills every field with its unique marker; customer "Mobil telefon" is deliberately blank —
        // replayed as MobilePhone("") to exercise blank ⇒ attribute-omitted against the oracle bytes. The project
        // half carries the escaping probes: æøå (raw Latin-1), & (&amp;), " (&quot;).
        private static void ApplyProjektinfoDialog(ProjectEditor editor) =>
            editor.SetCustomerInfo(c => c.Name("kundeNavn-A1").Address("kundeVej-A1").City("kundeBy-A1")
                                         .ZipCode("kundePost-A1").Country("kundeLand-A1").Phone("kundeTlf-A1")
                                         .MobilePhone("").Email("kunde-email-A1"))
                  .SetInstallerInfo(i => i.Name("instNavn-A1").Address("instVej-A1").City("instBy-A1")
                                          .ZipCode("instPost-A1").Country("instLand-A1").Phone("instTlf-A1")
                                          .MobilePhone("instMob-A1").Email("inst-email-A1"))
                  .SetProjectInfo(p => p.Programmer("prog-æøå-A1").Number("num-A1").Drawing("draw&A1")
                                        .Type("type-A1").Description("desc-\"Q\"-A1"));
    }
}
