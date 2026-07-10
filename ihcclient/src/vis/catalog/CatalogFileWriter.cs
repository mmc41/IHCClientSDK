#nullable enable
using System;
using System.IO;
using System.Text;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Serializes a catalog component definition back to its on-disk file form (<c>Products\*.def</c> /
    /// <c>FunctionBlocks\*.ifb</c>). Stream-based by design — callers compose with <c>File.Create</c>/<c>MemoryStream</c>;
    /// there are no file-path overloads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One generic path, no per-type logic.</b> A product and a function block are written by the <em>same</em>
    /// <see cref="WriteDefinition"/> core — the two public overloads only unpack <c>Grammar</c>/<c>Body</c>/
    /// <c>SourceEncoding</c>. The header is rendered from the structured <see cref="CatalogGrammar"/> (or re-emitted
    /// from its verbatim fallback for an exotic user file), then the body's element tree is written with attributes
    /// exactly as the model holds them (source order, logical values re-escaped), the ids the model carries, and the
    /// file's own encoding. The body path performs <b>no</b> grammar-driven reconstruction — no omit-if-default, no
    /// attribute reordering, no per-family formatting. A wrong model therefore surfaces as a difference rather than
    /// being silently compensated.
    /// </para>
    /// <para>
    /// <b>Whitespace is not significant to fidelity.</b> Vendor catalog files are hand-formatted irregularly (mixed
    /// indent, blank lines, trailing spaces — see <c>tmp/catalogfile-anatomy.md</c>), which no reconstructor can
    /// derive. So header and body are emitted in one fixed canonical layout and fidelity is asserted <em>after
    /// normalizing whitespace</em>. Everything that is significant — element/attribute structure, attribute values
    /// and their escaping, ids, and the header's declared content — is reproduced exactly.
    /// </para>
    /// <para>
    /// <b>Well-formedness is the writer's own guarantee.</b> The raw body verbs accept arbitrary tag/attribute
    /// text, so malformed XML is reachable through the public API — and the whitespace-normalized fidelity relation
    /// cannot see a missing token separator. The complete document is therefore assembled in memory and reparsed
    /// (<see cref="CatalogWellFormedness"/>) before a single byte reaches the output stream: failure throws
    /// <see cref="CatalogFormatException"/> and provably leaves the destination untouched.
    /// </para>
    /// </remarks>
    public static class CatalogFileWriter
    {
        private const string Crlf = "\r\n";
        private const string IndentUnit = "  ";

        /// <summary>Serializes a product definition to its <c>.def</c> bytes.</summary>
        public static void Write(ProductDefinition definition, Stream output)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(output);
            WriteDefinition(definition.Grammar, definition.Body, definition.SourceEncoding, output);
        }

        /// <summary>Serializes a function-block definition to its <c>.ifb</c> bytes.</summary>
        public static void Write(FunctionBlockDefinition definition, Stream output)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(output);
            WriteDefinition(definition.Grammar, definition.Body, definition.SourceEncoding, output);
        }

        private static void WriteDefinition(CatalogGrammar grammar, ProjectElement body, CatalogTextEncoding encoding,
            Stream output)
        {
            if (grammar is null || grammar.IsEmpty)
            {
                throw new InvalidOperationException(
                    "The definition has no catalog grammar (inline-DTD declarations), so it has no on-disk form. " +
                    "Read it with CatalogReader, take it from a catalog, or author its grammar before writing.");
            }
            if (grammar.VerbatimHead is null && grammar.DoctypeRoot is { } declaredRoot && declaredRoot != body.Tag)
            {
                throw new CatalogFormatException(
                    $"The grammar declares DOCTYPE root '{declaredRoot}' but the body root element is " +
                    $"'{body.Tag}' — the document would be inconsistent (corpus: the two are always equal).");
            }
            string head = grammar.VerbatimHead ?? CatalogDtdEmitter.RenderHead(grammar, body.Tag);
            var sb = new StringBuilder(head.Length + 512);
            sb.Append(head);
            AppendElement(sb, body, depth: 0);

            byte[] preamble = encoding.Preamble();
            byte[] text;
            try
            {
                text = encoding.TextEncoding().GetBytes(sb.ToString());
            }
            catch (EncoderFallbackException ex)
            {
                // D4: a character the file's own encoding cannot represent is refused, never transcoded — a
                // replacement '?' would still reparse clean, so this is the only gate that can see it.
                string offender = ex.IsUnknownSurrogate()
                    ? $"U+{char.ConvertToUtf32(ex.CharUnknownHigh, ex.CharUnknownLow):X4}"
                    : $"U+{(int)ex.CharUnknown:X4} ('{ex.CharUnknown}')";
                throw new CatalogFormatException(
                    $"The definition contains character {offender}, which the file's own {encoding} encoding cannot " +
                    "represent, and was not written. Restrict text to that encoding's repertoire.", ex);
            }
            var document = new byte[preamble.Length + text.Length];
            preamble.CopyTo(document, 0);
            text.CopyTo(document, preamble.Length);
            // The well-formedness gate (§2.3 of the plan of record): reparse the exact assembled bytes and only
            // then touch the caller's stream — a typed refusal must leave the destination untouched.
            if (CatalogWellFormedness.Check(document) is { } reason)
            {
                throw new CatalogFormatException(
                    $"The serialized definition is not well-formed XML and was not written: {reason}");
            }
            output.Write(document, 0, document.Length);
        }

        private static void AppendElement(StringBuilder sb, ProjectElement element, int depth)
        {
            AppendIndent(sb, depth);
            sb.Append('<').Append(element.Tag);
            foreach ((string name, string value) in element.AttrsOrEmpty())
            {
                sb.Append(' ').Append(name).Append('=').Append('"');
                AppendEscaped(sb, value);
                sb.Append('"');
            }

            if (element.Children.IsDefaultOrEmpty)
            {
                sb.Append(" />").Append(Crlf);
                return;
            }
            sb.Append('>').Append(Crlf);
            foreach (ProjectElement child in element.Children)
            {
                AppendElement(sb, child, depth + 1);
            }
            AppendIndent(sb, depth);
            sb.Append("</").Append(element.Tag).Append('>').Append(Crlf);
        }

        private static void AppendIndent(StringBuilder sb, int depth)
        {
            for (int i = 0; i < depth; i++)
            {
                sb.Append(IndentUnit);
            }
        }

        // The vendor catalog escaping rule (measured across the corpus, tmp/catalogfile-anatomy.md §3): the five XML
        // specials except the apostrophe — & < > " are escaped, ' is left literal — plus numeric refs for control
        // characters. One vendor file (1.2.05.ifb) escapes ' as &apos;, an inconsistency no single rule can match;
        // the fidelity relation forgives exactly that pair (CatalogTextCompare, decision D3), so this writer always
        // emits the literal apostrophe.
        private static void AppendEscaped(StringBuilder sb, string value)
        {
            foreach (char c in value)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\r': sb.Append("&#xD;"); break;
                    case '\n': sb.Append("&#xA;"); break;
                    case '\t': sb.Append("&#x9;"); break;
                    default:
                        if (c < 0x20)
                        {
                            throw new InvalidOperationException(
                                $"Attribute value contains control character U+{(int)c:X4}, which XML cannot represent.");
                        }
                        sb.Append(c);
                        break;
                }
            }
        }
    }
}
