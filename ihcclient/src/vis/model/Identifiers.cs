#nullable enable
using System;
using System.Globalization;

namespace Ihc.Vis.Model
{
    /// <summary>Shared rendering/parsing for the <c>.vis</c> <c>_0x</c> + lowercase-hex token form (leading zeros stripped).</summary>
    internal static class HexToken
    {
        public static string Format(long value) => "_0x" + value.ToString("x", CultureInfo.InvariantCulture);

        /// <summary>
        /// Strictly parses a <c>_0x</c>+hex token to its raw numeric value; <c>false</c> for a null, malformed,
        /// or negative token. Unlike <see cref="ElementId.TryParse"/> this keeps the packed value whole (no
        /// counter/type-code split) — used for scalar tokens such as <c>last_unique_id</c>.
        /// </summary>
        public static bool TryParseValue(string? token, out long value)
        {
            value = 0;
            return token is not null
                && token.StartsWith("_0x", StringComparison.Ordinal)
                && long.TryParse(token.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
                && value >= 0;
        }

        /// <summary>The lenient variant of <see cref="TryParseValue"/>: <paramref name="fallback"/> on failure.</summary>
        public static long ParseValueOrDefault(string? token, long fallback = 0) =>
            TryParseValue(token, out long value) ? value : fallback;
    }

    /// <summary>Shared invariant-decimal rendering for numeric <c>.vis</c> attribute values (dates, times, indexes).</summary>
    internal static class DecToken
    {
        public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
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
        /// <summary>
        /// The largest packed value a legal id can hold: 24-bit counter + 8-bit type code (spec ch. 02).
        /// Larger hex tokens are rejected by <see cref="TryParse"/> — accepting them would truncate the
        /// counter through the <c>int</c> cast, letting two distinct on-disk tokens alias to one
        /// <see cref="ElementId"/> and misdirect every id-addressed edit and delete.
        /// </summary>
        internal const long MaxPackedValue = 0xFFFFFFFFL;

        /// <summary>
        /// The vendor null token <c>"_0x0"</c>: the sentinel an unwired IDREF carries (the insert transform
        /// stamps it on #REQUIRED-yet-unwired references; the validator's bijection and dangling-IDREF checks
        /// bless it). A legitimate authored state — never a live element id.
        /// </summary>
        public const string NullToken = "_0x0";

        /// <summary>The packed numeric value <c>(Counter &lt;&lt; 8) | (TypeCode &amp; 0xFF)</c>.</summary>
        public long Value => ((long)Counter << 8) | (uint)(TypeCode & 0xFF);

        /// <summary>Renders the id as a <c>_0x</c> + lowercase-hex token with leading zeros stripped.</summary>
        public string ToToken() => HexToken.Format(Value);

        /// <summary>
        /// Parses a <c>_0x</c> id token into its <c>(Counter, TypeCode)</c> split (<c>counter = value &gt;&gt; 8</c>,
        /// <c>typeCode = value &amp; 0xFF</c>). Returns <c>false</c> for a token that is not a well-formed
        /// <c>_0x</c>+hex value in the legal packed range (e.g. an opaque catalog token, or a token beyond
        /// <see cref="MaxPackedValue"/>); such ids are still preserved verbatim in the model.
        /// </summary>
        public static bool TryParse(string? token, out ElementId id)
        {
            id = default;
            if (token is null || !token.StartsWith("_0x", StringComparison.Ordinal))
            {
                return false;
            }
            if (!long.TryParse(token.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long value)
                || value < 0 || value > MaxPackedValue)
            {
                return false;
            }
            id = new ElementId((int)(value >> 8), (int)(value & 0xFF));
            return true;
        }

        /// <inheritdoc/>
        public override string ToString() => ToToken();
    }

    /// <summary>
    /// A packed timestamp used by the root <c>id1</c>/<c>id2</c> attributes:
    /// <c>(Day &lt;&lt; 24) | (Hour &lt;&lt; 16) | (Minute &lt;&lt; 8) | Second</c> (spec ch. 02).
    /// <c>id1</c> is the project creation time (constant for the project's life); <c>id2</c> is the
    /// time of the current save and always agrees with the <c>modified</c> element to the minute.
    /// Components are range-validated at construction — bit-or'ing an out-of-range part would mint a
    /// token IHC Visual rejects.
    /// </summary>
    public readonly record struct PackedStamp
    {
        public PackedStamp(int day, int hour, int minute, int second)
        {
            Day = day is >= 1 and <= 31 ? day
                : throw new ArgumentOutOfRangeException(nameof(day), day, "day must be 1–31");
            Hour = hour is >= 0 and <= 23 ? hour
                : throw new ArgumentOutOfRangeException(nameof(hour), hour, "hour must be 0–23");
            Minute = minute is >= 0 and <= 59 ? minute
                : throw new ArgumentOutOfRangeException(nameof(minute), minute, "minute must be 0–59");
            Second = second is >= 0 and <= 59 ? second
                : throw new ArgumentOutOfRangeException(nameof(second), second, "second must be 0–59");
        }

        public int Day { get; }

        public int Hour { get; }

        public int Minute { get; }

        public int Second { get; }

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
