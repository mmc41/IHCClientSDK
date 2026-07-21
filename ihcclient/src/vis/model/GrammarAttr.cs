#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml;

namespace Ihc.Vis.Model
{
    /// <summary>The DTD attribute type of a catalog grammar attribute — the four kinds the vendor catalog
    /// envelope uses (corpus-measured: <c>ID</c>, <c>IDREF</c>, <c>CDATA</c> and enumerations; nothing else).</summary>
    public enum GrammarAttrType
    {
        /// <summary>DTD type <c>ID</c> — the element's own allocatable identifier.</summary>
        Id,

        /// <summary>DTD type <c>IDREF</c> — a reference to another element's id, remapped on insert.</summary>
        IdRef,

        /// <summary>DTD type <c>CDATA</c> — opaque text.</summary>
        Cdata,

        /// <summary>A DTD enumeration <c>(tok | tok | …)</c> of NMTOKEN values.</summary>
        Enumerated,
    }

    /// <summary>How a catalog grammar attribute's default is declared.</summary>
    public enum GrammarDefault
    {
        /// <summary><c>#REQUIRED</c> — the attribute must be present on every instance.</summary>
        Required,

        /// <summary><c>#IMPLIED</c> — optional, no default value.</summary>
        Implied,

        /// <summary>A quoted default literal — supplied by the DTD when the instance omits the attribute.</summary>
        Literal,
    }

    /// <summary>
    /// One attribute declaration of a catalog component's inline DTD (<see cref="GrammarDeclaration"/>), as
    /// structured data: name, type, enumeration tokens, default kind and the <b>raw</b> default literal (the exact
    /// text between the quotes, emitted verbatim on write and decoded on demand for the schema view). Instances are
    /// created through validated factory methods only, so a constructed grammar can never hold a contradictory
    /// state (the XML 1.0 validity constraints the model enforces are corpus-verified to hold across every vendor
    /// catalog file). Value semantics: two attrs with equal content are equal and hash equal.
    /// </summary>
    public sealed class GrammarAttr : IEquatable<GrammarAttr>
    {
        /// <summary>The attribute name (a validated XML Name).</summary>
        public string Name { get; }

        /// <summary>The DTD attribute type.</summary>
        public GrammarAttrType Type { get; }

        /// <summary>The enumeration's NMTOKEN values, in declared order — non-empty exactly when
        /// <see cref="Type"/> is <see cref="GrammarAttrType.Enumerated"/>.</summary>
        public ImmutableArray<string> EnumTokens { get; }

        /// <summary>How the default is declared.</summary>
        public GrammarDefault Default { get; }

        /// <summary>The raw default literal (text between the quotes, entities un-decoded) — present exactly when
        /// <see cref="Default"/> is <see cref="GrammarDefault.Literal"/>.</summary>
        public string? RawLiteral { get; }

        /// <summary>The default literal with entities/character references decoded — the logical value the schema
        /// view compares and materializes. Empty for <c>#REQUIRED</c>/<c>#IMPLIED</c>.</summary>
        internal string DecodedLiteral => RawLiteral is null ? string.Empty : XmlText.Unescape(RawLiteral);

        private GrammarAttr(string name, GrammarAttrType type, ImmutableArray<string> enumTokens,
            GrammarDefault @default, string? rawLiteral)
        {
            Name = name;
            Type = type;
            EnumTokens = enumTokens;
            Default = @default;
            RawLiteral = rawLiteral;
        }

        /// <summary>An <c>ID</c> attribute — <c>#REQUIRED</c> by default (the corpus shape); XML forbids a literal
        /// default on an ID attribute (VC: ID Attribute Default), so only required/implied are representable.</summary>
        public static GrammarAttr Id(string name, bool required = true) =>
            Create(name, GrammarAttrType.Id, ImmutableArray<string>.Empty,
                   required ? GrammarDefault.Required : GrammarDefault.Implied, rawLiteral: null);

        /// <summary>An <c>IDREF #IMPLIED</c> attribute (the corpus shape for every catalog IDREF).</summary>
        public static GrammarAttr IdRef(string name) =>
            Create(name, GrammarAttrType.IdRef, ImmutableArray<string>.Empty, GrammarDefault.Implied, rawLiteral: null);

        /// <summary>An <c>IDREF #REQUIRED</c> attribute.</summary>
        public static GrammarAttr IdRefRequired(string name) =>
            Create(name, GrammarAttrType.IdRef, ImmutableArray<string>.Empty, GrammarDefault.Required, rawLiteral: null);

        /// <summary>A <c>CDATA</c> attribute with a default literal (<paramref name="defaultRawLiteral"/> is the raw
        /// text between the quotes — entity references stay verbatim).</summary>
        public static GrammarAttr Cdata(string name, string defaultRawLiteral) =>
            Create(name, GrammarAttrType.Cdata, ImmutableArray<string>.Empty, GrammarDefault.Literal,
                   defaultRawLiteral ?? throw new ArgumentNullException(nameof(defaultRawLiteral)));

        /// <summary>A <c>CDATA #REQUIRED</c> attribute.</summary>
        public static GrammarAttr CdataRequired(string name) =>
            Create(name, GrammarAttrType.Cdata, ImmutableArray<string>.Empty, GrammarDefault.Required, rawLiteral: null);

        /// <summary>A <c>CDATA #IMPLIED</c> attribute.</summary>
        public static GrammarAttr CdataImplied(string name) =>
            Create(name, GrammarAttrType.Cdata, ImmutableArray<string>.Empty, GrammarDefault.Implied, rawLiteral: null);

