#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Text;

namespace Ihc.Projects
{
    /// <summary>
    /// The SDK's built-in <c>.vis</c> wire grammar: one <see cref="ElementSchema"/> per element type, each built from
    /// the verbatim canonical <c>&lt;!ELEMENT&gt;/&lt;!ATTLIST&gt;</c> block (embedded <c>CanonicalDtdBlocks.dtd</c>),
    /// with its ordered attribute facts parsed from that same block so the byte-emitted DTD and the structured
    /// attribute model can never drift. It is the grammar source for <strong>creating</strong> a new project and for
    /// <strong>inserting</strong> catalog components — deliberately <em>not</em> a complete catalog of every element
    /// type a project may contain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Completeness is impossible: IHC Visual lets installers author custom products/function blocks, and any real
    /// file can use element types the SDK has never seen. So <strong>round-trip (load/edit/save) is open-world</strong>
    /// — it sources each type's grammar from the file's <em>own</em> inline DTD (<see cref="Project.InlineDtdBlocks"/>
    /// via <see cref="ProjectSchemaView"/>), and consults this registry only as the fallback for types the file does
    /// not carry. The registry's size is therefore invisible to round-trip and to new-project output (the emitter
    /// writes a block only for the types actually present, not the whole registry).
    /// </para>
    /// <para>
    /// The blocks are the verbatim DTD IHC Visual writes in a real <c>.vis</c> (spec ch. 01 §8 canon). It covers the
    /// <strong>38 byte-verified</strong> types in the testdata (the empty project's 13 plus 25 in the complex sample)
    /// plus the curated grammar of the further <strong>insertable vendor catalog families</strong> (airlink,
    /// rs485 led-dimmer/sms-modem, s0, dimmer/shutter settings, <c>program_case</c>, the extra <c>resource_*</c> kinds).
    /// Those families are kept here because the catalog <c>.def</c>/<c>.ifb</c> templates are <em>not</em> a reliable
    /// wire-grammar source — they are pre-customization templates with copy-pasted/incomplete DTDs (e.g. a body that
    /// uses an element type its own DTD never declares). They are structurally tested via catalog insert/round-trip,
    /// but are not yet byte-verified (no committable vendor <c>.vis</c> uses them).
    /// </para>
    /// </remarks>
    internal static class ProjectSchemaRegistry
    {
        private const string ResourceName = "Ihc.Projects.CanonicalDtdBlocks.dtd";
        private const string ElementMarker = "<!ELEMENT ";   // matched anywhere on a line — vendor .def/.ifb DTDs indent with tabs or start at column 0

        private static readonly FrozenDictionary<string, ElementSchema> ByTag = Build();

        /// <summary>The schema for the given element tag, or <c>null</c> when the type is not in the registry.</summary>
        public static ElementSchema? TryGet(string tag) => ByTag.TryGetValue(tag, out ElementSchema? schema) ? schema : null;

        /// <summary>The schema for the given element tag; throws a coverage error when the type is unknown.</summary>
        public static ElementSchema Get(string tag) =>
            TryGet(tag) ?? throw new InvalidOperationException(
                $"No schema registered for .vis element type '{tag}'. The schema registry must declare every " +
                $"element type a project uses; add its canonical DTD block to {ResourceName}.");

        private static FrozenDictionary<string, ElementSchema> Build()
        {
            string dtd = ReadResource();
            var schemas = new Dictionary<string, ElementSchema>(StringComparer.Ordinal);
            foreach (string block in SplitBlocks(dtd))
            {
                ElementSchema schema = ParseBlock(block);
                schemas[schema.Tag] = schema;
            }
            return schemas.ToFrozenDictionary(StringComparer.Ordinal);
        }

