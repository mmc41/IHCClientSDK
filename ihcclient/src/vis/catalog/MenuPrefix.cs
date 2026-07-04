#nullable enable
using System.Text.RegularExpressions;

namespace Ihc.Projects
{
    /// <summary>
    /// Strips the vendor's <c>NN#</c> menu-ordering prefix that catalog display names carry (e.g. <c>12#Stikkontakt</c>
    /// → <c>Stikkontakt</c>). Shared by catalog discovery (product/block display names) and the insert transform
    /// (the inserted root's <c>name</c>), so the one grammar rule lives in a single place.
    /// </summary>
    internal static class MenuPrefix
    {
        private static readonly Regex Pattern = new(@"^\d+#", RegexOptions.Compiled);

        /// <summary>Returns <paramref name="name"/> with a leading <c>NN#</c> menu prefix removed (unchanged when there is none).</summary>
        public static string Strip(string name) => Pattern.Replace(name, string.Empty);
    }
}
