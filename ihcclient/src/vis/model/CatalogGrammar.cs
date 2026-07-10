#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// The structured grammar of one catalog component file (<c>.def</c>/<c>.ifb</c>): the header's prolog datum
    /// (<see cref="DeclaredEncoding"/>), DOCTYPE root, and the <b>ordered</b> per-tag declaration records that
    /// reproduce the inline DTD exactly under the whitespace-normalized fidelity relation. This is the primary
    /// representation (decision D1): <c>CatalogReader</c> parses file DTDs into it, builders construct it, the
    /// writer renders header text from it, and insert-time default materialization / IDREF detection read it
    /// through the schema view — one representation, one emitter, no verbatim vendor text.
    /// </summary>
    /// <remarks>
    /// <para><b>Two mutually consistent states.</b> <see cref="VerbatimHead"/> is <c>null</c> for every builder-
    /// or codegen-constructed grammar and for every read file whose header fits the corpus envelope: the
    /// declarations are then the single source for both serialization and semantics. When the lenient parser meets
    /// an exotic <em>user</em> file whose header uses a construct outside the model (a DTD comment, an entity,
    /// a non-<c>ANY</c> content model, …), it keeps the whole header text here so the writer re-emits it
    /// byte-faithfully, while <see cref="Declarations"/> still carries the best-effort structured projection of
    /// every individually parseable declaration — so defaults, IDREF re-stamping and open-world hoisting keep
    /// working for such files instead of silently degrading.</para>
    /// <para><b>Value semantics</b> including the array members — a precondition for the generated catalog's
    /// grammar interning/deduplication.</para>
    /// </remarks>
    public sealed class CatalogGrammar : IEquatable<CatalogGrammar>
    {
        /// <summary>The encoding label every vendor catalog file declares (independently of its physical
        /// <see cref="CatalogTextEncoding"/> — the corpus's <c>.def</c> files are physically UTF-8-BOM while
        /// declaring this).</summary>
        public const string DefaultDeclaredEncoding = "ISO-8859-1";

        /// <summary>The grammar of a definition authored without any DTD: no declarations, no fallback. A
        /// definition carrying it can be built and inserted (registry-resolved) but not written to a catalog file.</summary>
        public static readonly CatalogGrammar Empty =
            new(ImmutableArray<GrammarDeclaration>.Empty, DefaultDeclaredEncoding, doctypeRoot: null, verbatimHead: null);

        /// <summary>The encoding label of the XML prolog — header <b>data</b> (what the file says), deliberately
        /// independent of the physical byte encoding the writer uses (<see cref="CatalogTextEncoding"/>).</summary>
        public string DeclaredEncoding { get; }

        /// <summary>The DOCTYPE root name, or <c>null</c> to default from the body root tag at write time. An
        /// explicit value must equal the body root when written (corpus: always equal).</summary>
        public string? DoctypeRoot { get; }

        /// <summary>The ordered per-tag declaration records; tags are unique (ordinal).</summary>
        public ImmutableArray<GrammarDeclaration> Declarations { get; }

        /// <summary>The lenient parser's byte-faithful fallback for an out-of-envelope user header — see the class
        /// remarks for the two-state semantics. Never set by builders or generated code.</summary>
        internal string? VerbatimHead { get; }

        /// <summary>True when the grammar carries nothing to serialize a header from — no declarations and no
        /// verbatim fallback (the <see cref="Empty"/> state, possibly with different prolog data).</summary>
        public bool IsEmpty => Declarations.IsEmpty && VerbatimHead is null;

        private CatalogGrammar(ImmutableArray<GrammarDeclaration> declarations, string declaredEncoding,
            string? doctypeRoot, string? verbatimHead)
        {
            Declarations = declarations;
            DeclaredEncoding = declaredEncoding;
            DoctypeRoot = doctypeRoot;
            VerbatimHead = verbatimHead;
        }

        /// <summary>Creates a grammar from ordered declarations with the vendor-default prolog
        /// (<c>ISO-8859-1</c>) and a DOCTYPE root defaulted from the body root tag at write time.</summary>
        public static CatalogGrammar Create(IEnumerable<GrammarDeclaration> declarations) =>
            Create(declarations, DefaultDeclaredEncoding, doctypeRoot: null);

        /// <summary>Creates a grammar from ordered declarations, a prolog encoding label and an optional explicit
        /// DOCTYPE root.</summary>
        public static CatalogGrammar Create(IEnumerable<GrammarDeclaration> declarations, string declaredEncoding,
            string? doctypeRoot = null)
        {
            ArgumentNullException.ThrowIfNull(declarations);
            return Build(declarations.ToImmutableArray(), declaredEncoding, doctypeRoot, verbatimHead: null);
        }

        // The lenient parser's fallback construction — the only path that sets VerbatimHead (builders and
        // generated code never do).
        internal static CatalogGrammar CreateWithVerbatimHead(string verbatimHead,
            ImmutableArray<GrammarDeclaration> projectedDeclarations, string declaredEncoding, string? doctypeRoot)
        {
            ArgumentNullException.ThrowIfNull(verbatimHead);
            return Build(projectedDeclarations, declaredEncoding, doctypeRoot, verbatimHead);
        }

        private static readonly Regex EncodingLabel = new("^[A-Za-z][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant);

        private static CatalogGrammar Build(ImmutableArray<GrammarDeclaration> declarations, string declaredEncoding,
            string? doctypeRoot, string? verbatimHead)
        {
            ArgumentNullException.ThrowIfNull(declaredEncoding);
            if (!EncodingLabel.IsMatch(declaredEncoding))
            {
                throw new ArgumentException(
                    $"'{declaredEncoding}' is not a valid XML encoding label (it would render a malformed prolog).");
            }
            if (doctypeRoot is not null)
            {
                GrammarAttr.VerifyXmlName(doctypeRoot, $"DOCTYPE root '{doctypeRoot}'");
            }
            ImmutableArray<GrammarDeclaration> list =
                declarations.IsDefault ? ImmutableArray<GrammarDeclaration>.Empty : declarations;
            var tags = new HashSet<string>(StringComparer.Ordinal);
            foreach (GrammarDeclaration declaration in list)
            {
                if (declaration is null)
                {
                    throw new ArgumentException("Grammar contains a null declaration.");
                }
                if (!tags.Add(declaration.Tag))
                {
                    // A duplicate declaration for one tag is a DTD validity error the non-validating
                    // well-formedness gate cannot catch, so the model rejects it here.
                    throw new ArgumentException($"Grammar declares tag '{declaration.Tag}' twice.");
                }
            }
            return new CatalogGrammar(list, declaredEncoding, doctypeRoot, verbatimHead);
        }

        /// <summary>The declaration record for <paramref name="tag"/> (ordinal), full or orphan, or <c>null</c>.</summary>
        public GrammarDeclaration? TryGetDeclaration(string tag)
        {
            foreach (GrammarDeclaration declaration in Declarations)
            {
                if (declaration.Tag == tag)
                {
                    return declaration;
                }
            }
            return null;
        }

        public bool Equals(CatalogGrammar? other) =>
            other is not null
            && DeclaredEncoding == other.DeclaredEncoding
            && DoctypeRoot == other.DoctypeRoot
            && VerbatimHead == other.VerbatimHead
            && ImmutableArrayValue.Equal(Declarations, other.Declarations);

        public override bool Equals(object? obj) => Equals(obj as CatalogGrammar);

        public override int GetHashCode() =>
            HashCode.Combine(DeclaredEncoding, DoctypeRoot, VerbatimHead, ImmutableArrayValue.Hash(Declarations));

        public override string ToString() =>
            $"CatalogGrammar({Declarations.Length} declarations, encoding={DeclaredEncoding}" +
            $"{(DoctypeRoot is null ? "" : ", root=" + DoctypeRoot)}{(VerbatimHead is null ? "" : ", verbatim fallback")})";
    }
}
