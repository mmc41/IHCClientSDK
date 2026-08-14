#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// Lowers a <see cref="DefinitionDocumentation"/> (parsed from a sibling <c>syn_en*.md</c> by
    /// <see cref="CatalogDocReader"/>) into the builder calls that re-attach it — the ONE rule both catalog kinds bake
    /// their help text with, since both builders inherit the same authoring surface from
    /// <see cref="DefinitionBuilderBase{TSelf}"/>. Each decompiler wraps the yielded pairs in its own recorded-call
    /// type (a product <see cref="FluentCall"/>, a block <c>FbHeadCall</c>), so the executed and rendered forms stay in
    /// lock-step per side while the emitted C# can never drift between the two.
    /// </summary>
    /// <remarks>
    /// This metadata is programmatic-lookup-only: it rides on the in-memory definition and is never serialized into a
    /// <c>.def</c>/<c>.ifb</c> or a project <c>.vis</c>, so these calls cannot move a byte of the re-emitted file and
    /// the generator's byte-fidelity self-verify is indifferent to them.
    /// </remarks>
    internal static class DefinitionDocumentationCalls
    {
        /// <summary>The ordered <c>.Documentation(..)</c> calls for <paramref name="documentation"/>: the definition
        /// summary first, then one per documented resource, key-sorted so the generated source is diff-stable (the
        /// keys are the resource display names a caller reads off the definition's resource projections). Empty when
        /// there is no documentation to bake.</summary>
        public static IEnumerable<(Action<TBuilder> Apply, string Render)> For<TBuilder>(
            DefinitionDocumentation? documentation)
            where TBuilder : DefinitionBuilderBase<TBuilder>
        {
            if (documentation is null || documentation.IsEmpty)
            {
                yield break;
            }
            if (documentation.Summary is { } summary)
            {
                yield return (b => b.Documentation(summary), $".Documentation({CSharpLiteral.Quote(summary)})");
            }
            foreach (KeyValuePair<string, string> entry in
                     documentation.Resources.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                string resourceName = entry.Key;
                string text = entry.Value;
                yield return (b => b.Documentation(resourceName, text),
                    $".Documentation({CSharpLiteral.Quote(resourceName)}, {CSharpLiteral.Quote(text)})");
            }
        }
    }
}
