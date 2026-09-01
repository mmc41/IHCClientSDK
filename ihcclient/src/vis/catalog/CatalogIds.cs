using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Re-stamps a code-authored component body with its source file's <b>exact</b> id tokens (D1). A builder
    /// allocates placeholder ids; the acceptance compares ids strictly and vendor <c>.ifb</c> ids are non-contiguous
    /// (<c>_0x5128, _0x5223…</c>), so they cannot be re-derived — the generator captures the file's document-order id
    /// tokens and the generated factory calls
    /// <see cref="StampDocumentOrder(ProjectElement, IReadOnlyList{string}, CatalogGrammar)"/> to replace the builder
    /// ids with them and remap every schema-declared IDREF through the same old→new map. Hand-authored builders
    /// (no generator) never call this and keep auto-allocation.
    /// </summary>
    internal static class CatalogIds
    {
        /// <summary>The id tokens of every id-bearing element (a parseable <c>_0x</c> token) in document (pre-order)
        /// sequence — the list the generator bakes so
        /// <see cref="StampDocumentOrder(ProjectElement, IReadOnlyList{string}, CatalogGrammar)"/> can re-apply them.
        /// Elements whose id token does not parse (e.g. the vendor typo <c>_05</c>) are excluded; the writer emits
        /// their verbatim baked token unchanged.</summary>
        public static ImmutableArray<string> ExtractDocumentOrderIds(ProjectElement body)
        {
            ArgumentNullException.ThrowIfNull(body);
            var tokens = ImmutableArray.CreateBuilder<string>();
            foreach (ProjectElement e in body.DescendantsAndSelf())
            {
                if (e.GetAttribute("id") is { } token && e.Id is not null)
                {
                    tokens.Add(token);
                }
            }
            return tokens.ToImmutable();
        }

        /// <summary>
        /// Returns a copy of <paramref name="body"/> whose id-bearing elements carry <paramref name="idTokens"/> in
        /// document order (one token per parseable-id element, exactly as <see cref="ExtractDocumentOrderIds"/> lists
        /// them), with every schema-declared IDREF remapped through the old→new map. <paramref name="grammar"/>
        /// supplies the component's own grammar (registry fallback) for IDREF detection of custom element types.
        /// </summary>
        public static ProjectElement StampDocumentOrder(ProjectElement body, IReadOnlyList<string> idTokens,
            CatalogGrammar grammar)
        {
            ArgumentNullException.ThrowIfNull(body);
            ArgumentNullException.ThrowIfNull(idTokens);

            int assign = 0;
            Dictionary<string, string> map = BuildIdMap(body, _ => idTokens[assign++]);
            if (assign != idTokens.Count)
            {
                throw new InvalidOperationException(
                    $"Baked id-token count ({idTokens.Count}) does not match the built body's id-bearing element count " +
                    $"({assign}); the catalog definition factory is out of sync with the body it builds.");
            }

            int rewrite = 0;
            return RewriteIds(body, ProjectSchemaView.For(grammar), _ => idTokens[rewrite++], map);
        }

        // ---- the shared two-pass document-order id restamp (this class and DefinitionNormalizer.Renumber) ----
        // Pass 1 (BuildIdMap) visits id-bearing elements pre-order, building the old→new token map (last-wins for a
        // duplicated source token; IDREFs always target a unique id). Pass 2 (RewriteIds) walks the same order, giving
        // each element its OWN fresh token — duplicate source ids therefore become distinct, exactly as on insert —
        // and remaps every schema-declared IDREF through the map. The token supplier is stateful; callers pass a
        // fresh one per pass so element N receives the same token in both passes.

        internal static Dictionary<string, string> BuildIdMap(ProjectElement root, Func<ProjectElement, string> nextToken)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ProjectElement element in root.DescendantsAndSelf())
            {
                if (element.GetAttribute("id") is { } oldToken && element.Id is not null)
                {
                    map[oldToken] = nextToken(element);
                }
            }
            return map;
        }

        internal static ProjectElement RewriteIds(ProjectElement element, ProjectSchemaView view,
            Func<ProjectElement, string> nextToken, Dictionary<string, string> idRefMap)
        {
            ElementSchema? schema = view.TryGet(element.Tag);
            ElementId? newId = element.Id;
            string? newToken = null;
            if (element.GetAttribute("id") is not null && element.Id is not null)
            {
                newToken = nextToken(element);
                newId = ElementId.TryParse(newToken, out ElementId parsed) ? parsed : element.Id;
            }

            var attrs = ImmutableArray.CreateBuilder<(string, string)>();
            foreach ((string name, string value) in element.Attrs)
            {
                if (name == "id" && newToken is not null)
                {
                    attrs.Add(("id", newToken));
                }
                else if (schema is not null && schema.IsIdRef(name) && idRefMap.TryGetValue(value, out string? target))
                {
                    attrs.Add((name, target));
                }
                else
                {
                    attrs.Add((name, value));
                }
            }

            ImmutableArray<ProjectElement> children = element.Children
                .Select(c => RewriteIds(c, view, nextToken, idRefMap))
                .ToImmutableArray();
            return new ProjectElement(element.Tag, newId, attrs.ToImmutable(), children);
        }
    }
}
