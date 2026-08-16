#nullable enable
using System.Collections.Immutable;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Binds a name-keyed <see cref="HelpDocument"/> (what a <c>syn_en*.md</c> sibling can express) to the resources
    /// of a parsed catalog body, producing the position-keyed <see cref="DefinitionDocumentation"/> a definition
    /// carries. The one place the two key conventions meet — see <c>ResourceDocKey</c> for why a definition cannot
    /// key help by name.
    /// </summary>
    /// <remarks>
    /// A bullet whose name matches several resources documents <b>each</b> of them: the document has one sentence to
    /// say about that name, and this resolver must not invent a reason to prefer one pin. Giving those pins different
    /// texts means authoring the definition, where each resource is addressable, not the markdown.
    /// <para>A bullet matching <b>no</b> resource is dropped. That is the whole orphan class — a help document naming
    /// a pin its <c>.def</c>/<c>.ifb</c> does not have — retired here rather than carried into the definition as a
    /// key nothing can ever read back.</para>
    /// </remarks>
    internal static class HelpDocumentResolver
    {
        /// <summary>Resolves against a product body: its resource children, at their raw body positions.</summary>
        public static DefinitionDocumentation ForProduct(HelpDocument document, ProjectElement body)
        {
            var resolved = ImmutableDictionary.CreateBuilder<string, string>(System.StringComparer.Ordinal);
            for (int index = 0; index < body.Children.Length; index++)
            {
                ProjectElement child = body.Children[index];
                if (ProductRows.IsStructuralChild(child.Tag))
                {
                    continue;
                }
                if (document.ForName(child.GetAttribute("name") ?? string.Empty) is { } text)
                {
                    resolved[ResourceDocKey.ForProduct(index)] = text;
                }
            }
            return Materialize(document, resolved);
        }

        /// <summary>Resolves against a function-block body: the children of its four resource containers.</summary>
        public static DefinitionDocumentation ForBlock(HelpDocument document, ProjectElement body)
        {
            var resolved = ImmutableDictionary.CreateBuilder<string, string>(System.StringComparer.Ordinal);
            foreach (string containerTag in FunctionBlockDefinitionBuilder.ResourceContainerTags)
            {
                if (body.FindChild(containerTag) is not { } holder)
                {
                    continue;
                }
                for (int index = 0; index < holder.Children.Length; index++)
                {
                    if (document.ForName(holder.Children[index].GetAttribute("name") ?? string.Empty) is { } text)
                    {
                        resolved[ResourceDocKey.ForBlock(containerTag, index)] = text;
                    }
                }
            }
            return Materialize(document, resolved);
        }

        private static DefinitionDocumentation Materialize(
            HelpDocument document, ImmutableDictionary<string, string>.Builder resolved) =>
            document.Summary is null && resolved.Count == 0
                ? DefinitionDocumentation.Empty
                : new DefinitionDocumentation(document.Summary, resolved.ToImmutable());
    }
}
