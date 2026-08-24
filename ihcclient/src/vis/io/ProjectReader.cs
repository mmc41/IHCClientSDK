#nullable enable
using System;
using System.Collections.Immutable;
using System.IO;
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

        public static Project Read(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return Read(XmlProlog.ReadAllBytes(stream));
        }

        public static Project Read(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            GuardContainer(bytes);

            ImmutableDictionary<string, string> inlineDtd;
            try
            {
                inlineDtd = InlineDtd.Capture(bytes);
            }
            catch (VisSchemaFormatException ex)
            {
                throw new ProjectFormatException(
                    LoadRefusalCodes.DtdMalformed,
                    $"The file's inline DTD is malformed: {ex.Message}", ex);
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
                    LoadRefusalCodes.NotXml,
                    $"Not a well-formed .vis/.ihc XML document (line {ex.LineNumber}, position {ex.LinePosition}): {ex.Message}", ex);
            }
            if (root.Tag != "utcs_project")
            {
                throw new ProjectFormatException(
                    LoadRefusalCodes.RootTag,
                    $"Root element is <{root.Tag}>, expected <utcs_project> — not an IHC .vis/.ihc project file.");
            }
            if (root.GetAttribute("version_major") is null)
            {
                throw new ProjectFormatException(
                    LoadRefusalCodes.VersionMissing,
                    "The root <utcs_project> is missing its required version_major attribute — not a valid IHC project file.");
            }

            var project = new Project(root) { InlineDtdBlocks = inlineDtd };
            try
            {
                _ = project.SchemaView;   // eager: a malformed captured DTD block fails here, not at a later save
            }
            catch (VisSchemaFormatException ex)
            {
                throw new ProjectFormatException(
                    LoadRefusalCodes.DtdMalformed,
                    $"The file's inline DTD is malformed: {ex.Message}", ex);
            }
            return project;
        }

        private static void GuardContainer(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                throw new ProjectFormatException(
                    LoadRefusalCodes.Empty,
                    "The stream is empty — not a .vis/.ihc project.");
            }
            if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
            {
                throw new ProjectFormatException(
                    LoadRefusalCodes.Gzip,
                    "The content is gzip-compressed. Controller project blobs must be decompressed first " +
                    $"({nameof(IControllerService)}.{nameof(IControllerService.GetProject)} already returns decompressed XML).");
            }
            if (CatalogTextEncodingExtensions.HasUtf8Bom(bytes))
            {
                throw new ProjectFormatException(
                    LoadRefusalCodes.Utf8Bom,
                    ".vis files are ISO-8859-1 with no byte-order mark; found a UTF-8 BOM. Re-save the file as ISO-8859-1 without a BOM.");
            }
            if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
            {
                throw new ProjectFormatException(
                    LoadRefusalCodes.Utf16Bom,
                    ".vis files are ISO-8859-1 with no byte-order mark; found a UTF-16 BOM. Re-save the file as ISO-8859-1.");
            }
            GuardDeclaredEncoding(bytes);
        }

        private static void GuardDeclaredEncoding(byte[] bytes)
        {
            // The XML declaration is pure ASCII on the first line; a missing or unterminated declaration is
            // tolerated (the XmlReader default is ASCII-compatible / reports it with position info), but a
            // declaration naming a foreign encoding would make the reader's logical values silently disagree
            // with the Latin-1 writer — reject it up front.
            if (XmlProlog.TryGetDeclaredEncoding(XmlProlog.Head(bytes)) is { } declared
                && !declared.Equals("ISO-8859-1", StringComparison.OrdinalIgnoreCase))
            {
                throw new ProjectFormatException(
                    LoadRefusalCodes.DeclaredEncoding,
                    $"The file declares encoding '{declared}'; .vis files are ISO-8859-1. " +
                    "Re-encode the file as ISO-8859-1 first (note: a re-encoded file will not round-trip byte-identically).");
            }
        }

        private static ProjectElement ReadElement(XmlReader reader, int depth)
        {
            if (depth > MaxElementDepth)
            {
                throw new ProjectFormatException(
                    LoadRefusalCodes.Depth,
                    $"Element nesting exceeds {MaxElementDepth} levels; the file is corrupt or not a .vis project.");
            }
            reader.MoveToContent();
            string tag = reader.LocalName;
            ImmutableArray<(string, string)> attrs = ReadAttributes(reader);
            string? idToken = ProjectElement.GetAttribute(attrs, "id");
            ElementId? id = ElementId.ParseOrNull(idToken);

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
                    throw new ProjectFormatException(
                        LoadRefusalCodes.NotXml,
                        $"Unexpected end of document inside <{tag}> — the file is truncated.");
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
                LoadRefusalCodes.CharacterData,
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