        /// <summary>An enumerated attribute with a default literal that must match one of
        /// <paramref name="tokens"/> by decoded value.</summary>
        public static GrammarAttr Enumerated(string name, IEnumerable<string> tokens, string defaultRawLiteral) =>
            Create(name, GrammarAttrType.Enumerated, ToTokens(tokens), GrammarDefault.Literal,
                   defaultRawLiteral ?? throw new ArgumentNullException(nameof(defaultRawLiteral)));

        /// <summary>An enumerated <c>#REQUIRED</c> attribute.</summary>
        public static GrammarAttr EnumeratedRequired(string name, IEnumerable<string> tokens) =>
            Create(name, GrammarAttrType.Enumerated, ToTokens(tokens), GrammarDefault.Required, rawLiteral: null);

        private static ImmutableArray<string> ToTokens(IEnumerable<string> tokens)
        {
            ArgumentNullException.ThrowIfNull(tokens);
            return tokens.ToImmutableArray();
        }

        // The single validated construction path (the public factories are curated shapes over it; the DTD parser
        // and generated catalog code use it directly). Enforces the full invariant list of the plan:
        // XML-Name attribute names; enumeration tokens validated as NMTOKENS (an enumeration token may legally
        // begin with a digit — XML 1.0 VC: Enumeration — so Name validation would over-restrict) and unique within
        // the enumeration (VC: No Duplicate Tokens); tokens non-empty iff Enumerated; RawLiteral present iff the
        // Literal kind; an ID default must be #REQUIRED/#IMPLIED (VC: ID Attribute Default); an Enumerated literal
        // default must match a token by DECODED value; a literal must not contain '<', '"' or a bare '&' (it could
        // not be re-emitted between double quotes as well-formed XML).
        internal static GrammarAttr Create(string name, GrammarAttrType type, ImmutableArray<string> enumTokens,
            GrammarDefault @default, string? rawLiteral)
        {
            ArgumentNullException.ThrowIfNull(name);
            VerifyXmlName(name, $"attribute name '{name}'");
            ImmutableArray<string> tokens = enumTokens.IsDefault ? ImmutableArray<string>.Empty : enumTokens;

            if (type == GrammarAttrType.Enumerated)
            {
                if (tokens.IsEmpty)
                {
                    throw new ArgumentException($"Enumerated attribute '{name}' must declare at least one token.");
                }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string token in tokens)
                {
                    try
                    {
                        XmlConvert.VerifyNMTOKEN(token);
                    }
                    catch (XmlException ex)
                    {
                        throw new ArgumentException(
                            $"Enumeration token '{token}' of attribute '{name}' is not a valid XML NMTOKEN.", ex);
                    }
                    if (!seen.Add(token))
                    {
                        throw new ArgumentException(
                            $"Enumeration of attribute '{name}' declares token '{token}' twice (VC: No Duplicate Tokens).");
                    }
                }
            }
            else if (!tokens.IsEmpty)
            {
                throw new ArgumentException($"Attribute '{name}' is not enumerated but carries enumeration tokens.");
            }

            if (@default == GrammarDefault.Literal != rawLiteral is not null)
            {
                throw new ArgumentException(
                    $"Attribute '{name}': a raw default literal must be present exactly when the default kind is Literal.");
            }
            if (type == GrammarAttrType.Id && @default == GrammarDefault.Literal)
            {
                throw new ArgumentException(
                    $"ID attribute '{name}' cannot have a literal default (XML VC: ID Attribute Default).");
            }
            if (rawLiteral is not null)
            {
                if (rawLiteral.IndexOf('<') >= 0 || rawLiteral.IndexOf('"') >= 0)
                {
                    throw new ArgumentException(
                        $"Default literal of attribute '{name}' contains '<' or '\"', which cannot be re-emitted " +
                        "between double quotes as well-formed XML.");
                }
                if (!XmlText.HasOnlyWellFormedReferences(rawLiteral))
                {
                    throw new ArgumentException(
                        $"Default literal of attribute '{name}' contains a bare '&' (not a predefined entity or " +
                        "character reference), which cannot be re-emitted as well-formed XML.");
                }
                if (type == GrammarAttrType.Enumerated && !tokens.Contains(XmlText.Unescape(rawLiteral), StringComparer.Ordinal))
                {
                    throw new ArgumentException(
                        $"Default literal '{rawLiteral}' of enumerated attribute '{name}' does not match any of its " +
                        "tokens by decoded value.");
                }
            }
            return new GrammarAttr(name, type, tokens, @default, rawLiteral);
        }

        internal static void VerifyXmlName(string name, string what)
        {
            try
            {
                XmlConvert.VerifyName(name);
            }
            catch (XmlException ex)
            {
                throw new ArgumentException($"{what} is not a valid XML Name.", ex);
            }
        }

        public bool Equals(GrammarAttr? other) =>
            other is not null
            && Name == other.Name
            && Type == other.Type
            && Default == other.Default
            && RawLiteral == other.RawLiteral
            && ImmutableArrayValue.Equal(EnumTokens, other.EnumTokens);

        public override bool Equals(object? obj) => Equals(obj as GrammarAttr);

        public override int GetHashCode() =>
            HashCode.Combine(Name, Type, Default, RawLiteral, ImmutableArrayValue.Hash(EnumTokens));

        public override string ToString() =>
            $"GrammarAttr({Name} {Type}{(EnumTokens.IsEmpty ? "" : "(" + string.Join("|", EnumTokens) + ")")} " +
            $"{(Default == GrammarDefault.Literal ? "\"" + RawLiteral + "\"" : Default == GrammarDefault.Required ? "#REQUIRED" : "#IMPLIED")})";
    }
}
