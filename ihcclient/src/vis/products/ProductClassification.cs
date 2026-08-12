#nullable enable
using System;

namespace Ihc.Vis.Products
{
    /// <summary>The product family a device-root tag belongs to (US-012/US-013/US-014). The six known catalog
    /// families plus <see cref="Other"/> — an open-world product whose family is not one the SDK recognises.</summary>
    public enum ProductFamily
    {
        Dataline,
        Airlink,
        Rs485LedDimmer,
        Rs485Modem,
        Rs485SmsModem,

        /// <summary>The S0 metering device (<c>s0_device</c>) — a catalog product whose device root carries no
        /// <c>product_</c> prefix. It has its own measured dialog shape, so it is a family of its own rather than
        /// falling through to <see cref="Other"/> and getting the generic dialog.</summary>
        S0Device,

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
                // Open-world: `product_rs485_modem` is recognised here but has NO built-in TypeCode (see TypeCode.cs).
                "product_rs485_modem" => ProductFamily.Rs485Modem,
                "product_rs485_sms_modem" => ProductFamily.Rs485SmsModem,
                "s0_device" => ProductFamily.S0Device,
                _ when tag.Contains("airlink", StringComparison.Ordinal) => ProductFamily.Airlink,
                _ when tag.Contains("modem", StringComparison.Ordinal) => ProductFamily.Rs485Modem,
                _ => ProductFamily.Other,
            };
        }

        /// <summary>
        /// Catalog device roots that ARE products but do not carry the <c>product_</c> prefix. The prefix is a
        /// vendor naming convention, not a rule: <c>s0_device</c> is an ordinary catalog product — placed from
        /// the Insert menu, sitting under a locality, and the original opens an ordinary properties dialog for
        /// it (measured 2026-08-12: title "S0 Device", one group box, seven fields). Keyed on the prefix
        /// alone, every product predicate answered "no" for it and its Egenskaber route opened nothing at all.
        /// <para>An explicit closed set rather than a looser pattern: a pattern wide enough to catch
        /// <c>s0_device</c> would also catch element types that are not products.</para>
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> UnprefixedProductRoots =
            new(StringComparer.Ordinal) { "s0_device" };

        /// <summary>Whether a tag is any product device root: the <c>product_</c> prefix, or one of the known
        /// unprefixed catalog roots (see <see cref="UnprefixedProductRoots"/>).</summary>
        public static bool IsProduct(string tag) =>
            tag.StartsWith("product_", StringComparison.Ordinal) || UnprefixedProductRoots.Contains(tag);

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
