using System;

namespace ihc_openvisual.Services;

/// <summary>Classifies product element tags for routing (US-013 modem handling).</summary>
public static class ProductKinds
{
    /// <summary>Whether a device-root tag is a modem product (e.g. <c>product_rs485_sms_modem</c>) — the family the
    /// at-most-one-modem rule and the modem properties dialog apply to.</summary>
    public static bool IsModem(string tag) =>
        tag.StartsWith("product_", StringComparison.Ordinal) && tag.Contains("modem", StringComparison.Ordinal);

    /// <summary>Whether a tag is any product device root.</summary>
    public static bool IsProduct(string tag) => tag.StartsWith("product_", StringComparison.Ordinal);

    /// <summary>Whether a device-root tag is an IHC Wireless (airlink) product — the family with no cable/terminal
    /// addressing and an unlinked marker until commissioned (US-014).</summary>
    public static bool IsWireless(string tag) =>
        tag.StartsWith("product_", StringComparison.Ordinal) && tag.Contains("airlink", StringComparison.Ordinal);

    /// <summary>Whether a wireless product is not yet linked to the controller — its <c>serialnumber</c> is blank or
    /// the null token. (Linking is a controller operation, out of scope here, so inserted wireless products stay
    /// unlinked and show the yellow "!" marker.)</summary>
    public static bool IsUnlinkedWireless(string tag, string? serialNumber)
    {
        if (!IsWireless(tag))
            return false;
        string s = (serialNumber ?? string.Empty).Trim();
        return s.Length == 0 || s is "_0x0" or "0";
    }
}
