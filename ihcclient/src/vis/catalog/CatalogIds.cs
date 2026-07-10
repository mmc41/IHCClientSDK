#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

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
            Collect(body, tokens);
            return tokens.ToImmutable();
        }

        private static void Collect(ProjectElement element, ImmutableArray<string>.Builder tokens)
        {
            if (element.GetAttribute("id") is { } token && element.Id is not null)
            {
                tokens.Add(token);
            }
            foreach (ProjectElement child in element.ChildrenOrEmpty())
            {
                Collect(child, tokens);
            }
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

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            int assign = 0;
            AssignIds(body, idTokens, ref assign, map);
            if (assign != idTokens.Count)
            {
                throw new InvalidOperationException(
                    $"Baked id-token count ({idTokens.Count}) does not match the built body's id-bearing element count " +
                    $"({assign}); the generated factory is out of sync with its source file. Regenerate the catalog.");
            }

            ProjectSchemaView view = ProjectSchemaView.For(grammar);
            int rewrite = 0;
            return Rewrite(body, idTokens, ref rewrite, map, view);
        }


        private static void AssignIds(ProjectElement element, IReadOnlyList<string> tokens, ref int idx,
            Dictionary<string, string> map)
        {
            if (element.GetAttribute("id") is { } oldToken && element.Id is not null)
            {
                map[oldToken] = tokens[idx++];   // last-wins for a duplicated token; IDREFs target a unique id
            }
            foreach (ProjectElement child in element.ChildrenOrEmpty())
            {
                AssignIds(child, tokens, ref idx, map);
            }
        }

        private static ProjectElement Rewrite(ProjectElement element, IReadOnlyList<string> tokens, ref int idx,
            Dictionary<string, string> map, ProjectSchemaView view)
        {
            ElementSchema? schema = view.TryGet(element.Tag);
            ElementId? newId = element.Id;
            string? newToken = null;
            if (element.GetAttribute("id") is not null && element.Id is not null)
            {
                newToken = tokens[idx++];
                newId = ElementId.TryParse(newToken, out ElementId parsed) ? parsed : element.Id;
            }

            var attrs = ImmutableArray.CreateBuilder<(string, string)>();
            foreach ((string name, string value) in element.AttrsOrEmpty())
            {
                if (name == "id" && newToken is not null)
                {
                    attrs.Add(("id", newToken));
                }
                else if (schema is not null && schema.IsIdRef(name) && map.TryGetValue(value, out string? target))
                {
                    attrs.Add((name, target));
                }
                else
                {
                    attrs.Add((name, value));
                }
            }

            var children = ImmutableArray.CreateBuilder<ProjectElement>();
            foreach (ProjectElement child in element.ChildrenOrEmpty())
            {
                children.Add(Rewrite(child, tokens, ref idx, map, view));
            }
            return new ProjectElement(element.Tag, newId, attrs.ToImmutable(), children.ToImmutable());
        }
    }
}