        private static string ReadResource()
        {
            Assembly assembly = typeof(ProjectSchemaRegistry).Assembly;
            using Stream? stream = assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Embedded schema resource '{ResourceName}' not found.");
            // The blocks are pure ASCII; read as Latin-1 so the bytes are preserved exactly.
            using var reader = new StreamReader(stream, Encoding.Latin1);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Splits the resource into per-element verbatim blocks. Each block runs from the start of its
        /// <c>&lt;!ELEMENT</c> line (including any leading indent — clean <c>.vis</c> blocks use 3 spaces, vendor
        /// <c>.def</c>/<c>.ifb</c> DTDs may use tabs or column 0) to just before the next one (or end), so it carries
        /// the element + attlist declarations and the trailing CRLF the emitter re-uses verbatim.
        /// </summary>
        internal static IEnumerable<string> SplitBlocks(string dtd)
        {
            int marker = dtd.IndexOf(ElementMarker, StringComparison.Ordinal);
            int start = marker >= 0 ? LineStart(dtd, marker) : -1;
            while (start >= 0)
            {
                int nextMarker = dtd.IndexOf(ElementMarker, marker + ElementMarker.Length, StringComparison.Ordinal);
                int next = nextMarker >= 0 ? LineStart(dtd, nextMarker) : -1;
                yield return next >= 0 ? dtd.Substring(start, next - start) : dtd.Substring(start);
                marker = nextMarker;
                start = next;
            }
        }

        /// <summary>The index of the first character of the line containing <paramref name="index"/>.</summary>
        private static int LineStart(string s, int index)
        {
            int newline = s.LastIndexOf('\n', index);
            return newline >= 0 ? newline + 1 : 0;
        }

        internal static ElementSchema ParseBlock(string block)
        {
            string tag = ReadTag(block);
            ImmutableArray<AttrSchema> attrs = ParseAttrs(block, tag);
            return new ElementSchema(tag, TypeCode.ForTag(tag), block, attrs);
        }

        internal static string ReadTag(string block)
        {
            // block begins with optional indent then "<!ELEMENT <tag> ANY>..."
            int nameStart = block.IndexOf(ElementMarker, StringComparison.Ordinal) + ElementMarker.Length;
            int nameEnd = block.IndexOf(' ', nameStart);
            return block.Substring(nameStart, nameEnd - nameStart);
        }

        private static ImmutableArray<AttrSchema> ParseAttrs(string block, string tag)
        {
            const string attlistMarker = "<!ATTLIST ";
            int ai = block.IndexOf(attlistMarker, StringComparison.Ordinal);
            if (ai < 0)
            {
                return ImmutableArray<AttrSchema>.Empty;
            }
            string attlist = block.Substring(ai + attlistMarker.Length);
            int closeGt = attlist.LastIndexOf('>');                 // the only '>' in the ATTLIST is its close
            string decl = attlist.Substring(0, closeGt);            // "<tag> <attr decls...>"

            // Drop the element name and collapse all whitespace (CRLF + 18-space continuations) to single spaces.
            string body = CollapseWhitespace(decl);
            string prefix = tag + " ";
            if (body.StartsWith(prefix, StringComparison.Ordinal))
            {
                body = body.Substring(prefix.Length);
            }
            return TokenizeAttrs(body);
        }

        private static string CollapseWhitespace(string s)
        {
            var sb = new StringBuilder(s.Length);
            bool inWhitespace = false;
            foreach (char c in s)
            {
                if (c is ' ' or '\t' or '\r' or '\n')
                {
                    inWhitespace = true;
                }
                else
                {
                    if (inWhitespace && sb.Length > 0)
                    {
                        sb.Append(' ');
                    }
                    inWhitespace = false;
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static ImmutableArray<AttrSchema> TokenizeAttrs(string body)
        {
            var result = ImmutableArray.CreateBuilder<AttrSchema>();
            int pos = 0;
            int len = body.Length;
            while (pos < len)
            {
                SkipSpaces(body, ref pos, len);
                if (pos >= len)
                {
                    break;
                }
                string name = ReadWord(body, ref pos, len);
                SkipSpaces(body, ref pos, len);

                // TYPE: an enumeration "( ... )" or a keyword (CDATA/ID/IDREF/...).
                AttrRender render;
                ImmutableArray<string> enumValues = ImmutableArray<string>.Empty;
                if (pos < len && body[pos] == '(')
                {
                    int close = body.IndexOf(')', pos);
                    string inside = body.Substring(pos + 1, close - pos - 1);
                    enumValues = SplitEnum(inside);
                    render = AttrRender.Text;     // enumerated tokens are written verbatim
                    pos = close + 1;
                }
                else
                {
                    string type = ReadWord(body, ref pos, len);
                    render = type switch
                    {
                        "ID" => AttrRender.Id,
                        "IDREF" => AttrRender.IdRef,
                        "IDREFS" => AttrRender.IdRef,
                        _ => AttrRender.Text,     // CDATA (Decimal sub-classification is a Stage-2 concern)
                    };
                }
                SkipSpaces(body, ref pos, len);

                // DEFAULT: #REQUIRED | #IMPLIED | #FIXED "v" | "v"
                AttrKind kind;
                string def = string.Empty;
                if (pos < len && body[pos] == '#')
                {
                    string keyword = ReadWord(body, ref pos, len);
                    if (keyword == "#REQUIRED")
                    {
                        kind = AttrKind.Required;
                    }
                    else if (keyword == "#IMPLIED")
                    {
                        kind = AttrKind.Implied;
                    }
                    else // #FIXED "value" — never observed in v4, mapped to a fixed default
                    {
                        SkipSpaces(body, ref pos, len);
                        def = ReadQuoted(body, ref pos, len);
                        kind = AttrKind.Defaulted;
                    }
                }
                else
                {
                    def = ReadQuoted(body, ref pos, len);
                    kind = AttrKind.Defaulted;
                }

                result.Add(new AttrSchema(name, kind, render, def, enumValues));
            }
            return result.ToImmutable();
        }

        private static ImmutableArray<string> SplitEnum(string inside)
        {
            var values = ImmutableArray.CreateBuilder<string>();
            foreach (string part in inside.Split('|'))
            {
                values.Add(part.Trim());
            }
            return values.ToImmutable();
        }

        private static void SkipSpaces(string s, ref int pos, int len)
        {
            while (pos < len && s[pos] == ' ')
            {
                pos++;
            }
        }

        private static string ReadWord(string s, ref int pos, int len)
        {
            int start = pos;
            while (pos < len && s[pos] != ' ')
            {
                pos++;
            }
            return s.Substring(start, pos - start);
        }

        private static string ReadQuoted(string s, ref int pos, int len)
        {
            // s[pos] is the opening quote.
            int open = pos + 1;
            int close = s.IndexOf('"', open);
            pos = close + 1;
            return s.Substring(open, close - open);
        }
    }
}
