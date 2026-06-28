#nullable enable
using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Xml;

namespace Ihc.Projects
{
    /// <summary>
    /// Reads a vendor component/template file (<c>Products\*.def</c>, <c>FunctionBlocks\*.ifb</c>,
    /// <c>Data\NewDoc.idf</c>, <c>Data\EnumeratorDefinitions.def</c>) into the generic <see cref="ProjectElement"/>
    /// node model, <b>applying the file's own internal-DTD ATTLIST defaults</b>. This is the crucial difference
    /// from <see cref="ProjectReader"/> (which ignores the DTD): catalog instances routinely omit attributes such
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
    internal static class CatalogReader
    {
        public static ProjectElement ReadFile(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Read(stream);
        }

        public static ProjectElement Read(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
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
            // mojibakes the Products\*.def files. A StreamReader with BOM detection picks UTF-8 when the BOM is
            // present and falls back to Latin-1 (the .ifb/.idf encoding) otherwise; handing a TextReader to
            // XmlReader makes it use that decoding and ignore the (often wrong) declared encoding.
            using var textReader = new StreamReader(stream, Encoding.Latin1, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            using XmlReader reader = XmlReader.Create(textReader, settings);
            return ReadElement(reader);
        }

        private static ProjectElement ReadElement(XmlReader reader)
        {
            reader.MoveToContent();
            string tag = reader.LocalName;
            ImmutableArray<(string, string)> attrs = ReadAttributes(reader);
            string? idToken = GetAttr(attrs, "id");
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

        private static string? GetAttr(ImmutableArray<(string Name, string Value)> attrs, string name)
        {
            foreach ((string Name, string Value) a in attrs)
            {
                if (a.Name == name)
                {
                    return a.Value;
                }
            }
            return null;
        }
    }
}
