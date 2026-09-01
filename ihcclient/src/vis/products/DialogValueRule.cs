using System;
using System.Linq;

namespace Ihc.Vis.Products
{
    /// <summary>
    /// A constraint on what a dialog field may hold, together with the Danish sentence shown when the value
    /// breaks it.
    /// <para>This is the <b>one rule record</b> of the dialog-metadata vocabulary. Its members are a closed,
    /// deliberately small set: a length range, a whitespace ban and a leading-country-code requirement. Each
    /// exists because a story clause needs it today (US-013's telephone numbers need all three); anything
    /// added later must say which catalog product needs it and why composition cannot express it.</para>
    /// <para>Rules are authored ONCE, as shared static values, and referenced by the presets. Two
    /// separately-constructed rules that happen to look alike are the mechanism by which a dialog and its
    /// write-back drift into disagreeing about what a valid value is.</para>
    /// </summary>
    public sealed record DialogValueRule
    {
        /// <summary>Fewest characters a NON-EMPTY value may have; null for no lower bound.</summary>
        public int? MinLength { get; init; }

        /// <summary>Most characters a value may have; null for no upper bound.</summary>
        public int? MaxLength { get; init; }

        /// <summary>When false, a value containing any whitespace is refused.</summary>
        public bool WhitespaceAllowed { get; init; } = true;

        /// <summary>
        /// When true, a non-empty value must begin with a country code: a <c>+</c> followed immediately by at
        /// least one digit. The <c>00</c> international prefix does NOT satisfy it — US-013 names the
        /// <c>+45</c> form specifically, and the two are not interchangeable to the modem.
        /// </summary>
        public bool CountryCodeRequired { get; init; }

        /// <summary>The Danish sentence shown when the value breaks this rule (FR-2.6).</summary>
        public required string Refusal { get; init; }

        /// <summary>
        /// Whether <paramref name="value"/> satisfies every constraint.
        /// <para>An EMPTY value always satisfies the rule. A rule says what a value must look like, not that
        /// one is required: the modem offers 30 telephone slots of which a few are typically filled, and
        /// refusing blank would make the dialog uncommittable the moment it opened. A field that must be
        /// filled is a different concern and has no consumer yet.</para>
        /// </summary>
        public bool IsSatisfiedBy(string? value)
        {
            if (string.IsNullOrEmpty(value)) return true;

            if (MinLength is { } min && value.Length < min) return false;
            if (MaxLength is { } max && value.Length > max) return false;
            if (!WhitespaceAllowed && value.Any(char.IsWhiteSpace)) return false;
            if (CountryCodeRequired && !StartsWithCountryCode(value)) return false;

            return true;
        }

        private static bool StartsWithCountryCode(string value) =>
            value.Length >= 2 && value[0] == '+' && char.IsAsciiDigit(value[1]);

        /// <summary>
        /// US-013's telephone-number rule: 3–20 characters, no spaces, leading country code.
        /// <para><b>Only the 3-character minimum is original-application behaviour.</b> Measured against LK
        /// IHC Visual 2026-08-12: it refuses a 2-character number with
        /// <c>"Ugyldigt telefonnummer på talværdi 1 / skal være mere end 3 cifre"</c> but accepts a
        /// 3-character one — and it accepts a 60-digit number, accepts a number with no country code, and
        /// silently STRIPS spaces at input rather than refusing them. The 20-character maximum, the
        /// whitespace ban and the country-code requirement are therefore deliberate OpenVisual
        /// strictnesses, registered in <c>docs/product.md</c>.</para>
        /// </summary>
        public static DialogValueRule PhoneNumber { get; } = new()
        {
            MinLength = 3,
            MaxLength = 20,
            WhitespaceAllowed = false,
            CountryCodeRequired = true,
            Refusal = "Telefonnummeret skal være på 3-20 tegn uden mellemrum og begynde med en landekode, "
                    + "f.eks. +45.",
        };
    }
}
