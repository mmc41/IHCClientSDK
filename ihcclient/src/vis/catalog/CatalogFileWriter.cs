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
    /// <b>Two layouts, two fidelity relations</b> (see <see cref="CatalogLayout"/>). For the SHIPPED corpus, whitespace
    /// is not significant: those files are hand-formatted irregularly (mixed indent — even within one file — blank
    /// lines between elements, trailing spaces, a close tag indented independently of its open tag), and no
    /// reconstructor can derive that. Measured over the 173 shipped files: the best fully reconstructing writer
    /// reproduced 157 of them byte-exactly, and the residual 16 demanded per-element verbatim whitespace echoing.
    /// So they are emitted in one fixed canonical layout and compared <em>after normalizing whitespace</em>. For an
    /// EXPORT (<see cref="CatalogLayout.Export"/>, save-to-library) the opposite holds: it is compared against a file
    /// the vendor's own writer produced, whose layout is perfectly regular and therefore reproduced <em>to the
    /// byte</em> (uxparity S-22). Either way everything semantic — element/attribute structure, attribute values and their
    /// escaping, ids, and the header's declared content — is reproduced exactly.
    /// </para>
    /// <para>
    /// <b>Deliberately separate from <see cref="Ihc.Vis.Io.ProjectSerializer"/>.</b> The catalog contract —
    /// per-file DOCTYPE and inline-DTD composition, authored attribute order and presence, two-space indent,
    /// per-file encoding — differs materially from the <c>.vis</c> contract (fixed prolog, registry-driven ATTLIST
    /// order with omit-if-default, three-space indent, strict Latin-1), so the two writers share no emission path:
    /// a change to one file format cannot silently reshape the other.
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
        private const string ExportIndentUnit = "   ";

        /// <summary>Serializes a product definition to its <c>.def</c> bytes.</summary>
        public static void Write(ProductDefinition definition, Stream output)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(output);
            WriteDefinition(definition.Grammar, definition.Body, definition.SourceEncoding, output);
        }

        /// <summary>Serializes a function-block definition to its <c>.ifb</c> bytes in the given
        /// <paramref name="layout"/> — <see cref="CatalogLayout.Export"/> for a save-to-library master, which the
        /// vendor writes in a different shape from the shipped corpus (S-22).</summary>
        public static void Write(FunctionBlockDefinition definition, Stream output,
            CatalogLayout layout = CatalogLayout.Catalog)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(output);
            WriteDefinition(definition.Grammar, definition.Body, definition.SourceEncoding, output, layout,
                definition.ExplicitCloseIds);
        }

        private static void WriteDefinition(CatalogGrammar grammar, ProjectElement body, CatalogTextEncoding encoding,
            Stream output, CatalogLayout layout = CatalogLayout.Catalog,
            EquatableSet<ElementId> explicitCloseIds = default)
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
            string head = grammar.VerbatimHead ?? CatalogDtdEmitter.RenderHead(grammar, body.Tag, layout);
            var sb = new StringBuilder(head.Length + 512);
            sb.Append(head);
            AppendElement(sb, body, depth: 0, layout, explicitCloseIds);

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
                    ? ex.OffendingCodePointLabel()
                    : $"{ex.OffendingCodePointLabel()} ('{ex.CharUnknown}')";
                throw new CatalogFormatException(
                    $"The definition contains character {offender}, which the file's own {encoding} encoding cannot " +
                    "represent, and was not written. Restrict text to that encoding's repertoire.", ex);
            }
            var document = new byte[preamble.Length + text.Length];
            preamble.CopyTo(document, 0);
            text.CopyTo(document, preamble.Length);
            // The well-formedness gate: reparse the exact assembled bytes and only then touch the caller's stream —
            // a typed refusal must leave the destination untouched.
            if (CatalogWellFormedness.Check(document) is { } reason)
            {
                throw new CatalogFormatException(
                    $"The serialized definition is not well-formed XML and was not written: {reason}");
            }
            output.Write(document, 0, document.Length);
        }

        private static void AppendElement(StringBuilder sb, ProjectElement element, int depth, CatalogLayout layout,
            EquatableSet<ElementId> explicitCloseIds)
        {
            AppendIndent(sb, depth, layout);
            sb.Append('<').Append(element.Tag);
            foreach ((string name, string value) in element.Attrs)
            {
                sb.Append(' ').Append(name).Append('=').Append('"');
                XmlText.AppendEscaped(sb, value, escapeApostrophe: false);   // the catalog writer leaves ' literal (D3)
                sb.Append('"');
            }

            // An element the export EMPTIED keeps its two-tag form; one that never had children self-closes (S-22).
            bool explicitClose = element.Id is { } id && explicitCloseIds.Contains(id);
            if (element.Children.IsEmpty && !explicitClose)
            {
                // The export writer closes tight; the catalog writer leaves a space (S-22).
                sb.Append(layout == CatalogLayout.Export ? "/>" : " />").Append(Crlf);
                return;
            }
            sb.Append('>').Append(Crlf);
            foreach (ProjectElement child in element.Children)
            {
                AppendElement(sb, child, depth + 1, layout, explicitCloseIds);
            }
            AppendIndent(sb, depth, layout);
            sb.Append("</").Append(element.Tag).Append('>').Append(Crlf);
        }

        // Catalog layout: one two-space unit per level from column 0. Export layout: three-space units, with the
        // root's children starting at column 6 rather than 3 — the ladder the vendor writes is 0, 6, 9, 12, … (S-22).
        private static void AppendIndent(StringBuilder sb, int depth, CatalogLayout layout)
        {
            if (layout == CatalogLayout.Export)
            {
                for (int i = 0; depth > 0 && i <= depth; i++)
                {
                    sb.Append(ExportIndentUnit);
                }
                return;
            }
            for (int i = 0; i < depth; i++)
            {
                sb.Append(IndentUnit);
            }
        }

    }
}
