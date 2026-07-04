#nullable enable
using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
namespace Ihc.Vis.Io
{
    /// <summary>
    /// Parses a <c>.vis</c>/<c>.ihc</c> byte stream into the generic <see cref="Project"/> node model. Reads the
    /// declared ISO-8859-1 encoding, resolves the five XML entities and <c>&amp;#xD;&amp;#xA;</c> line breaks to
    /// logical values, and stores exactly the attributes physically present (the inline DTD is ignored, so omitted
    /// defaulted attributes are <em>not</em> materialized — preserving the omit-if-default contract for a byte-exact
    /// re-serialize). Document order of attributes and children is preserved. Anything that is not a loadable
    /// project — empty/gzip data, a BOM or foreign declared encoding, malformed or truncated XML, a
    /// non-<c>utcs_project</c> root, element character data (the model is attribute-only), or a malformed inline
    /// DTD — fails fast with a <see cref="ProjectFormatException"/> carrying actionable context.
    /// </summary>
    internal static class ProjectReader
    {
        private const int MaxElementDepth = 128;   // real projects nest ~12 deep; far past that is corrupt input

        private static readonly Regex DeclaredEncoding = new("encoding=[\"']([^\"']+)[\"']", RegexOptions.Compiled);

        public static Project Read(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            byte[] bytes = ReadAllBytes(stream);
            GuardContainer(bytes);

            ImmutableDictionary<string, string> inlineDtd;
            try
            {
                inlineDtd = InlineDtd.Capture(bytes);
            }
            catch (VisSchemaFormatException ex)
            {
                throw new ProjectFormatException($"The file's inline DTD is malformed: {ex.Message}", ex);
            }

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,   // skip the inline DTD: no ATTLIST default materialization
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true,
                CloseInput = false,
            };
            ProjectElement root;
            try
            {
                using var buffer = new MemoryStream(bytes, writable: false);
                using XmlReader reader = XmlReader.Create(buffer, settings);
                root = ReadElement(reader, depth: 0);
            }
            catch (XmlException ex)
            {
                throw new ProjectFormatException(
                    $"Not a well-formed .vis/.ihc XML document (line {ex.LineNumber}, position {ex.LinePosition}): {ex.Message}", ex);
            }
            if (root.Tag != "utcs_project")
            {
                throw new ProjectFormatException(
                    $"Root element is <{root.Tag}>, expected <utcs_project> — not an IHC .vis/.ihc project file.");
            }
            if (root.GetAttribute("version_major") is null)
            {
                throw new ProjectFormatException(
                    "The root <utcs_project> is missing its required version_major attribute — not a valid IHC project file.");
            }

            var project = new Project(root) { InlineDtdBlocks = inlineDtd };
            try
            {
                _ = project.SchemaView;   // eager: a malformed captured DTD block fails here, not at a later save
            }
            catch (VisSchemaFormatException ex)
            {
                throw new ProjectFormatException($"The file's inline DTD is malformed: {ex.Message}", ex);
            }
            return project;
        }

        private static void GuardContainer(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                throw new ProjectFormatException("The stream is empty — not a .vis/.ihc project.");
            }
            if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
            {
                throw new ProjectFormatException(
                    "The content is gzip-compressed. Controller project blobs must be decompressed first " +
                    $"({nameof(IControllerService)}.{nameof(IControllerService.GetProject)} already returns decompressed XML).");
            }
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                throw new ProjectFormatException(
                    ".vis files are ISO-8859-1 with no byte-order mark; found a UTF-8 BOM. Re-save the file as ISO-8859-1 without a BOM.");
            }
            if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
            {
                throw new ProjectFormatException(
                    ".vis files are ISO-8859-1 with no byte-order mark; found a UTF-16 BOM. Re-save the file as ISO-8859-1.");
            }
            GuardDeclaredEncoding(bytes);
        }

        private static void GuardDeclaredEncoding(byte[] bytes)
        {
            // The XML declaration is pure ASCII on the first line; a missing declaration is tolerated (the
            // XmlReader default is ASCII-compatible), but a declaration naming a foreign encoding would make
            // the reader's logical values silently disagree with the Latin-1 writer — reject it up front.
            string head = Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 200));
            if (!head.StartsWith("<?xml", StringComparison.Ordinal))
            {
                return;
            }
            int declarationEnd = head.IndexOf("?>", StringComparison.Ordinal);
            if (declarationEnd < 0)
            {
                return;   // unterminated declaration — the XmlReader reports it with position info
            }
            Match encoding = DeclaredEncoding.Match(head.Substring(0, declarationEnd));
            if (encoding.Success
                && !encoding.Groups[1].Value.Equals("ISO-8859-1", StringComparison.OrdinalIgnoreCase))
            {
                throw new ProjectFormatException(
                    $"The file declares encoding '{encoding.Groups[1].Value}'; .vis files are ISO-8859-1. " +
                    "Re-encode the file as ISO-8859-1 first (note: a re-encoded file will not round-trip byte-identically).");
            }
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

        private static ProjectElement ReadElement(XmlReader reader, int depth)
        {
            if (depth > MaxElementDepth)
            {
                throw new ProjectFormatException(
                    $"Element nesting exceeds {MaxElementDepth} levels; the file is corrupt or not a .vis project.");
            }
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
                    children.Add(ReadElement(reader, depth + 1));
                }
                else if (reader.NodeType == XmlNodeType.None)
                {
                    throw new ProjectFormatException($"Unexpected end of document inside <{tag}> — the file is truncated.");
                }
                else
                {
                    GuardNoCharacterData(reader, tag);
                    reader.Read();
                }
            }
            return new ProjectElement(tag, id, attrs, children.ToImmutable());
        }

        private static void GuardNoCharacterData(XmlReader reader, string parentTag)
        {
            if (reader.NodeType is not (XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace)
                || string.IsNullOrWhiteSpace(reader.Value))
            {
                return;   // whitespace-only nodes carry no information the model would lose
            }
            string excerpt = reader.Value.Trim();
            if (excerpt.Length > 40)
            {
                excerpt = excerpt.Substring(0, 40) + "...";
            }
            string at = reader is IXmlLineInfo { } info && info.HasLineInfo()
                ? $" (line {info.LineNumber}, position {info.LinePosition})"
                : string.Empty;
            throw new ProjectFormatException(
                $"Element <{parentTag}> contains character data (\"{excerpt}\"){at}; the .vis model is " +
                "attribute-only, and loading this file would silently lose the text on save.");
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
    }
}
