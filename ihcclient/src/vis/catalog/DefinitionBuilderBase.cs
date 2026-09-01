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
        // The definition's root/body attributes in authored order (M7): the shared list both concrete builders bake
        // their fluent setters (Note/Locked/…) and the raw Attribute escape hatch through; each emits it from its own
        // ComposeRoot/identity path.
        private protected readonly List<(string Name, string Value)> rootAttrs = new();
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

        // ---- root/body attributes + the build-tail (shared plumbing, M7) ----

        /// <summary>Bakes a raw definition-level attribute verbatim (the open-world escape hatch shared by both
        /// builders); canonicalization still normalizes order and drops default-valued attributes on insert.</summary>
        public TSelf Attribute(string name, string value) => SetRoot(name, value);

        // Records a root attribute in authored order — the single seam every concrete fluent setter (Note/Locked/…)
        // and the Attribute escape hatch route through, so the ordered-append rule lives once.
        private protected TSelf SetRoot(string name, string value)
        {
            rootAttrs.Add((name, value));
            return Self;
        }

        /// <summary>Attaches the definition-level documentation summary — <b>programmatic-lookup-only</b> metadata
        /// (surfaces on the definition's <c>Documentation</c>); it is never serialized into the body or a
        /// <c>.def</c>/<c>.ifb</c>. Contrast the builders' <c>Note</c> methods, which set the serialized <c>note</c>
        /// attribute: <c>Note</c> is project data, <c>Documentation</c> is help.</summary>
        public TSelf Documentation(string documentation)
        {
            docSummary = documentation;
            return Self;
        }

        // Per-resource help has exactly ONE authoring door: the resource itself — Documentation on the configurator
        // passed to AddInput/AddOutput/AddSetting/AddInternalVariable/AddResource, or the product builder's
        // RawChild(child, documentation) for a resource spliced in through the raw-subtree escape hatch.
        // The retired name-keyed Documentation(resourceName, text) overload repeated the name as a string key, so a
        // typo bound the text to nothing and failed silently; keying off the resource being added makes that
        // impossible by construction.

        // Records a per-resource doc under the position key ResourceDocKey minted for the resource being added — the
        // seam every concrete builder's resource-add path routes through. A position is occupied by exactly one
        // resource, so a second write to the same key is never a legitimate re-documentation: it means a builder
        // computed the key wrong (typically an offset it failed to seed), which would otherwise silently discard one
        // pin's help text — the very failure per-position keying exists to remove. Throw instead.
        private protected void SetResourceDoc(string key, string documentation)
        {
            if (!resourceDocs.TryAdd(key, documentation))
            {
                throw new InvalidOperationException(
                    $"Two resources were documented under the same position key '{key}'. A position identifies one " +
                    "resource, so this is a key-minting bug in the builder, not a duplicate authoring call.");
            }
        }

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
