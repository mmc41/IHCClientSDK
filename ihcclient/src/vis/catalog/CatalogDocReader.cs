#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;

using Ihc.Vis.Model;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Parses a synthetic English help document (<c>syn_en*.md</c>) — the de-branded companion the IHC Visual install
    /// ships next to a catalog definition file (a <c>FunctionBlocks\*.ifb</c> block or a <c>Products\*.def</c> product)
    /// — into a <see cref="HelpDocument"/> — the name-keyed intermediate a definition's position-keyed
    /// <see cref="DefinitionDocumentation"/> is resolved from, since a bullet can only name its resource.
    /// The document shape is a fixed, trivially parseable convention: a level-1 heading (<c># display name</c>, kept
    /// for reference but not part of the summary), then a leading prose paragraph (the definition
    /// <see cref="HelpDocument.Summary"/>), then <c>**Inputs**</c>/<c>**Outputs**</c>/… sections whose
    /// bullets read <c>- **resource name** — help text</c> and map to the per-resource text keyed by resource display
    /// name (<see cref="HelpDocument.ByName"/>).
    /// </summary>
    /// <remarks>
    /// The vendor also ships a copyrighted <c>{base}.md</c>; this reader uses <b>only</b> the synthetic
    /// <c>syn_en{base}.md</c> for the embedded catalog (see <see cref="ForDefinitionFile"/>'s <c>synEnOnly</c> gate).
    /// The produced documentation rides on the in-memory definition for a GUI to surface and is never serialized into
    /// a project <c>.vis</c>, an <c>.ifb</c> or a <c>.def</c> — it has zero byte-fidelity impact and is verified by a
    /// separate equality oracle, not the body self-verify. Nothing here is definition-kind-specific: the document
    /// shape and the sibling naming convention are the same for a block and a product, so one reader serves both.
    /// </remarks>
    public static class CatalogDocReader
    {
        // The characters a bullet uses to separate the bold resource name from its help text, tried longest/most
        // specific first: em dash and en dash (the syn_en convention), then a spaced ASCII hyphen (a lenient fallback).
        private static readonly string[] Separators = { " — ", " – ", " - " };

        /// <summary>Parses help-document <paramref name="markdown"/> into a <see cref="HelpDocument"/>:
        /// the leading paragraph becomes the definition summary, and each <c>- **name** — text</c> bullet under any
        /// <c>**section**</c> heading becomes a per-resource entry keyed by <c>name</c>. Missing sections are tolerated
        /// (an empty document yields <see cref="HelpDocument.Empty"/>).</summary>
        public static HelpDocument Parse(string markdown)
        {
            ArgumentNullException.ThrowIfNull(markdown);
            string[] lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            var summary = new StringBuilder();
            var resources = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            bool inResourceSection = false;

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (IsHeading(line))
                {
                    continue;   // the "# display name" H1 is reference metadata, not summary text
                }
                if (IsSectionHeader(line))
                {
                    inResourceSection = true;
                    continue;
                }
                if (inResourceSection)
                {
                    if (TryParseResourceBullet(line, out string name, out string text))
                    {
                        resources[name] = text;
                    }
                    continue;
                }
                // Prose before the first section header is the definition summary (paragraphs joined by a single space).
                if (summary.Length > 0)
                {
                    summary.Append(' ');
                }
                summary.Append(line);
            }

            string? summaryText = summary.Length > 0 ? summary.ToString() : null;
            return summaryText is null && resources.Count == 0
                ? HelpDocument.Empty
                : new HelpDocument(summaryText, resources.ToImmutable());
        }

        /// <summary>Probes for the help document sibling of the catalog definition file at
        /// <paramref name="catalogFilePath"/> (a <c>.ifb</c> block or a <c>.def</c> product) and parses it: the
        /// synthetic <c>syn_en{base}.md</c> is tried first, then — unless <paramref name="synEnOnly"/> — the vendor
        /// <c>{base}.md</c> (used only for a caller's own components, never the copyrighted install catalog).
        /// Returns <c>null</c> when no sibling exists.</summary>
        public static HelpDocument? ForDefinitionFile(string catalogFilePath, bool synEnOnly = false)
        {
            ArgumentNullException.ThrowIfNull(catalogFilePath);
            foreach (string candidate in SiblingCandidates(catalogFilePath, synEnOnly))
            {
                if (File.Exists(candidate))
                {
                    return Parse(ReadAllText(candidate));
                }
            }
            return null;
        }

        // The help-document sibling paths for a definition file, in probe order: the synthetic "syn_en{base}.md"
        // (prefix naming, e.g. FunctionBlocks\...\1.1.01.ifb -> syn_en1.1.01.md, Products\...\product2101.def ->
        // syn_enproduct2101.md), then the vendor "{base}.md" fallback.
        private static IEnumerable<string> SiblingCandidates(string catalogFilePath, bool synEnOnly)
        {
            string directory = Path.GetDirectoryName(catalogFilePath) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(catalogFilePath);
            yield return Path.Combine(directory, "syn_en" + baseName + ".md");
            if (!synEnOnly)
            {
                yield return Path.Combine(directory, baseName + ".md");
            }
        }

        private static bool IsHeading(string line) => line.StartsWith("#", StringComparison.Ordinal);

        // A section header is a stand-alone bold run: "**Inputs**", "**Outputs**", "**Settings**", ... (an optional
        // trailing colon tolerated). It only marks that the following bullets are resource entries; its text is unused.
        private static bool IsSectionHeader(string line)
        {
            string trimmed = line.EndsWith(":", StringComparison.Ordinal) ? line[..^1].TrimEnd() : line;
            return trimmed.Length >= 4
                && trimmed.StartsWith("**", StringComparison.Ordinal)
                && trimmed.EndsWith("**", StringComparison.Ordinal)
                && !trimmed.StartsWith("-", StringComparison.Ordinal);
        }

        // Parses "- **resource name** — help text" into (name, text). The bullet marker (- or *) and a bold name are
        // required; the separator and text are optional (a name-only bullet yields empty text). Returns false for a
        // line that is not a resource bullet (so section prose is silently skipped).
        private static bool TryParseResourceBullet(string line, out string name, out string text)
        {
            name = string.Empty;
            text = string.Empty;
            if (line.Length < 2 || (line[0] != '-' && line[0] != '*'))
            {
                return false;
            }
            string body = line[1..].TrimStart();
            if (!body.StartsWith("**", StringComparison.Ordinal))
            {
                return false;
            }
            int close = body.IndexOf("**", 2, StringComparison.Ordinal);
            if (close < 0)
            {
                return false;
            }
            name = body[2..close].Trim();
            if (name.Length == 0)
            {
                return false;
            }
            string rest = body[(close + 2)..];
            foreach (string separator in Separators)
            {
                int at = rest.IndexOf(separator, StringComparison.Ordinal);
                if (at >= 0)
                {
                    text = rest[(at + separator.Length)..].Trim();
                    return true;
                }
            }
            text = rest.Trim();   // no separator: the whole remainder (possibly empty) is the text
            return true;
        }

        // syn_en documents are UTF-8 in the repo corpus; read as UTF-8 (BOM-tolerant) so Danish letters decode cleanly.
        private static string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);
    }
}
