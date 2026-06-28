#nullable enable
using System;
using System.Globalization;

namespace Ihc.Projects
{
    /// <summary>Shared rendering for the <c>.vis</c> <c>_0x</c> + lowercase-hex token form (leading zeros stripped).</summary>
    internal static class HexToken
    {
        public static string Format(long value) => "_0x" + value.ToString("x", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A strongly-typed `.vis` element identifier. An id encodes <c>(Counter &lt;&lt; 8) | TypeCode</c>
    /// (spec ch. 02); the low byte is a constant per element type and the high bits are a
    /// project-wide allocation counter. Rendered as a <c>_0x</c> token with leading zeros stripped.
    /// </summary>
    /// <remarks>
    /// This type exists to prevent the <c>_0x</c>-conflation bug class: only ids and IDREFs are
    /// modelled as <see cref="ElementId"/>. Every other <c>_0x</c> token (<c>typeid</c>, <c>icon</c>,
    /// <c>product_identifier</c>, <c>method</c>, ...) is an opaque string and is never modelled here.
    /// </remarks>
    public readonly record struct ElementId(int Counter, int TypeCode)
    {
        /// <summary>The packed numeric value <c>(Counter &lt;&lt; 8) | (TypeCode &amp; 0xFF)</c>.</summary>
        public long Value => ((long)Counter << 8) | (uint)(TypeCode & 0xFF);

        /// <summary>Renders the id as a <c>_0x</c> + lowercase-hex token with leading zeros stripped.</summary>
        public string ToToken() => HexToken.Format(Value);

        /// <inheritdoc/>
        public override string ToString() => ToToken();
    }

    /// <summary>
    /// A packed timestamp used by the root <c>id1</c>/<c>id2</c> attributes:
    /// <c>(Day &lt;&lt; 24) | (Hour &lt;&lt; 16) | (Minute &lt;&lt; 8) | Second</c> (spec ch. 02).
    /// <c>id1</c> is the project creation time (constant for the project's life); <c>id2</c> is the
    /// time of the current save and always agrees with the <c>modified</c> element to the minute.
    /// </summary>
    public readonly record struct PackedStamp(int Day, int Hour, int Minute, int Second)
    {
        /// <summary>The packed numeric value of the stamp.</summary>
        public long Value =>
            ((long)Day << 24) | ((long)Hour << 16) | ((long)Minute << 8) | (uint)(Second & 0xFF);

        /// <summary>Renders the stamp as a <c>_0x</c> + lowercase-hex token with leading zeros stripped.</summary>
        public string ToToken() => HexToken.Format(Value);

        /// <summary>Builds a stamp from the day/hour/minute/second components of a point in time.</summary>
        public static PackedStamp FromDateTime(DateTimeOffset moment) =>
            new(moment.Day, moment.Hour, moment.Minute, moment.Second);

        /// <inheritdoc/>
        public override string ToString() => ToToken();
    }
}
