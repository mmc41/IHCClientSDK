#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Ihc.Vis.Io;
using Ihc.Vis.Model;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// T034 (S3): the shared authoring core of the two definition builders —
    /// <see cref="Ihc.Vis.Products.ProductDefinitionBuilder"/> and
    /// <see cref="Ihc.Vis.FunctionBlocks.FunctionBlockDefinitionBuilder"/>. A CRTP base whose
    /// <typeparamref name="TSelf"/> is the concrete builder, so every shared fluent verb returns the concrete type and
    /// chaining is preserved. Holds the id allocator, the library category path, the effective grammar, the physical
    /// source encoding, and the programmatic-lookup-only documentation (summary + per-resource text). The concrete
    /// builders (in the same assembly) read the <c>private protected</c> state directly and add their own
    /// definition-specific authoring surface (resources, program graph, root attributes, Build).
    /// </summary>
    public abstract class DefinitionBuilderBase<TSelf> where TSelf : DefinitionBuilderBase<TSelf>
    {
        private protected IdAllocator ids = new(0);
        private protected string categoryPath = string.Empty;
        private protected CatalogGrammar grammar;
        private protected CatalogTextEncoding? sourceEncoding;   // From-carried physical encoding
        private string? docSummary;
        private readonly Dictionary<string, string> resourceDocs = new(StringComparer.Ordinal);

        private protected DefinitionBuilderBase(CatalogGrammar initialGrammar) => grammar = initialGrammar;

        /// <summary>The concrete builder instance, for fluent chaining (the CRTP self-type).</summary>
        private protected abstract TSelf Self { get; }

        /// <summary>Sets the library category path the definition is filed under.</summary>
        public TSelf CategoryPath(string categoryPath)
        {
            this.categoryPath = categoryPath;
            return Self;
        }

        /// <summary><b>Replaces</b> the effective grammar wholesale — the canonical assignment generated catalog code
        /// uses and the full replacement after <see cref="Ihc.Vis.Products.ProductDefinitionBuilder.From"/>/
        /// <see cref="Ihc.Vis.FunctionBlocks.FunctionBlockDefinitionBuilder.From"/>. To adjust a single declaration
        /// while keeping the preset/carried grammar intact, use <see cref="ExtendGrammar"/>.</summary>
        public TSelf Grammar(CatalogGrammar grammar)
        {
            ArgumentNullException.ThrowIfNull(grammar);
            this.grammar = grammar;
            return Self;
        }

        /// <summary><b>Extends</b> the effective grammar (the family preset, a From-carried grammar, or a prior
        /// assignment): the callback add-or-replaces whole per-tag declarations, leaving every other declaration,
        /// default and IDREF classification intact — the near-minimal path for declaring one custom body type.</summary>
        public TSelf ExtendGrammar(Action<CatalogGrammarBuilder> extend)
        {
            ArgumentNullException.ThrowIfNull(extend);
            var builder = new CatalogGrammarBuilder(grammar);
            extend(builder);
            grammar = builder.Build();
            return Self;
        }

        /// <summary>Attaches the definition-level documentation summary — <b>programmatic-lookup-only</b> metadata
        /// (surfaces on the definition's <c>Documentation</c>); it is never serialized into the body or a
        /// <c>.def</c>/<c>.ifb</c>. Contrast the builders' <c>Note</c> methods, which set the serialized attribute.</summary>
        public TSelf Documentation(string documentation)
        {
            docSummary = documentation;
            return Self;
        }

        /// <summary>Attaches documentation text to a resource identified by its display <paramref name="resourceName"/>
        /// (the key <see cref="DefinitionDocumentation.ForResource"/> looks it up by) — the name-keyed overload, for a
        /// caller with help text keyed by pin name. Programmatic-lookup-only; never serialized.</summary>
        public TSelf Documentation(string resourceName, string documentation)
        {
            ArgumentNullException.ThrowIfNull(resourceName);
            resourceDocs[resourceName] = documentation;
            return Self;
        }

        // Records a per-resource doc by name — the seam a concrete builder's by-handle Documentation overload uses.
        private protected void SetResourceDoc(string resourceName, string documentation) =>
            resourceDocs[resourceName] = documentation;

        // Seeds summary + per-resource docs from an existing definition (the From(...) round-trip path).
        private protected void SeedDocumentation(DefinitionDocumentation documentation)
        {
            docSummary = documentation.Summary;
            foreach (KeyValuePair<string, string> doc in documentation.Resources)
            {
                resourceDocs[doc.Key] = doc.Value;
            }
        }

        // The materialized documentation record — empty when nothing was attached.
        private protected DefinitionDocumentation BuildDocumentation() =>
            docSummary is null && resourceDocs.Count == 0
                ? DefinitionDocumentation.Empty
                : new DefinitionDocumentation(docSummary, resourceDocs.ToImmutableDictionary(StringComparer.Ordinal));
    }
}
