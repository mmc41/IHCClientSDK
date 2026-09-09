using System.Collections.Generic;
using System.Collections.Immutable;
using Ihc.Vis.Catalog;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The empty <see cref="ICatalog"/> these fixtures build a service on, and the few variations that need a row
    /// or a template to answer with.
    /// <para>
    /// A factory over the SDK's own <see cref="MaterializedCatalog"/> rather than a second implementation of the
    /// interface: that type IS the source-agnostic in-memory catalog, its misses already throw
    /// <see cref="KeyNotFoundException"/>, and a hand-written double would be a parallel definition of the same
    /// behaviour that could drift from it. What the suite needed was never a different catalog — only a convenient
    /// way to ask for an empty one, which a positional five-argument constructor does not give.
    /// </para>
    /// <para>
    /// It is written by hand rather than mocked because <see cref="ICatalog"/> is INTERNAL to the SDK: a proxy
    /// generator cannot subclass an internal interface without the SDK granting its dynamic assembly
    /// <c>InternalsVisibleTo</c>, and the friend list is deliberately closed.
    /// </para>
    /// </summary>
    internal static class StubCatalog
    {
        /// <summary>A catalog that holds nothing: empty rosters, empty templates, and a miss for every lookup.</summary>
        internal static ICatalog Empty() => With();

        /// <summary>The same catalog with whichever parts a fixture needs it to answer with.</summary>
        internal static ICatalog With(
            IEnumerable<ProductDefinition>? products = null,
            IEnumerable<FunctionBlockDefinition>? functionBlocks = null,
            ProjectElement? newProjectSkeleton = null,
            ProjectElement? builtInEnumerators = null) =>
            new MaterializedCatalog(
                products is null ? [] : [.. products],
                functionBlocks is null ? [] : [.. functionBlocks],
                newProjectSkeleton ?? ProjectElement.Create("utcs_project", null, [], []),
                builtInEnumerators ?? ProjectElement.Create("enum_definitions", null, [], []),
                new FunctionBlockDefinition("", "", "", "", "", ProjectElement.Create("functionblock", null, [], [])));
    }
}
