using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// The <c>.ExtendGrammar</c> callback surface: starts from an existing <b>effective</b> grammar (a family
    /// preset, a <c>From</c>-carried grammar, or a prior assignment) and add-or-replaces whole per-tag
    /// declarations — the near-minimal path for declaring one custom body type without wiping the preset's root
    /// and standard declarations (which a wholesale <c>.Grammar(...)</c> replacement would). A replaced tag keeps
    /// its position; a new tag appends. Prolog datum and DOCTYPE root carry over unchanged.
    /// </summary>
    public sealed class CatalogGrammarBuilder
    {
        private readonly List<GrammarDeclaration> declarations;
        private readonly string declaredEncoding;
        private readonly string? doctypeRoot;

        internal CatalogGrammarBuilder(CatalogGrammar start)
        {
            ArgumentNullException.ThrowIfNull(start);
            if (start.VerbatimHead is not null)
            {
                // The verbatim fallback is serialization provenance for an out-of-envelope user header; extending
                // only the structured projection would silently desynchronize what is written from what is meant.
                throw new InvalidOperationException(
                    "Cannot extend a grammar carrying an exotic-header verbatim fallback — its serialized form is " +
                    "the original header text, which structured edits cannot represent. Assign a fully structured " +
                    "grammar with .Grammar(...) instead.");
            }
            declarations = new List<GrammarDeclaration>(start.Declarations);
            declaredEncoding = start.DeclaredEncoding;
            doctypeRoot = start.DoctypeRoot;
        }

        /// <summary>Adds or replaces the full declaration for <paramref name="tag"/>:
        /// <c>&lt;!ELEMENT tag ANY&gt;</c> plus an ATTLIST with <paramref name="attrs"/>.</summary>
        public CatalogGrammarBuilder Element(string tag, params GrammarAttr[] attrs) =>
            AddOrReplace(GrammarDeclaration.Element(tag, attrs));

        /// <summary>Adds or replaces a lone <c>&lt;!ELEMENT tag ANY&gt;</c> declaration (no ATTLIST).</summary>
        public CatalogGrammarBuilder ElementOnly(string tag) =>
            AddOrReplace(GrammarDeclaration.ElementOnly(tag));

        /// <summary>Adds or replaces an orphan <c>&lt;!ATTLIST tag …&gt;</c> declaration (no element declaration
        /// of its own — the vendor "med logning" shape).</summary>
        public CatalogGrammarBuilder AttlistOnly(string tag, params GrammarAttr[] attrs) =>
            AddOrReplace(GrammarDeclaration.AttlistOnly(tag, attrs));

        private CatalogGrammarBuilder AddOrReplace(GrammarDeclaration declaration)
        {
            for (int i = 0; i < declarations.Count; i++)
            {
                if (declarations[i].Tag == declaration.Tag)
                {
                    declarations[i] = declaration;
                    return this;
                }
            }
            declarations.Add(declaration);
            return this;
        }

        internal CatalogGrammar Build() =>
            CatalogGrammar.Create(declarations.ToImmutableArray(), declaredEncoding, doctypeRoot);
    }
}
