#nullable enable
using System;
using System.Globalization;

using Ihc.Vis.Model;

namespace Ihc.Vis.Addressing
{
    /// <summary>
    /// The canonical data-line terminal address (US-012): the single SDK owner of the <c>address_dataline</c>
    /// <c>_0x</c> token encoding/decoding shared by the project editor, the validator, the reports and any GUI.
    /// The vendor packs a terminal's location as the 1-based value <c>(dataLine-1)·perLine + terminal</c>, legal
    /// in <c>1..<see cref="MaxAddressValue"/></c> per direction (the validator's cap, spec ch. 04), where a data
    /// line carries <see cref="InputTerminalsPerLine"/> input terminals or <see cref="OutputTerminalsPerLine"/>
    /// output terminals. Because the legal value never exceeds two hex digits, the full-value parse
    /// (<see cref="TryParse"/>) and the report's first-two-hex-digit read (<see cref="ToVendorLabel"/>) agree.
    /// </summary>
    public readonly record struct DatalineAddress(int DataLine, int Terminal)
    {
        /// <summary>The inclusive upper bound of a packed address value per direction — the single source of truth
        /// (equal to the validator's 1–128 module cap).</summary>
        public const int MaxAddressValue = 128;

        /// <summary>The number of terminals an input data line carries.</summary>
        public const int InputTerminalsPerLine = 16;

        /// <summary>The number of terminals an output data line carries.</summary>
        public const int OutputTerminalsPerLine = 8;

        /// <summary>The terminals-per-data-line for a direction (16 input / 8 output).</summary>
        public static int TerminalsPerLine(bool isOutput) => isOutput ? OutputTerminalsPerLine : InputTerminalsPerLine;

        /// <summary>The highest data line number a direction can address (8 input / 16 output) — the packed-value
        /// cap divided by the direction's terminals-per-line.</summary>
        public static int MaxDataLine(bool isOutput) => MaxAddressValue / TerminalsPerLine(isOutput);

        /// <summary>
        /// Encodes a (data line, terminal) pair to its <c>address_dataline</c> token. Returns <c>false</c> — with
        /// <paramref name="token"/> set to the unassigned <see cref="ElementId.NullToken"/> — when either coordinate
        /// is below 1, the terminal exceeds the direction's terminals-per-line, or the packed value falls outside
        /// <c>1..<see cref="MaxAddressValue"/></c>. Failure is signalled by the return value; the null token is
        /// never emitted to <i>mean</i> out-of-range.
        /// </summary>
        public static bool TryEncode(int dataLine, int terminal, bool isOutput, out string token)
        {
            token = ElementId.NullToken;
            int perLine = TerminalsPerLine(isOutput);
            if (dataLine < 1 || terminal < 1 || terminal > perLine)
                return false;
            int value = (dataLine - 1) * perLine + terminal;
            if (value < 1 || value > MaxAddressValue)
                return false;
            token = "_0x" + value.ToString("x", CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary>Decodes an <c>address_dataline</c> token into its (data line, terminal). <c>False</c> (with a
        /// default address) for an unassigned/blank/unparseable token — the editor renders those as "unaddressed".</summary>
        public static bool TryParse(string? token, bool isOutput, out DatalineAddress address)
        {
            address = default;
            if (token is null || !token.StartsWith("_0x", StringComparison.Ordinal))
                return false;
            if (!int.TryParse(token.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value) || value <= 0)
                return false;
            int perLine = TerminalsPerLine(isOutput);
            address = new DatalineAddress((value - 1) / perLine + 1, (value - 1) % perLine + 1);
            return true;
        }

        /// <summary>
        /// The report's <c>"dataline.bit"</c> display for an <c>address_dataline</c> token ("?" when unassigned or
        /// zero), replicating the vendor <c>get_address</c>: it reads only the first two hex digits and shows the
        /// bit as <c>0n</c> for bit ≤ 7, else <c>bit+3</c>.
        /// </summary>
        public static string ToVendorLabel(string? token, bool isOutput)
        {
            int divider = TerminalsPerLine(isOutput);
            string hex = token is not null && token.StartsWith("_0x", StringComparison.Ordinal)
                ? token.Substring(3)
                : string.Empty;
            int value;
            if (hex.Length == 0)
            {
                value = 0;
            }
            else if (hex.Length < 2)
            {
                int d = HexDigit(hex[0]);
                if (d < 0) { return Unknown; }
                value = d;
            }
            else
            {
                int d0 = HexDigit(hex[0]);
                int d1 = HexDigit(hex[1]);
                if (d0 < 0 || d1 < 0) { return Unknown; }
                value = d0 * 16 + d1;
            }
            if (value <= 0)
            {
                return Unknown;
            }
            int dataline = (value - 1) / divider + 1;
            int bit = (value - 1) % divider;
            string low = bit > 7
                ? (bit + 3).ToString(CultureInfo.InvariantCulture)
                : "0" + (bit + 1).ToString(CultureInfo.InvariantCulture);
            return dataline.ToString(CultureInfo.InvariantCulture) + "." + low;
        }

        private const string Unknown = "?";

        private static int HexDigit(char c) => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            _ => -1,
        };
    }
}
