using System;
using System.Globalization;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// A CONTROLLER FIRMWARE VERSION, as a comparable value — the target a firmware-bounded row is narrowed
    /// against.
    /// <para>
    /// A value rather than a string because it is COMPARED: a row declares the release that fixed its defect, and
    /// the question asked of a target is "is this at or past that release", which string equality cannot answer and
    /// ordinal comparison answers wrongly (<c>"3.3.9"</c> sorts above <c>"3.3.21"</c>).
    /// </para>
    /// <para>
    /// Deliberately NOT <see cref="Version"/>: that type treats an absent component as <c>-1</c> and orders
    /// <c>3.3</c> below <c>3.3.0</c>, which would make a two-part vendor citation compare below the three-part
    /// reading of the same release. Here an absent component is zero, so <c>3.3</c> and <c>3.3.0</c> are one value.
    /// </para>
    /// </summary>
    /// <param name="Major">The first component.</param>
    /// <param name="Minor">The second component.</param>
    /// <param name="Patch">The third component; zero when the citation gives only two.</param>
    /// <param name="Build">The fourth component; zero when the citation gives only three.</param>
    public readonly record struct ControllerFirmwareVersion(int Major, int Minor, int Patch, int Build)
        : IComparable<ControllerFirmwareVersion>
    {
        /// <summary>The three-component form, which is how nearly every cited release is written.</summary>
        /// <param name="major">The first component.</param>
        /// <param name="minor">The second component.</param>
        /// <param name="patch">The third component.</param>
        public ControllerFirmwareVersion(int major, int minor, int patch)
            : this(major, minor, patch, 0)
        {
        }

        /// <summary>
        /// Reads a version the way the sources actually write one, and REFUSES anything else.
        /// <para>
        /// Lenient at the head, strict at the tail. A leading designation is skipped — <c>CTR.R.03.03.44</c> and
        /// the shipped message's <c>v3.3.21</c> both name a release, and a host reading a version off a live
        /// controller does not get to choose the form. But once the digits start, every remaining component must be
        /// a plain number: a garbled tail means the target is UNKNOWN, and guessing one would silently narrow a
        /// real finding away, which is the one failure this whole mechanism must not have.
        /// </para>
        /// <para>
        /// Two to four components. One is refused because a bare number is not a version, and reading a stray count
        /// as a target would be exactly that guess.
        /// </para>
        /// </summary>
        /// <param name="text">The version as written, or null.</param>
        /// <param name="version">The parsed version; <see langword="default"/> when this returns false.</param>
        public static bool TryParse(string? text, out ControllerFirmwareVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            int start = 0;
            while (start < text.Length && !char.IsAsciiDigit(text[start]))
            {
                start++;
            }

            if (start == text.Length)
            {
                return false;
            }

            string[] parts = text[start..].Split('.');
            if (parts.Length is < 2 or > 4)
            {
                return false;
            }

            int[] components = [0, 0, 0, 0];
            for (int i = 0; i < parts.Length; i++)
            {
                // NumberStyles.None is the strict half: no sign, no whitespace, no separators, and an empty
                // component fails — while a leading zero, which is how the vendor writes 03.03.44, still reads.
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out int component))
                {
                    return false;
                }

                components[i] = component;
            }

            version = new ControllerFirmwareVersion(components[0], components[1], components[2], components[3]);
            return true;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Through the tuple, whose own comparison is component-by-component from the left — the same order
        /// written out by hand, without the four early returns to read past to confirm it.
        /// </remarks>
        public int CompareTo(ControllerFirmwareVersion other) =>
            (Major, Minor, Patch, Build).CompareTo((other.Major, other.Minor, other.Patch, other.Build));

        /// <summary>Whether the left version precedes the right.</summary>
        /// <param name="left">The left version.</param>
        /// <param name="right">The right version.</param>
        public static bool operator <(ControllerFirmwareVersion left, ControllerFirmwareVersion right) =>
            left.CompareTo(right) < 0;

        /// <summary>Whether the left version precedes or equals the right.</summary>
        /// <param name="left">The left version.</param>
        /// <param name="right">The right version.</param>
        public static bool operator <=(ControllerFirmwareVersion left, ControllerFirmwareVersion right) =>
            left.CompareTo(right) <= 0;

        /// <summary>Whether the left version follows the right.</summary>
        /// <param name="left">The left version.</param>
        /// <param name="right">The right version.</param>
        public static bool operator >(ControllerFirmwareVersion left, ControllerFirmwareVersion right) =>
            left.CompareTo(right) > 0;

        /// <summary>Whether the left version follows or equals the right.</summary>
        /// <param name="left">The left version.</param>
        /// <param name="right">The right version.</param>
        public static bool operator >=(ControllerFirmwareVersion left, ControllerFirmwareVersion right) =>
            left.CompareTo(right) >= 0;

        /// <summary>The dotted form, with the fourth component only when it carries something.</summary>
        public override string ToString() => Build == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}")
            : string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}.{Build}");
    }
}
