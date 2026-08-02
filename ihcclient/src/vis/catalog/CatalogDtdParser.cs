#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using Ihc.Vis.Model;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Parses a catalog component file's header text (XML prolog + <c>&lt;!DOCTYPE root[ … ]&gt;</c>) into a
    /// structured <see cref="CatalogGrammar"/> — the inverse of <see cref="CatalogDtdEmitter"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Strict mode</b> (<see cref="ParseStrict"/>, the code generator's envelope guard): any construct
    /// outside the corpus-measured envelope — DTD comment, ENTITY, parameter entity, NOTATION, <c>#FIXED</c>, a
    /// non-<c>ANY</c> content model, a SYSTEM/PUBLIC external id, an attribute type beyond
    /// ID/IDREF/CDATA/enumeration, a second declaration for a tag, a non-adjacent ELEMENT/ATTLIST pair, or an
    /// unexpected prolog form — throws <see cref="CatalogFormatException"/>, so generation fails loudly if a future
    /// catalog version exceeds the model.</para>
    /// <para><b>Lenient mode</b> (<see cref="ParseLenient"/>, the <see cref="CatalogReader"/> read path): the same
    /// inputs fall back to capturing the whole header as the grammar's byte-faithful <c>VerbatimHead</c>
    /// <em>and</em> still harvest the best-effort structured projection of every individually parseable
    /// declaration — a declaration that is itself out-of-envelope (e.g. <c>#FIXED</c>) is skipped and survives
    /// only in the verbatim text. An in-envelope header parses to the pure structured state (no fallback).</para>
    /// <para>The end-of-subset scan is quote-aware everywhere (a <c>]&gt;</c> inside a quoted default literal must
    /// not truncate the header — corpus: zero occurrences; user files: possible).</para>
    /// </remarks>
    internal static class CatalogDtdParser
    {
        /// <summary>Strict-parses a header; throws <see cref="CatalogFormatException"/> on any construct outside
        /// the catalog grammar envelope.</summary>
        public static CatalogGrammar ParseStrict(string headText)
        {
            ArgumentNullException.ThrowIfNull(headText);
            return ParseCore(headText);
        }

        /// <summary>Parses a header, falling back to the verbatim-head-plus-projection state on any
        /// out-of-envelope construct (never throws on exotic input).</summary>
        public static CatalogGrammar ParseLenient(string headText)
        {
            ArgumentNullException.ThrowIfNull(headText);
            try
            {
                return ParseCore(headText);
            }
            catch (CatalogFormatException)
            {
                return LenientFallback(headText);
            }
        }

        /// <summary>
        /// The verbatim file header decoded from raw file bytes: everything up to (not including) the body root
        /// element — using the same encoding sniff as <see cref="CatalogReader"/> and a <b>quote-aware</b>
        /// end-of-subset scan, so a <c>]&gt;</c> inside a quoted default cannot truncate the capture. Returns the
        /// whole text when no root element follows the header (degenerate input; the lenient parser then falls
        /// back).
        /// </summary>
        public static string CaptureHeadText(byte[] fileBytes)
        {
            ArgumentNullException.ThrowIfNull(fileBytes);
            string text;
            using (var reader = new StreamReader(new MemoryStream(fileBytes, writable: false),
                                                 CatalogReader.SniffEncoding(fileBytes),
                                                 detectEncodingFromByteOrderMarks: true))
            {
                text = reader.ReadToEnd();
            }
            int doctype = text.IndexOf("<!DOCTYPE", StringComparison.Ordinal);
            int rootStart;
            if (doctype < 0)
            {
                // No DOCTYPE at all — the header is the prolog up to the first element after it.
                int prologEnd = text.IndexOf("?>", StringComparison.Ordinal);
                rootStart = text.IndexOf('<', prologEnd >= 0 ? prologEnd + 2 : 0);
            }
            else
            {
                // A '[' that opens the internal subset comes BEFORE the DOCTYPE's own closing '>'. If it does, skip
                // past the subset's ']>' to the body root; otherwise the DOCTYPE has NO internal subset (a bare
                // '<!DOCTYPE root>' or an external-id one), and the body root is the first element after the DOCTYPE's
                // '>'. The old code took the first '<' after the prolog, which landed on '<!DOCTYPE' itself and dropped
                // the whole DOCTYPE line from the captured head — a silent read→write byte-fidelity loss (review D1).
                int open = text.IndexOf('[', doctype);
                int doctypeGt = text.IndexOf('>', doctype);
                if (open >= 0 && (doctypeGt < 0 || open < doctypeGt))
                {
                    int close = FindSubsetEnd(text, open + 1);
                    rootStart = close >= 0 ? text.IndexOf('<', close) : -1;
                }
                else
                {
                    rootStart = doctypeGt >= 0 ? text.IndexOf('<', doctypeGt + 1) : -1;
                }
            }
            return rootStart >= 0 ? text.Substring(0, rootStart) : text;
        }

        // The index just past the '>' closing the internal subset ("]" then optional whitespace then ">" — the
        // vendor-tolerant form), or -1 when it never closes. Adapts the shared quote- and comment-aware scanner
        // (XmlText.FindDtdSubsetClose) to this parser's past-the-'>' convention.
        private static int FindSubsetEnd(string s, int start) =>
            XmlText.FindDtdSubsetClose(s, start, allowWhitespaceBeforeGt: true, out int afterEnd) >= 0 ? afterEnd : -1;

        // ----- strict core -----

        private static readonly Regex PrologShape = new(
            "^<\\?xml\\s+version\\s*=\\s*\"1\\.0\"\\s+encoding\\s*=\\s*\"(" + CatalogGrammar.EncName + ")\"\\s*\\?>$",
            RegexOptions.CultureInvariant);

        private static CatalogGrammar ParseCore(string headText)
        {
            var cursor = new Cursor(headText);
            cursor.SkipBomAndWhitespace();

            string declaredEncoding = ReadPrologStrict(cursor);
            cursor.SkipWhitespace();

            if (!cursor.TryConsume("<!DOCTYPE"))
            {
                throw new CatalogFormatException(
                    "Catalog header has no <!DOCTYPE …[ internal subset ]> — outside the catalog grammar envelope.");
            }
            cursor.RequireWhitespace("<!DOCTYPE");
            string doctypeRoot = cursor.ReadName("DOCTYPE root name");
            cursor.SkipWhitespace();
            if (!cursor.TryConsume("["))
            {
                throw new CatalogFormatException(
                    $"DOCTYPE '{doctypeRoot}' does not open an internal subset directly — an external id " +
                    "(SYSTEM/PUBLIC) or other content before '[' is outside the catalog grammar envelope.");
            }

            var records = new Records(strict: true);
            while (true)
            {
                cursor.SkipWhitespace();
                if (cursor.TryConsume("]"))
                {
                    cursor.SkipWhitespace();
                    if (!cursor.TryConsume(">"))
                    {
                        throw new CatalogFormatException("Internal subset ']' is not followed by '>'.");
                    }
                    break;
                }
                if (cursor.AtEnd)
                {
                    throw new CatalogFormatException("Catalog header ends inside the DOCTYPE internal subset.");
                }
                ParseDeclaration(cursor, records);
            }

            cursor.SkipWhitespace();
            if (!cursor.AtEnd)
            {
                throw new CatalogFormatException(
                    $"Unexpected content after ']>' in the catalog header: '{cursor.Excerpt()}'.");
            }
            return Guarded(() => CatalogGrammar.Create(records.ToImmutable(), declaredEncoding, doctypeRoot));
        }

        private static string ReadPrologStrict(Cursor cursor)
        {
            if (!cursor.Peek("<?xml"))
            {
                throw new CatalogFormatException(
                    "Catalog header does not start with an XML prolog — outside the catalog grammar envelope.");
            }
            string prolog = cursor.ReadThrough("?>", "XML prolog");
            Match match = PrologShape.Match(prolog);
            if (!match.Success)
            {
                throw new CatalogFormatException(
                    $"XML prolog '{prolog}' is not the <?xml version=\"1.0\" encoding=\"…\"?> form of the " +
                    "catalog grammar envelope.");
            }
            return match.Groups[1].Value;
        }

        private static void ParseDeclaration(Cursor cursor, Records records)
        {
            if (cursor.Peek("<!--"))
            {
                throw new CatalogFormatException("DTD comment — outside the catalog grammar envelope.");
            }
            if (cursor.Peek("<!ENTITY"))
            {
                throw new CatalogFormatException("ENTITY declaration — outside the catalog grammar envelope.");
            }
            if (cursor.Peek("<!NOTATION"))
            {
                throw new CatalogFormatException("NOTATION declaration — outside the catalog grammar envelope.");
            }
            if (cursor.Peek("%"))
            {
                throw new CatalogFormatException("Parameter-entity reference — outside the catalog grammar envelope.");
            }
            if (cursor.TryConsume("<!ELEMENT"))
            {
                cursor.RequireWhitespace("<!ELEMENT");
                string tag = cursor.ReadName("element name");
                cursor.SkipWhitespace();
                if (!cursor.TryConsume("ANY"))
                {
                    throw new CatalogFormatException(
                        $"<!ELEMENT {tag}> declares a content model other than ANY — outside the catalog grammar envelope.");
                }
                cursor.SkipWhitespace();
                if (!cursor.TryConsume(">"))
                {
                    throw new CatalogFormatException($"<!ELEMENT {tag} ANY is not closed by '>'.");
                }
                records.AddElement(tag);
                return;
            }
            if (cursor.TryConsume("<!ATTLIST"))
            {
                cursor.RequireWhitespace("<!ATTLIST");
                string tag = cursor.ReadName("ATTLIST element name");
                var attrs = ImmutableArray.CreateBuilder<GrammarAttr>();
                while (true)
                {
                    cursor.SkipWhitespace();
                    if (cursor.TryConsume(">"))
                    {
                        break;
                    }
                    if (cursor.AtEnd)
                    {
                        throw new CatalogFormatException($"<!ATTLIST {tag}> is never closed.");
                    }
                    attrs.Add(ParseAttrDefinition(cursor, tag));
                }
                records.AddAttlist(tag, attrs.ToImmutable());
                return;
            }
            throw new CatalogFormatException(
                $"Unexpected content in the DOCTYPE internal subset: '{cursor.Excerpt()}'.");
        }

        private static GrammarAttr ParseAttrDefinition(Cursor cursor, string tag)
        {
            string name = cursor.ReadName($"attribute name in <!ATTLIST {tag}>");
            cursor.SkipWhitespace();

            GrammarAttrType type;
            ImmutableArray<string> tokens = ImmutableArray<string>.Empty;
            if (cursor.TryConsume("("))
            {
                var list = ImmutableArray.CreateBuilder<string>();
                while (true)
                {
                    cursor.SkipWhitespace();
                    list.Add(cursor.ReadNmtoken($"enumeration token of '{name}' in <!ATTLIST {tag}>"));
                    cursor.SkipWhitespace();
                    if (cursor.TryConsume(")"))
                    {
                        break;
                    }
                    if (!cursor.TryConsume("|"))
                    {
                        throw new CatalogFormatException(
                            $"Enumeration of '{name}' in <!ATTLIST {tag}> is not '|'-separated or ')'-closed.");
                    }
                }
                type = GrammarAttrType.Enumerated;
                tokens = list.ToImmutable();
            }
            else
            {
                string keyword = cursor.ReadName($"attribute type of '{name}' in <!ATTLIST {tag}>");
                type = keyword switch
                {
                    "ID" => GrammarAttrType.Id,
                    "IDREF" => GrammarAttrType.IdRef,
                    "CDATA" => GrammarAttrType.Cdata,
                    _ => throw new CatalogFormatException(
                        $"Attribute type '{keyword}' of '{name}' in <!ATTLIST {tag}> — outside the catalog " +
                        "grammar envelope (ID/IDREF/CDATA/enumeration)."),
                };
            }

            cursor.SkipWhitespace();
            GrammarDefault kind;
            string? rawLiteral = null;
            if (cursor.TryConsume("#REQUIRED"))
            {
                kind = GrammarDefault.Required;
            }
            else if (cursor.TryConsume("#IMPLIED"))
            {
                kind = GrammarDefault.Implied;
            }
            else if (cursor.Peek("#FIXED"))
            {
                throw new CatalogFormatException(
                    $"#FIXED default of '{name}' in <!ATTLIST {tag}> — outside the catalog grammar envelope.");
            }
            else if (cursor.TryConsume("\""))
            {
                kind = GrammarDefault.Literal;
                rawLiteral = cursor.ReadThroughQuote('"', $"default literal of '{name}' in <!ATTLIST {tag}>");
            }
            else if (cursor.Peek("'"))
            {
                throw new CatalogFormatException(
                    $"Single-quoted default literal of '{name}' in <!ATTLIST {tag}> — outside the catalog grammar " +
                    "envelope (the model re-emits literals between double quotes).");
            }
            else
            {
                throw new CatalogFormatException(
                    $"Attribute '{name}' in <!ATTLIST {tag}> has no default declaration " +
                    $"(#REQUIRED/#IMPLIED/quoted literal): '{cursor.Excerpt()}'.");
            }
            return Guarded(() => GrammarAttr.Create(name, type, tokens, kind, rawLiteral));
        }

        // Model-factory validation failures (duplicate names, two IDs, malformed literals, …) are envelope
        // violations when they come from parsed text — surface them as the parser's typed exception so strict
        // mode fails loudly and lenient mode can fall back.
        private static T Guarded<T>(Func<T> build)
        {
            try
            {
                return build();
            }
            catch (ArgumentException ex)
            {
                throw new CatalogFormatException(ex.Message, ex);
            }
        }

        // ----- lenient fallback -----

        private static readonly Regex LooseEncoding = new(
            "encoding\\s*=\\s*[\"'](" + CatalogGrammar.EncName + ")[\"']", RegexOptions.CultureInvariant);

        private static readonly Regex LooseDoctype = new(
            "<!DOCTYPE\\s+([^\\s\\[>]+)", RegexOptions.CultureInvariant);

        // Captures the whole header as the byte-faithful VerbatimHead and harvests the best-effort structured
        // projection: every individually parseable in-envelope declaration is kept (first declaration per tag
        // wins; later duplicates and out-of-envelope declarations are skipped — they survive in the verbatim
        // text), so defaults materialization, IDREF detection and open-world hoisting keep working for exotic
        // user files. Never throws.
        private static CatalogGrammar LenientFallback(string headText)
        {
            string declaredEncoding = CatalogGrammar.DefaultDeclaredEncoding;
            int prologEnd = headText.IndexOf("?>", StringComparison.Ordinal);
            if (prologEnd > 0 && LooseEncoding.Match(headText, 0, prologEnd) is { Success: true } encoding)
            {
                declaredEncoding = encoding.Groups[1].Value;
            }

            string? doctypeRoot = null;
            if (LooseDoctype.Match(headText) is { Success: true } doctype)
            {
                string candidate = doctype.Groups[1].Value;
                try
                {
                    GrammarAttr.VerifyXmlName(candidate, "DOCTYPE root");
                    doctypeRoot = candidate;
                }
                catch (ArgumentException)
                {
                    // not a usable name — leave null (the verbatim head still serializes the file faithfully)
                }
            }

            var records = new Records(strict: false);
            int doctypeAt = headText.IndexOf("<!DOCTYPE", StringComparison.Ordinal);
            int open = doctypeAt >= 0 ? headText.IndexOf('[', doctypeAt) : -1;
            if (open >= 0)
            {
                int end = FindSubsetEnd(headText, open + 1);
                string subset = headText.Substring(open + 1, (end >= 0 ? end - 1 : headText.Length) - (open + 1));
                HarvestDeclarations(subset, records);
            }
            try
            {
                return CatalogGrammar.CreateWithVerbatimHead(headText, records.ToImmutable(), declaredEncoding, doctypeRoot);
            }
            catch (ArgumentException)
            {
                // Belt: even a wholly unprojectable header still serializes byte-faithfully via the verbatim text.
                return CatalogGrammar.CreateWithVerbatimHead(headText, ImmutableArray<GrammarDeclaration>.Empty,
                    CatalogGrammar.DefaultDeclaredEncoding, doctypeRoot: null);
            }
        }

        private static void HarvestDeclarations(string subset, Records records)
        {
            var cursor = new Cursor(subset);
            while (true)
            {
                // Scan to the next declaration marker; anything else (comments, entities, PE references) is
                // skipped quote-unaware here because we only ever *start* parsing at a '<!' marker and each
                // parse attempt below re-scans quote-aware.
                int element = subset.IndexOf("<!ELEMENT", cursor.Position, StringComparison.Ordinal);
                int attlist = subset.IndexOf("<!ATTLIST", cursor.Position, StringComparison.Ordinal);
                int next = element < 0 ? attlist : attlist < 0 ? element : Math.Min(element, attlist);
                if (next < 0)
                {
                    return;
                }
                cursor.MoveTo(next);
                int before = cursor.Position;
                try
                {
                    ParseDeclaration(cursor, records);
                }
                catch (CatalogFormatException)
                {
                    // Out-of-envelope declaration — skip past its marker and keep harvesting neighbours.
                    cursor.MoveTo(before + 1);
                }
            }
        }

        // ----- record assembly (the ordered per-tag list, with the corpus adjacency rule) -----

        private sealed class Records
        {
            private readonly bool strict;
            private readonly List<(string Tag, bool HasElement, ImmutableArray<GrammarAttr> Attrs)> list = new();
            private readonly HashSet<string> tags = new(StringComparer.Ordinal);

            public Records(bool strict) => this.strict = strict;

            public void AddElement(string tag)
            {
                if (tags.Contains(tag))
                {
                    if (strict)
                    {
                        throw new CatalogFormatException(
                            $"Duplicate <!ELEMENT {tag}> declaration — outside the catalog grammar envelope.");
                    }
                    return;   // lenient: first declaration wins, the duplicate survives in the verbatim text
                }
                tags.Add(tag);
                list.Add((tag, true, ImmutableArray<GrammarAttr>.Empty));
            }

            public void AddAttlist(string tag, ImmutableArray<GrammarAttr> attrs)
            {
                if (attrs.IsEmpty && !(list.Count > 0 && list[^1].Tag == tag))
                {
                    // An orphan <!ATTLIST tag> with zero attribute definitions renders as no text at all —
                    // unrepresentable in the model.
                    if (strict)
                    {
                        throw new CatalogFormatException(
                            $"Orphan <!ATTLIST {tag}> declares no attributes — outside the catalog grammar envelope.");
                    }
                    return;
                }
                if (tags.Contains(tag))
                {
                    // Only the corpus shape "ATTLIST immediately after its own ELEMENT" attaches; anything else
                    // (multi-ATTLIST, interleaved pair) is outside the envelope.
                    int last = list.Count - 1;
                    if (last >= 0 && list[last].Tag == tag && list[last].HasElement && list[last].Attrs.IsEmpty)
                    {
                        list[last] = (tag, true, attrs);
                        return;
                    }
                    if (strict)
                    {
                        throw new CatalogFormatException(
                            $"<!ATTLIST {tag}> does not immediately follow its own <!ELEMENT> (second ATTLIST or " +
                            "interleaved declarations) — outside the catalog grammar envelope.");
                    }
                    return;   // lenient: keep the first declaration for the tag
                }
                tags.Add(tag);
                list.Add((tag, false, attrs));   // orphan ATTLIST
            }

            public ImmutableArray<GrammarDeclaration> ToImmutable()
            {
                var result = ImmutableArray.CreateBuilder<GrammarDeclaration>(list.Count);
                foreach ((string tag, bool hasElement, ImmutableArray<GrammarAttr> attrs) in list)
                {
                    result.Add(GrammarDeclaration.Create(tag, hasElement, attrs));
                }
                return result.MoveToImmutable();
            }
        }

        // ----- text cursor -----

        private sealed class Cursor
        {
            private readonly string s;

            public Cursor(string s) => this.s = s;

            public int Position { get; private set; }

            public bool AtEnd => Position >= s.Length;

            public void MoveTo(int position) => Position = position;

            public void SkipBomAndWhitespace()
            {
                if (Position < s.Length && s[Position] == '﻿')
                {
                    Position++;
                }
                SkipWhitespace();
            }

            public void SkipWhitespace()
            {
                while (Position < s.Length && char.IsWhiteSpace(s[Position]))
                {
                    Position++;
                }
            }

            public void RequireWhitespace(string after)
            {
                if (Position >= s.Length || !char.IsWhiteSpace(s[Position]))
                {
                    throw new CatalogFormatException($"Expected whitespace after {after}: '{Excerpt()}'.");
                }
                SkipWhitespace();
            }

            public bool Peek(string token) => s.AsSpan(Position).StartsWith(token, StringComparison.Ordinal);

            public bool TryConsume(string token)
            {
                if (!Peek(token))
                {
                    return false;
                }
                Position += token.Length;
                return true;
            }

            public string ReadThrough(string terminator, string what)
            {
                int end = s.IndexOf(terminator, Position, StringComparison.Ordinal);
                if (end < 0)
                {
                    throw new CatalogFormatException($"{what} is never terminated by '{terminator}'.");
                }
                string result = s.Substring(Position, end + terminator.Length - Position);
                Position = end + terminator.Length;
                return result;
            }

            public string ReadThroughQuote(char quote, string what)
            {
                int end = s.IndexOf(quote, Position);
                if (end < 0)
                {
                    throw new CatalogFormatException($"{what} has an unterminated quote.");
                }
                string result = s.Substring(Position, end - Position);
                Position = end + 1;
                return result;
            }

            public string ReadName(string what) => ReadToken(what);

            public string ReadNmtoken(string what) => ReadToken(what);

            // One token class (XML NameChar) serves both ReadName and ReadNmtoken — a superset of what distinguishes
            // tokens here — so ReadToken takes no char-class delegate; the model factories (XmlConvert) do the
            // spec-exact Name-vs-Nmtoken validation.
            private string ReadToken(string what)
            {
                int start = Position;
                while (Position < s.Length && IsNameChar(s[Position]))
                {
                    Position++;
                }
                if (Position == start)
                {
                    throw new CatalogFormatException($"Expected {what}: '{Excerpt()}'.");
                }
                return s.Substring(start, Position - start);
            }

            private static bool IsNameChar(char c) =>
                char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or ':' || c > 0x7F;

            public string Excerpt()
            {
                int len = Math.Min(40, s.Length - Position);
                return len <= 0 ? "<end of header>" : s.Substring(Position, len);
            }
        }
    }
}
