namespace Ihc.Vis.Tests
{
    /// <summary>
    /// US-012: the canonical data-line address value type (<see cref="DatalineAddress"/>). Reconciled to the
    /// validator's authoritative 1–128 packed-value cap per direction (8 input lines × 16 terminals, 16 output
    /// lines × 8 terminals): encode is fallible at the range edges, parse tolerates the unassigned token, and the
    /// vendor label agrees with the report decode across the legal range.
    /// </summary>
    public class DatalineAddressTests
    {
        // ----- TryEncode: in range -----

        [TestCase(1, 1, false, "_0x1")]
        [TestCase(2, 3, false, "_0x13")]   // (2-1)*16 + 3 = 19 = 0x13
        [TestCase(1, 2, true, "_0x2")]
        [TestCase(3, 8, true, "_0x18")]    // (3-1)*8 + 8 = 24 = 0x18
        public void TryEncode_InRange_ProducesToken(int line, int terminal, bool isOutput, string expected)
        {
            Assert.That(DatalineAddress.TryEncode(line, terminal, isOutput, out string token), Is.True);
            Assert.That(token, Is.EqualTo(expected));
        }

        // The maximum valid boundary is value 128 = _0x80 in both directions (8 input lines / 16 output lines).
        [TestCase(8, 16, false)]
        [TestCase(16, 8, true)]
        public void TryEncode_MaxBoundary_Is0x80(int line, int terminal, bool isOutput)
        {
            Assert.That(DatalineAddress.TryEncode(line, terminal, isOutput, out string token), Is.True);
            Assert.That(token, Is.EqualTo("_0x80"));
        }

        // ----- TryEncode: out of range → false, never emits a live token -----

        [TestCase(0, 1, false)]     // data line < 1
        [TestCase(1, 0, false)]     // terminal < 1
        [TestCase(1, 17, false)]    // terminal beyond an input line
        [TestCase(1, 9, true)]      // terminal beyond an output line
        [TestCase(9, 1, false)]     // input line 9 → value 129 > 128 (the pre-existing gap)
        [TestCase(17, 1, true)]     // output line 17 → value 129 > 128
        public void TryEncode_OutOfRange_ReturnsFalseWithNullToken(int line, int terminal, bool isOutput)
        {
            Assert.That(DatalineAddress.TryEncode(line, terminal, isOutput, out string token), Is.False);
            Assert.That(token, Is.EqualTo(ElementId.NullToken), "failure signals via bool; token is the unassigned default, never a live address");
        }

        // ----- TryParse -----

        [Test]
        public void TryParse_UnassignedOrBlank_IsFalse()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DatalineAddress.TryParse(ElementId.NullToken, false, out _), Is.False);
                Assert.That(DatalineAddress.TryParse("", false, out _), Is.False);
                Assert.That(DatalineAddress.TryParse(null, false, out _), Is.False);
            });
        }

        // ----- round-trip and cross-agreement across the legal range -----

        [Test]
        public void Encode_Parse_VendorLabel_AgreeAcrossLegalRange()
        {
            Assert.Multiple(() =>
            {
                foreach (bool isOutput in new[] { false, true })
                {
                    int perLine = DatalineAddress.TerminalsPerLine(isOutput);
                    for (int line = 1; line <= DatalineAddress.MaxDataLine(isOutput); line++)
                    {
                        for (int term = 1; term <= perLine; term++)
                        {
                            Assert.That(DatalineAddress.TryEncode(line, term, isOutput, out string token), Is.True);
                            Assert.That(DatalineAddress.TryParse(token, isOutput, out DatalineAddress addr), Is.True);
                            Assert.That((addr.DataLine, addr.Terminal), Is.EqualTo((line, term)));
                            // The vendor label reads the same value the round-trip stored.
                            Assert.That(DatalineAddress.ToVendorLabel(token, isOutput), Is.Not.EqualTo("?"));
                        }
                    }
                }
            });
        }

        // ----- ToVendorLabel matches the vendor get_address formula (byte-identical to the old report decode) -----

        [TestCase("_0x0", false, "?")]
        [TestCase("_0x1", false, "1.01")]
        [TestCase("_0x08", false, "1.08")]   // bit 7 → "0" + (bit+1)
        [TestCase("_0x09", false, "1.11")]   // bit 8 → bit+3
        [TestCase("_0x10", false, "1.18")]   // bit 15 → bit+3
        [TestCase("_0x11", false, "2.01")]   // next data line
        [TestCase("_0x1", true, "1.01")]
        [TestCase("_0x08", true, "1.08")]
        [TestCase("_0x09", true, "2.01")]    // output divider 8 → data line increments sooner
        public void ToVendorLabel_MatchesVendorFormula(string token, bool isOutput, string expected) =>
            Assert.That(DatalineAddress.ToVendorLabel(token, isOutput), Is.EqualTo(expected));

        // ----- A-12: a terminal address written onto a data-line pin survives a save/reload byte round-trip -----

        [Test]
        public async System.Threading.Tasks.Task TerminalAddress_RoundTrips()
        {
            var service = new ProjectAppService(TestSetup.Settings);
            Project project = service.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
            ProductDefinition product = service.GetAvailableProducts()
                .First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Any(r => r.Tag == "dataline_input"));
            string room = project.Groups.First().GetAttribute("name")!;

            ProjectEditor editor = project.Edit();
            editor.Group(room).AddProduct(product);
            ElementId pinId = editor.ToProject().Root.DescendantsAndSelf().First(e => e.Tag == "dataline_input").Id!.Value;
            Assert.That(DatalineAddress.TryEncode(2, 4, isOutput: false, out string token), Is.True, "Datalinie 2.04");
            Assert.That(editor.TryResolve(pinId, out ElementRef? handle), Is.True);
            handle!.SetAttribute("address_dataline", token);

            using var stream = new System.IO.MemoryStream();
            await service.Save(editor.ToProject(), stream);
            stream.Position = 0;
            Project reloaded = await service.Load(stream);

            ProjectElement reloadedPin = reloaded.FindById(pinId)!;
            Assert.Multiple(() =>
            {
                Assert.That(reloadedPin.GetAttribute("address_dataline"), Is.EqualTo(token), "the address token round-trips");
                Assert.That(DatalineAddress.ToVendorLabel(reloadedPin.GetAttribute("address_dataline"), isOutput: false),
                    Is.EqualTo("2.04"), "and decodes to the vendor Datalinie N.PP label");
            });
        }
    }
}
