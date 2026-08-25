using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Ihc.Tests.Shared;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The scaffolding every findings-export fixture needs: the pinned clock, a stamped project, and the four
    /// readers that take the writer's bytes apart again.
    ///
    /// <para><b>Why a probe rather than a helper per fixture.</b> Six fixtures assert on one format, so six
    /// copies of "which attributes does this line carry" were six parsers for one grammar — and the two copies
    /// of <see cref="AttributeNames"/> were character-for-character identical. A change to the emitted line
    /// shape then means finding every copy, and a bug in one is invisible to the others. It sits beside
    /// <c>ReportProbe</c>, which is this folder's existing answer to the same problem for the report suites.</para>
    ///
    /// <para><b>The readers work on TEXT, deliberately.</b> These files are committed oracles compared byte for
    /// byte, so an assertion routed through an XML parser would pass on a BOM, LF line ends, a different
    /// attribute order or a different escape of one character — every one a real difference to what consumes
    /// them.</para>
    /// </summary>
    internal static class FindingExportProbe
    {
        /// <summary>
        /// The clock every export fixture stamps its output with — <see cref="ReportOracleHarness.PinnedInstant"/>,
        /// not a second literal. Two spellings of one pinned date is two dates to remember, and the report
        /// oracles already pin this one.
        /// </summary>
        internal static DateTimeOffset Instant => ReportOracleHarness.PinnedInstant;

        /// <summary>A project whose root carries a save stamp, which is all the writer reads from it.</summary>
        internal static Project Stamped(string id2 = "_0x2") =>
            new(new ProjectElement(
                "utcs_project",
                null,
                ImmutableArray.Create((Name: "id2", Value: id2)),
                EquatableArray<ProjectElement>.Empty));

        /// <summary>The document's bytes as text, decoded through the encoding it declares.</summary>
        internal static string Text(byte[] bytes) => ProjectFile.Encoding.GetString(bytes);

        /// <summary>The <c>&lt;finding&gt;</c> lines, in emitted order.</summary>
        internal static string[] FindingLines(byte[] bytes) => FindingLines(Text(bytes));

        /// <summary>The same, for a caller that already decoded the document.</summary>
        internal static string[] FindingLines(string text) =>
            [.. text.Split("\r\n").Where(line => line.Contains("<finding "))];

        /// <summary>One attribute's value off an emitted line.</summary>
        internal static string Value(string line, string attribute) =>
            line.Split($" {attribute}=\"")[1].Split('"')[0];

        /// <summary>
        /// The attribute NAMES of one emitted element line, in emitted order — read off the text so the
        /// assertion is about what was written rather than about what the writer says it writes.
        /// </summary>
        internal static ImmutableArray<string> AttributeNames(string elementLine)
        {
            var names = ImmutableArray.CreateBuilder<string>();
            var name = new StringBuilder();
            bool inValue = false;
            foreach (char c in elementLine)
            {
                if (inValue)
                {
                    inValue = c != '"';
                }
                else if (c == '=')
                {
                    names.Add(name.ToString().Trim());
                    name.Clear();
                }
                else if (c == '"')
                {
                    inValue = true;
                }
                else if (c is ' ' or '<' or '>' or '/')
                {
                    name.Clear();
                }
                else
                {
                    name.Append(c);
                }
            }

            return names.ToImmutable();
        }
    }
}
