#nullable enable
using System;
using System.Collections.Immutable;
using System.IO;
using System.Xml;

namespace Ihc.Projects
{
    /// <summary>
    /// Parses a <c>.vis</c>/<c>.ihc</c> byte stream into the generic <see cref="Project"/> node model. Reads the
    /// declared ISO-8859-1 encoding, resolves the five XML entities and <c>&amp;#xD;&amp;#xA;</c> line breaks to
    /// logical values, and stores exactly the attributes physically present (the inline DTD is ignored, so omitted
    /// defaulted attributes are <em>not</em> materialized — preserving the omit-if-default contract for a byte-exact
    /// re-serialize). Document order of attributes and children is preserved.
    /// </summary>
    internal static class ProjectReader
    {
        public static Project Read(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            byte[] bytes = ReadAllBytes(stream);
            ImmutableDictionary<string, string> inlineDtd = InlineDtd.Capture(bytes);

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,   // skip the inline DTD: no ATTLIST default materialization
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true,
                CloseInput = false,
            };
            using var buffer = new MemoryStream(bytes, writable: false);
            using XmlReader reader = XmlReader.Create(buffer, settings);
            ProjectElement root = ReadElement(reader);
            return new Project(root) { InlineDtdBlocks = inlineDtd };
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
            reader.Read(); // consume start tag
            while (true)
            {
                reader.MoveToContent();
                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    reader.Read(); // consume end tag
                    break;
                }
                if (reader.NodeType == XmlNodeType.Element)
                {
                    children.Add(ReadElement(reader));
                }
                else if (reader.NodeType == XmlNodeType.None)
                {
                    break; // defensive: malformed/truncated
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
                attrs.Add((reader.LocalName, reader.Value)); // reader.Value is the unescaped logical value
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
