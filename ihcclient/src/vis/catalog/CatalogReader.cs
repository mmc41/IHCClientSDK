#nullable enable
using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Xml;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Reads a vendor component/template file (<c>Products\*.def</c>, <c>FunctionBlocks\*.ifb</c>,
    /// <c>Data\NewDoc.idf</c>, <c>Data\EnumeratorDefinitions.def</c>) into the generic <see cref="ProjectElement"/>
    /// node model, <b>applying the file's own internal-DTD ATTLIST defaults</b>. This is the crucial difference
    /// from <see cref="Ihc.Vis.Io.ProjectReader"/> (which ignores the DTD): catalog instances routinely omit attributes such
    /// as <c>locked="yes"</c>/<c>backup="yes"</c> and rely on the file's DTD default, and the insert transform
    /// needs those <em>effective</em> values to decide cross-DTD materialization (spec ch. 09 §9.3.7).
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
        /// same encoding sniffing, inline-DTD ATTLIST defaulting, and open-world <c>InlineDtdBlocks</c> capture that
        /// <see cref="CatalogDiscovery"/> uses per file, so an imported product resolves and inserts identically to a
        /// scanned one. <see cref="ProductDefinition.CategoryPath"/> is empty (a standalone file has no catalog-tree
        /// category); pass <paramref name="documentation"/> to attach programmatic help metadata (the D3 doc-probe hook).
        /// </summary>
        public static ProductDefinition ReadProduct(string path, ProductDocumentation? documentation = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            return BuildProduct(File.ReadAllBytes(path), string.Empty, documentation);
        }

        /// <summary>Reads a product catalog file from a stream into a <see cref="ProductDefinition"/>; see
        /// <see cref="ReadProduct(string, ProductDocumentation?)"/>.</summary>
        public static ProductDefinition ReadProduct(Stream stream, ProductDocumentation? documentation = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return BuildProduct(ReadAllBytes(stream), string.Empty, documentation);
        }

        /// <summary>
        /// Reads a function-block catalog file (<c>FunctionBlocks\*.ifb</c>) into a <see cref="FunctionBlockDefinition"/>
        /// — the public file→instance entry point for importing a single block. Same encoding/DTD-default/InlineDtd
        /// handling as <see cref="CatalogDiscovery"/>; <see cref="FunctionBlockDefinition.CategoryPath"/> is empty.
        /// Pass <paramref name="documentation"/> to attach programmatic help metadata (the D3 doc-probe hook).
        /// </summary>
        public static FunctionBlockDefinition ReadFunctionBlock(string path, FunctionBlockDocumentation? documentation = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            return BuildFunctionBlock(File.ReadAllBytes(path), string.Empty, documentation);
        }

        /// <summary>Reads a function-block catalog file from a stream into a <see cref="FunctionBlockDefinition"/>; see
        /// <see cref="ReadFunctionBlock(string, FunctionBlockDocumentation?)"/>.</summary>
        public static FunctionBlockDefinition ReadFunctionBlock(Stream stream, FunctionBlockDocumentation? documentation = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return BuildFunctionBlock(ReadAllBytes(stream), string.Empty, documentation);
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
                InlineDtdBlocks = InlineDtd.Capture(bytes),
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
                InlineDtdBlocks = InlineDtd.Capture(bytes),
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
            return Read(ReadAllBytes(stream));
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

        private static byte[] ReadAllBytes(Stream stream)
        {
            if (stream is MemoryStream memory)
            {
                return memory.ToArray();
            }
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        private static Encoding SniffEncoding(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8;   // redundant with the StreamReader's own BOM detection, but explicit
            }
            string head = Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 200));
            int declarationEnd = head.IndexOf("?>", StringComparison.Ordinal);
            if (head.StartsWith("<?xml", StringComparison.Ordinal) && declarationEnd > 0)
            {
                System.Text.RegularExpressions.Match declared = System.Text.RegularExpressions.Regex.Match(
                    head.Substring(0, declarationEnd), "encoding=[\"']([^\"']+)[\"']");
                if (declared.Success
                    && !declared.Groups[1].Value.Equals("ISO-8859-1", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        return Encoding.GetEncoding(declared.Groups[1].Value);
                    }
                    catch (ArgumentException)
                    {
                        // unknown name → fall through to Latin-1 (total: every byte decodes)
                    }
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
                reader.MoveToAttribute(i);   // includes DTD-defaulted attributes
                attrs.Add((reader.LocalName, reader.Value));
            }
            reader.MoveToElement();
            return attrs.ToImmutable();
        }
    }
}
