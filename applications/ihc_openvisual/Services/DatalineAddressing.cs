using System;
using System.Globalization;

namespace ihc_openvisual.Services;

/// <summary>
/// Encodes/decodes a data-line terminal address (US-012). The vendor stores a product terminal's location in the
/// <c>address_dataline</c> attribute as a 1-based <c>_0x</c> value: <c>value = (dataLine-1)·perLine + terminal</c>,
/// where a data line carries 16 input terminals or 8 output terminals. Matches the decode the report uses
/// (<c>dataLine = (value-1)/perLine + 1</c>). A blank/invalid address is the unaddressed default <c>_0x0</c>.
/// </summary>
public static class DatalineAddressing
{
    public const int InputTerminalsPerLine = 16;
    public const int OutputTerminalsPerLine = 8;

    public static int TerminalsPerLine(bool isOutput) => isOutput ? OutputTerminalsPerLine : InputTerminalsPerLine;

    /// <summary>The <c>address_dataline</c> token for a (data line, terminal) pair, or the unaddressed default
    /// <c>_0x0</c> when either is out of range.</summary>
    public static string Encode(int dataLine, int terminal, int perLine)
    {
        if (dataLine < 1 || terminal < 1 || terminal > perLine)
            return "_0x0";
        int value = (dataLine - 1) * perLine + terminal;
        return "_0x" + value.ToString("x", CultureInfo.InvariantCulture);
    }

    /// <summary>Decodes an <c>address_dataline</c> token into its (data line, terminal). False (with defaults) for an
    /// unaddressed/blank/unparseable token.</summary>
    public static bool TryDecode(string? token, int perLine, out int dataLine, out int terminal)
    {
        dataLine = 1;
        terminal = 0;
        if (token is null || !token.StartsWith("_0x", StringComparison.Ordinal))
            return false;
        if (!int.TryParse(token.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value) || value <= 0)
            return false;
        dataLine = (value - 1) / perLine + 1;
        terminal = (value - 1) % perLine + 1;
        return true;
    }
}
