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
        private readonly Lazy<LibraryIndex> index = new(() => Build(definitions()));

        /// <inheritdoc/>
        public bool TryGetBody(string masterType, string masterVersion, out ProjectElement body)
        {
            ArgumentNullException.ThrowIfNull(masterType);
            ArgumentNullException.ThrowIfNull(masterVersion);
            return index.Value.Bodies.TryGetValue((masterType, masterVersion), out body!);
        }

        /// <inheritdoc/>
        public bool TryGetVersions(string masterType, out EquatableArray<string> versions)
        {
            ArgumentNullException.ThrowIfNull(masterType);
            if (index.Value.Versions.TryGetValue(masterType, out EquatableArray<string> held))
            {
                versions = held;
                return true;
            }

            // The empty list rather than default, so a caller that ignores the return value still reads a list.
            versions = EquatableArray<string>.Empty;
            return false;
        }

        /// <summary>Both readings of the same definition list, built in ONE pass over it.</summary>
        /// <param name="Bodies">Keyed by the exact identity a placed block carries.</param>
        /// <param name="Versions">Every version held per type, distinct and ordinal-ascending.</param>
        private sealed record LibraryIndex(
            Dictionary<(string Type, string Version), ProjectElement> Bodies,
            Dictionary<string, EquatableArray<string>> Versions);

        /// <summary>
        /// The definitions read twice over, once per question, from one enumeration — materializing the catalog is
        /// the expensive half and a second pass over it would double it for nothing.
        /// <para>
        /// BODIES: first entry wins for a duplicate key, the same convention the id and topology analyses use, so
        /// a catalog holding two variants of one identity resolves deterministically rather than by dictionary
        /// order. VERSIONS: sorted ordinal-ascending because the port's contract says so — the catalog's own
        /// declaration order is not a promise, and a rule binds one of these into a Danish sentence.
        /// </para>
        /// </summary>
        private static LibraryIndex Build(IReadOnlyList<FunctionBlockDefinition> definitions)
        {
            Dictionary<(string, string), ProjectElement> bodies = [];
            Dictionary<string, SortedSet<string>> versions = [];
            foreach (FunctionBlockDefinition definition in definitions)
            {
                bodies.TryAdd((definition.MasterType, definition.MasterVersion), definition.Body);
                if (!versions.TryGetValue(definition.MasterType, out SortedSet<string>? held))
                {
                    held = new SortedSet<string>(StringComparer.Ordinal);
                    versions[definition.MasterType] = held;
                }

                held.Add(definition.MasterVersion);
            }

            return new LibraryIndex(
                bodies,
                versions.ToDictionary(e => e.Key, e => (EquatableArray<string>)[.. e.Value], StringComparer.Ordinal));
        }
    }
}
