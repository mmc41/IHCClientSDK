#nullable enable
using System;
using System.IO;
using System.Xml;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// The well-formedness reparse gate of the catalog fidelity relation: the whitespace-normalized byte
    /// comparison is blind to a missing token separator (<c>&lt;!ELEMENTfooANY&gt;</c> normalizes equal to the
    /// valid declaration), so equivalence is only meaningful between well-formed documents — every acceptance of
    /// writer output first reparses the complete emitted byte stream here, and <see cref="CatalogFileWriter"/>
    /// itself refuses output that does not pass. Parsing is deliberately <b>non-validating</b>
    /// (authentic bodies instantiate element types their own DTD never declares; validity is out of scope, syntax
    /// is the point) and never fetches external resources. The parser's decoded <em>text</em> is never compared —
    /// the vendor <c>.def</c> BOM-vs-declaration mojibake is irrelevant to a syntax check.
    /// </summary>
    internal static class CatalogWellFormedness
    {
        /// <summary>Parses the complete document bytes to end-of-file; returns <c>null</c> when well-formed, else
        /// a one-line reason (message + position).</summary>
        public static string? Check(byte[] documentBytes)
        {
            ArgumentNullException.ThrowIfNull(documentBytes);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Parse,
                ValidationType = ValidationType.None,
                XmlResolver = null,
                MaxCharactersFromEntities = 10_000_000,   // DoS ceiling; the catalog envelope declares no entities
                CloseInput = false,
            };
            try
            {
                using var reader = XmlReader.Create(new MemoryStream(documentBytes, writable: false), settings);
                while (reader.Read())
                {
                }
                return null;
            }
            catch (XmlException ex)
            {
                return $"{ex.Message} (line {ex.LineNumber}, position {ex.LinePosition})";
            }
        }
    }
}
