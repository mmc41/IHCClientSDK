#nullable enable
using System;

namespace Ihc.Vis.Products
{
    /// <summary>The product family a device-root tag belongs to (US-012/US-013/US-014). The five known catalog
    /// families plus <see cref="Other"/> — an open-world product whose family is not one the SDK recognises.</summary>
    public enum ProductFamily
    {
        Dataline,
        Airlink,
        Rs485LedDimmer,
        Rs485Modem,
        Rs485SmsModem,
        Other,
    }

    /// <summary>
    /// The single SDK classifier for product device-root tags — the shared owner of the open-world predicates the
    /// OpenVisual UI routes on (modem handling US-013, wireless/airlink US-014) and the exact-match
    /// <see cref="Classify"/> the reports label admitted elements with. Predicates are substring/prefix rules
    /// (open-world: an undocumented <c>product_x_modem</c> still reads as a modem for the UI), while
    /// <see cref="Classify"/> matches the known catalog tags exactly before falling back to a stated precedence.
    /// </summary>
    public static class ProductClassifier
    {
        /// <summary>
        /// The product family of a device-root <paramref name="tag"/>: an <b>exact</b> match on the known catalog
        /// tags first, else a pattern fallback with a stated precedence — a tag containing <c>airlink</c> →
        /// <see cref="ProductFamily.Airlink"/>, else containing <c>modem</c> → <see cref="ProductFamily.Rs485Modem"/>,
        /// else <see cref="ProductFamily.Other"/>. Airlink-before-modem is the tie-break for a hypothetical tag
        /// matching both.
        /// </summary>
        public static ProductFamily Classify(string tag)
        {
            ArgumentNullException.ThrowIfNull(tag);
            return tag switch
            {
                "product_dataline" => ProductFamily.Dataline,
                "product_airlink" => ProductFamily.Airlink,
                "product_rs485_led_dimmer" => ProductFamily.Rs485LedDimmer,
                "product_rs485_modem" => ProductFamily.Rs485Modem,
                "product_rs485_sms_modem" => ProductFamily.Rs485SmsModem,
                _ when tag.Contains("airlink", StringComparison.Ordinal) => ProductFamily.Airlink,
                _ when tag.Contains("modem", StringComparison.Ordinal) => ProductFamily.Rs485Modem,
                _ => ProductFamily.Other,
            };
        }

        /// <summary>Whether a tag is any product device root (<c>product_</c> prefix).</summary>
        public static bool IsProduct(string tag) => tag.StartsWith("product_", StringComparison.Ordinal);

        /// <summary>Whether a device-root tag is a modem product (e.g. <c>product_rs485_sms_modem</c>) — the family
        /// the at-most-one-modem rule and the modem properties dialog apply to.</summary>
        public static bool IsModem(string tag) =>
            IsProduct(tag) && tag.Contains("modem", StringComparison.Ordinal);

        /// <summary>Whether a device-root tag is an IHC Wireless (airlink) product — the family with no cable/terminal
        /// addressing and an unlinked marker until commissioned (US-014).</summary>
        public static bool IsWireless(string tag) =>
            IsProduct(tag) && tag.Contains("airlink", StringComparison.Ordinal);

        /// <summary>Whether a wireless product is not yet linked to the controller — its <c>serialnumber</c> is blank
        /// or the null token. (Linking is a controller operation; an inserted wireless product stays unlinked and
        /// shows the yellow "!" marker.)</summary>
        public static bool IsUnlinkedWireless(string tag, string? serialNumber)
        {
            if (!IsWireless(tag))
                return false;
            string s = (serialNumber ?? string.Empty).Trim();
            return s.Length == 0 || s is "_0x0" or "0";
        }
    }
}
