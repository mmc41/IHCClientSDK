#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using Ihc.Vis.Catalog;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Validation;

namespace Ihc.App
{
    /// <summary>
    /// The composition root's adapter from the catalog to the validation engine's narrow library port (D27).
    ///
    /// <para><b>It exists so the validation layer never names the catalog.</b> The engine declares
    /// <see cref="ILibraryBlockSource"/> in terms of <see cref="ProjectElement"/> alone; the catalog lives here,
    /// where <c>ProjectAppService</c> already holds one, so the ruling that "the validation context may carry the
    /// catalog" is satisfied without widening the engine's dependency set. The L1-L5 layer rules
    /// (<c>ARCHITECTURE.md</c>, challenge 5) are untouched, and the architecture tests still hold.</para>
    ///
    /// <para><b>Lazy on purpose, and for the same reason the service's catalog is:</b> materializing the built-in
    /// catalog is ~173 components, and a validation run that reaches no locked library block must not pay for it.
    /// The index is built on first lookup and kept, because a project with fifty locked blocks would otherwise scan
    /// the definition list fifty times.</para>
    /// </summary>
    /// <param name="definitions">The catalog's function-block definitions, resolved lazily.</param>
    internal sealed class CatalogLibraryBlockSource(Func<IReadOnlyList<FunctionBlockDefinition>> definitions)
        : ILibraryBlockSource
    {
        private readonly Lazy<Dictionary<(string Type, string Version), ProjectElement>> index =
            new(() => Build(definitions()));

        /// <inheritdoc/>
        public bool TryGetBody(string masterType, string masterVersion, out ProjectElement body)
        {
            ArgumentNullException.ThrowIfNull(masterType);
            ArgumentNullException.ThrowIfNull(masterVersion);
            return index.Value.TryGetValue((masterType, masterVersion), out body!);
        }

        /// <summary>
        /// The definitions keyed by the identity a PLACED block carries. First entry wins for a duplicate key, the
        /// same convention the id and topology analyses use, so a catalog holding two variants of one identity
        /// resolves deterministically rather than by dictionary order.
        /// </summary>
        private static Dictionary<(string, string), ProjectElement> Build(
            IReadOnlyList<FunctionBlockDefinition> definitions)
        {
            Dictionary<(string, string), ProjectElement> map = [];
            foreach (FunctionBlockDefinition definition in definitions)
            {
                map.TryAdd((definition.MasterType, definition.MasterVersion), definition.Body);
            }

            return map;
        }
    }
}
