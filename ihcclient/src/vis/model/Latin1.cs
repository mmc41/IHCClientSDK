
namespace Ihc.Vis.Model
{
    /// <summary>
    /// T027: the ISO-8859-1 (Latin-1) repertoire test, defined once so the validator (which rejects out-of-repertoire
    /// text) and the serializer (which names the first offending character) can never disagree on exactly which
    /// characters the <c>.vis</c> wire can represent — a code point at or below <c>0xFF</c>.
    /// </summary>
    internal static class Latin1
    {
        /// <summary>Whether the character is in the Latin-1 repertoire (code point &lt;= <c>0xFF</c>).</summary>
        public static bool Contains(char c) => c <= 0xFF;

        /// <summary>Whether every character of <paramref name="value"/> is Latin-1.</summary>
        public static bool Contains(string value)
        {
            foreach (char c in value)
            {
                if (!Contains(c))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
