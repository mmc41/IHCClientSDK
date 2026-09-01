using System.Text;

namespace Ihc
{
    /// <summary>Diagnostics for <see cref="EncoderFallbackException"/> thrown by a strict (throwing) encoding.</summary>
    internal static class EncoderFallbackExceptionExtensions
    {
        /// <summary>
        /// The offending code point as a <c>U+XXXX</c> label. <see cref="EncoderFallbackException.CharUnknown"/> is
        /// U+0000 when the offender is a surrogate PAIR (an astral char), so the halves are combined to name the
        /// real scalar (e.g. <c>U+1F600</c>) instead of a lone surrogate half.
        /// </summary>
        public static string OffendingCodePointLabel(this EncoderFallbackException ex) =>
            ex.IsUnknownSurrogate()
                ? $"U+{char.ConvertToUtf32(ex.CharUnknownHigh, ex.CharUnknownLow):X4}"
                : $"U+{(int)ex.CharUnknown:X4}";
    }
}
