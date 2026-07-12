#nullable enable
using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Xml;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Reads a vendor component/template file (<c>Products\*.def</c>, <c>FunctionBlocks\*.ifb</c>,
    /// <c>Data\NewDoc.idf</c>, <c>Data\EnumeratorDefinitions.def</c>) into the generic <see cref="ProjectElement"/>
    /// node model as the <b>raw file body</b>: only the attributes physically present, in source order, with their
    /// file id tokens verbatim. DTD-defaulted attributes are deliberately <em>not</em> materialized here — that keeps
    /// <c>Body</c> the byte-faithful shape <see cref="CatalogFileWriter"/> re-emits and a builder authors. The
    /// <em>effective</em> values the insert transform needs for cross-DTD materialization (spec ch. 09 §9.3.7) are
    /// re-derived on demand from the definition's own DTD at insert time (see
    /// <see cref="Ihc.Vis.Editing.ProjectEditor"/>.InsertComponent → <see cref="CatalogDefaults"/>). The file's header
    /// (prolog + DOCTYPE + inline DTD) is lenient-parsed into the definition's structured
    /// <see cref="Ihc.Vis.Model.CatalogGrammar"/>, and its text encoding is captured, for faithful re-emission.
    /// </summary>
    /// <remarks>
    /// Parsing is non-validating and forgiving (spec ch. 09 §9.3.8): catalog files contain duplicate ids,
    /// undeclared/ misspelled attributes and copy-pasted DTDs, all of which a validating parser would reject.
    /// Encoding: .NET's <see cref="XmlReader"/> trusts the declared <c>ISO-8859-1</c> over a UTF-8 BOM, which
    /// mojibakes the <c>Products\*.def</c> files — they are UTF-8-with-BOM despite declaring ISO-8859-1 (the spec's
    /// documented trap, ch. 09 §9.3.2). So this reader decodes the bytes itself via a BOM-detecting
    /// <see cref="StreamReader"/> (Latin-1 fallback for the genuine ISO-8859-1 <c>.ifb</c>/<c>.idf</c> files) and
    /// hands <c>XmlReader</c> a <c>TextReader</c>. Attribute values are returned unescaped (logical), in the order
    /// the reader surfaces them.
    /// </remarks>
    public static class CatalogReader
    {
        /// <summary>
        /// Reads a product catalog file (<c>Products\*.def</c>) into a <see cref="ProductDefinition"/> — the public
        /// file→instance entry point for importing a single product without an IHC Visual install scan. Applies the
        /// same encoding sniffing, inline-DTD ATTLIST defaulting, and structured-grammar capture that
        /// <see cref="CatalogDiscovery"/> uses per file, so an imported product resolves and inserts identically to a
        /// scanned one. <see cref="ProductDefinition.CategoryPath"/> is empty (a standalone file has no catalog-tree
        /// category); pass <paramref name="documentation"/> to attach programmatic help metadata (the D3 doc-probe hook).
        /// </summary>
        public static ProductDefinition ReadProduct(string path, ProductDocumentation? documentation = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            return ParseCatalogFile(path, () => BuildProduct(File.ReadAllBytes(path), string.Empty, documentation));
        }

        /// <summary>Reads a product catalog file from a stream into a <see cref="ProductDefinition"/>; see
        /// <see cref="ReadProduct(string, ProductDocumentation?)"/>.</summary>
        public static ProductDefinition ReadProduct(Stream stream, ProductDocumentation? documentation = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return BuildProduct(XmlProlog.ReadAllBytes(stream), string.Empty, documentation);
        }

        /// <summary>
        /// Reads a function-block catalog file (<c>FunctionBlocks\*.ifb</c>) into a <see cref="FunctionBlockDefinition"/>
        /// — the public file→instance entry point for importing a single block. Same encoding/DTD-default/grammar
        /// handling as <see cref="CatalogDiscovery"/>; <see cref="FunctionBlockDefinition.CategoryPath"/> is empty.
        /// Pass <paramref name="documentation"/> to attach programmatic help metadata (the D3 doc-probe hook).
        /// </summary>
        public static FunctionBlockDefinition ReadFunctionBlock(string path, FunctionBlockDocumentation? documentation = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            return ParseCatalogFile(path, () => BuildFunctionBlock(File.ReadAllBytes(path), string.Empty, documentation));
        }

        /// <summary>
        /// Runs a catalog-file parse with file-context error wrapping: one malformed/truncated vendor file surfaces
        /// an <see cref="InvalidDataException"/> naming the offending path, instead of a bare
        /// <see cref="XmlException"/>/<see cref="IOException"/> that names neither the file nor that a catalog read
        /// was in progress. This reader owns the path, so the wrap lives here — shared by the path-taking readers
        /// above and by <see cref="CatalogDiscovery"/>'s install-dir scan.
        /// </summary>
        internal static T ParseCatalogFile<T>(string path, Func<T> parse)
        {
            try
            {
                return parse();
            }
            catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
            {
                throw new InvalidDataException($"Failed to parse IHC Visual catalog file '{path}': {ex.Message}", ex);
            }
        }

        /// <summary>Reads a function-block catalog file from a stream into a <see cref="FunctionBlockDefinition"/>; see
        /// <see cref="ReadFunctionBlock(string, FunctionBlockDocumentation?)"/>.</summary>
        public static FunctionBlockDefinition ReadFunctionBlock(Stream stream, FunctionBlockDocumentation? documentation = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return BuildFunctionBlock(XmlProlog.ReadAllBytes(stream), string.Empty, documentation);
        }

        // The single product/function-block construction path, shared by the public single-file readers above and by
        // CatalogDiscovery's install-dir scan (which supplies the tree-relative CategoryPath). Keeping identifier/
        // display-name extraction + InlineDtd capture here means an imported instance is byte-for-byte the same shape
        // as a scanned one.
        internal static ProductDefinition BuildProduct(byte[] bytes, string categoryPath, ProductDocumentation? documentation)
        {
            ProjectElement body = Read(bytes);
            string identifier = body.GetAttribute("product_identifier") ?? string.Empty;
            string displayName = MenuPrefix.Strip(body.GetAttribute("name") ?? string.Empty);
            var definition = new ProductDefinition(identifier, displayName, categoryPath, body)
            {
                Grammar = CatalogDtdParser.ParseLenient(CatalogDtdParser.CaptureHeadText(bytes)),
                SourceEncoding = CatalogTextEncodingExtensions.Classify(bytes),
            };
            return documentation is null ? definition : definition with { Documentation = documentation };
        }

        internal static FunctionBlockDefinition BuildFunctionBlock(byte[] bytes, string categoryPath, FunctionBlockDocumentation? documentation)
        {
            ProjectElement body = Read(bytes);
            string masterType = body.GetAttribute("master_type") ?? string.Empty;
            string masterVersion = body.GetAttribute("master_version") ?? string.Empty;
            string masterName = body.GetAttribute("master_name") ?? string.Empty;
            string displayName = body.GetAttribute("name") ?? masterName;
            var definition = new FunctionBlockDefinition(masterType, masterVersion, masterName, displayName, categoryPath, body)
            {
                Grammar = CatalogDtdParser.ParseLenient(CatalogDtdParser.CaptureHeadText(bytes)),
                SourceEncoding = CatalogTextEncodingExtensions.Classify(bytes),
            };
            return documentation is null ? definition : definition with { Documentation = documentation };
        }

        internal static ProjectElement ReadFile(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Read(stream);
        }

        internal static ProjectElement Read(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return Read(XmlProlog.ReadAllBytes(stream));
        }

        internal static ProjectElement Read(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Parse,   // process the inline DTD so ATTLIST defaults are materialized
                ValidationType = ValidationType.None,  // non-validating: tolerate duplicate ids / undeclared attrs
                XmlResolver = null,                    // never fetch an external DTD
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true,
                CloseInput = false,
            };
            // Decode the bytes ourselves: .NET's XmlReader trusts the declared ISO-8859-1 over a UTF-8 BOM, which
            // mojibakes the Products\*.def files. A BOM wins (the spec's documented trap, ch. 09 §9.3.2); with no
            // BOM a declared non-Latin-1 encoding is honored (a UTF-8-without-BOM file would otherwise silently
            // mojibake its names into every project it is inserted into); otherwise Latin-1 (.ifb/.idf).
            using var textReader = new StreamReader(new MemoryStream(bytes, writable: false), SniffEncoding(bytes),
                                                    detectEncodingFromByteOrderMarks: true);
            using XmlReader reader = XmlReader.Create(textReader, settings);
            return ReadElement(reader);
        }

        // Internal (not private): CatalogDtdParser.CaptureHeadText decodes file bytes with the identical rule, so
        // the header text the grammar parser sees and the body text the XML reader sees can never diverge.
        internal static Encoding SniffEncoding(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8;   // redundant with the StreamReader's own BOM detection, but explicit
            }
            if (XmlProlog.TryGetDeclaredEncoding(XmlProlog.Head(bytes)) is { } declared
                && !declared.Equals("ISO-8859-1", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return Encoding.GetEncoding(declared);
                }
                catch (ArgumentException)
                {
                    // unknown name → fall through to Latin-1 (total: every byte decodes)
                }
            }
            return Encoding.Latin1;
        }

        private static ProjectElement ReadElement(XmlReader reader)
        {
            reader.MoveToContent();
            string tag = reader.LocalName;
            ImmutableArray<(string, string)> attrs = ReadAttributes(reader);
            string? idToken = ProjectElement.GetAttribute(attrs, "id");
            ElementId? id = idToken is not null && ElementId.TryParse(idToken, out ElementId parsed) ? parsed : null;

            if (reader.IsEmptyElement)
            {
                reader.Read();
                return new ProjectElement(tag, id, attrs, ImmutableArray<ProjectElement>.Empty);
            }

            var children = ImmutableArray.CreateBuilder<ProjectElement>();
            reader.Read();
            while (true)
            {
                reader.MoveToContent();
                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    reader.Read();
                    break;
                }
                if (reader.NodeType == XmlNodeType.Element)
                {
                    children.Add(ReadElement(reader));
                }
                else if (reader.NodeType == XmlNodeType.None)
                {
                    break;
                }
                else
                {
                    reader.Read();
                }
            }
            return new ProjectElement(tag, id, attrs, children.ToImmutable());
        }

        private static ImmutableArray<(string, string)> ReadAttributes(XmlReader reader)
        {
            if (!reader.HasAttributes)
            {
                return ImmutableArray<(string, string)>.Empty;
            }
            var attrs = ImmutableArray.CreateBuilder<(string, string)>(reader.AttributeCount);
            for (int i = 0; i < reader.AttributeCount; i++)
            {
                reader.MoveToAttribute(i);
                // Capture only the attributes physically present in the file, in source order (verified: XmlReader's
                // present-attribute order == source order across the whole corpus). DTD-defaulted attributes are
                // skipped so Body is the RAW file body — the byte-faithful shape CatalogFileWriter re-emits and the
                // shape a builder authors. The insert transform re-materializes catalog defaults on demand from the
                // definition's own DTD (see ProjectEditor.InsertComponent); DefinitionNormalizer canonicalizes against
                // the source grammar, so dropping defaults here is symmetric there.
                if (reader.IsDefault)
                {
                    continue;
                }
                attrs.Add((reader.LocalName, reader.Value));
            }
            reader.MoveToElement();
            return attrs.ToImmutable();
        }
    }
}
