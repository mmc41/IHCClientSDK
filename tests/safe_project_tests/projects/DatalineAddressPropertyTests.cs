using System.Globalization;
using System.Linq;
using CsCheck;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Property-based laws for the canonical data-line address (US-012). <see cref="DatalineAddressTests"/> pins
    /// the vendor formula against measured examples and walks the legal coordinate grid; this pins the three laws
    /// that hold over the whole <c>_0x</c> token alphabet, which examples can only sample:
    /// <list type="number">
    /// <item>encode and parse are inverse, and encode refuses exactly the illegal coordinates;</item>
    /// <item>the vendor label reads the same address the parse reads, whichever letter case the digits are in
    /// (the divergence T002 fixed lived here, and only for uppercase);</item>
    /// <item>the deliberate leniency at the top end: packed values above the 1–128 cap still parse, so a report
    /// keeps showing an out-of-range stored address, even though encode will never mint one (D04).</item>
    /// </list>
    /// </summary>
    public class DatalineAddressPropertyTests
    {
        /// <summary>Hex digits in both cases — the token alphabet a real <c>.vis</c> file can carry.</summary>
        private const string HexDigits = "0123456789abcdefABCDEF";

        /// <summary>Non-hex characters a corrupt token might carry. Whitespace is deliberately excluded: see
        /// <see cref="ToVendorLabel_AndParse_AgreeOnRefusingMalformedTokens"/>.</summary>
        private const string NonHexDigits = "gGzZ%+._-";

        /// <summary>The legal token shape: <c>_0x</c> plus one or two hex digits, in either case.</summary>
        private static readonly Gen<string> LegalShapeToken =
            Gen.OneOfConst(HexDigits.ToCharArray()).Array[1, 2].Select(cs => "_0x" + new string(cs));

        /// <summary>The same shape, but the digits may be junk.</summary>
        private static readonly Gen<string> MalformedShapeToken =
            Gen.OneOfConst((HexDigits + NonHexDigits).ToCharArray()).Array[1, 2].Select(cs => "_0x" + new string(cs));

        /// <summary>The label the token's parsed address implies — what <c>ToVendorLabel</c> must independently produce.</summary>
        private static string LabelFromParse(string token, bool isOutput) =>
            DatalineAddress.TryParse(token, isOutput, out DatalineAddress address)
                ? $"{address.DataLine}.{DatalineAddress.TerminalLabel(address.Terminal)}"
                : "?";

        /// <summary>
        /// Law 1. Encode succeeds for exactly the legal coordinates — data line and terminal both 1-based, the
        /// terminal within its direction's line, and the packed value within the 1–128 cap — it emits the
        /// unassigned token on refusal, and whatever it emits parses back to the coordinates it was given.
        /// </summary>
        [Test]
        public void Encode_And_Parse_AreInverse_AndEncodeRefusesExactlyTheIllegalCoordinates()
        {
            Gen.Select(Gen.Int[-2, 20], Gen.Int[-2, 20], Gen.Bool).Sample(t =>
            {
                (int line, int terminal, bool isOutput) = t;
                int perLine = DatalineAddress.TerminalsPerLine(isOutput);
                bool legal = line >= 1 && terminal >= 1 && terminal <= perLine
                             && (line - 1) * perLine + terminal <= DatalineAddress.MaxAddressValue;

                bool encoded = DatalineAddress.TryEncode(line, terminal, isOutput, out string token);
                if (encoded != legal)
                {
                    return false;
                }
                if (!encoded)
                {
                    return token == ElementId.NullToken;
                }
                return DatalineAddress.TryParse(token, isOutput, out DatalineAddress back)
                       && back == new DatalineAddress(line, terminal);
            }, iter: 2000);
        }

        /// <summary>
        /// Law 2. For any legally shaped token the vendor label reads the address the parse reads, and letter case
        /// is invisible to both. This is the law T002's defect broke: uppercase digits parsed but labelled "?".
        /// </summary>
        [Test]
        public void ToVendorLabel_ReadsTheSameAddressAsParse_InEitherHexCase()
        {
            Gen.Select(LegalShapeToken, Gen.Bool).Sample(t =>
            {
                (string token, bool isOutput) = t;
                string digits = token.Substring(3);
                string upper = "_0x" + digits.ToUpperInvariant();
                string lower = "_0x" + digits.ToLowerInvariant();
                string label = DatalineAddress.ToVendorLabel(token, isOutput);

                return label == LabelFromParse(token, isOutput)
                       && label == DatalineAddress.ToVendorLabel(upper, isOutput)
                       && label == DatalineAddress.ToVendorLabel(lower, isOutput);
            }, iter: 2000);
        }

        /// <summary>
        /// Law 2, negative half: a token whose digits are not hex is refused by both readers alike, so a corrupt
        /// attribute can never be shown as an address. Whitespace is out of the alphabet on purpose — the parse
        /// tolerates it (<c>NumberStyles.HexNumber</c> allows surrounding white space) where the label does not,
        /// and no writer in this engine can emit it.
        /// </summary>
        [Test]
        public void ToVendorLabel_AndParse_AgreeOnRefusingMalformedTokens()
        {
            Gen.Select(MalformedShapeToken, Gen.Bool).Sample(t =>
            {
                (string token, bool isOutput) = t;
                return DatalineAddress.ToVendorLabel(token, isOutput) == LabelFromParse(token, isOutput);
            }, iter: 2000);
        }

        /// <summary>
        /// Law 3 (D04). Above the 1–128 cap the two directions part company on purpose: a stored value of 129–255
        /// still parses and still labels, so the validator's out-of-range finding can quote what the file actually
        /// holds, while encode refuses to mint such an address in the first place.
        /// </summary>
        [Test]
        public void PackedValuesAboveTheCap_StillParseAndLabel_ButCanNeverBeEncoded()
        {
            Gen.Select(Gen.Int[DatalineAddress.MaxAddressValue + 1, 255], Gen.Bool).Sample(t =>
            {
                (int value, bool isOutput) = t;
                string token = "_0x" + value.ToString("x", CultureInfo.InvariantCulture);

                return DatalineAddress.TryParse(token, isOutput, out DatalineAddress address)
                       && DatalineAddress.ToVendorLabel(token, isOutput) == LabelFromParse(token, isOutput)
                       && DatalineAddress.ToVendorLabel(token, isOutput) != "?"
                       && !DatalineAddress.TryEncode(address.DataLine, address.Terminal, isOutput, out _);
            }, iter: 1000);
        }
    }
}
