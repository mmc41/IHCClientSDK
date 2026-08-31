using System;
using System.Linq;
using System.Reflection;

namespace Ihc.Tests
{
    /// <summary>
    /// The coverage collector's STATIC instrumentation, which rewrites the product assembly a rule then reads.
    ///
    /// <para>Whether a run sees it at all depends on the mode the collector picks for the platform — static on
    /// macOS, dynamic on Windows, which rewrites nothing — so a rule over a product assembly has to tolerate it
    /// BY RULE rather than by having happened to see it. A rule that does not is green on the platform it was
    /// written on and broken on the other, which is the failure mode this type exists to name once.</para>
    ///
    /// <para>Two rules depend on this: the GUI scan scope, which must not read the injected TYPES as unauthored
    /// GUI code, and the task-discard gate, which cannot read a rewritten body at all. They have to agree on what
    /// "injected" means, which is why the root lives here rather than in either of them.</para>
    /// </summary>
    internal static class CoverageInstrumentation
    {
        /// <summary>The namespace root everything injected lands under. Matched by ROOT rather than by name
        /// because the collector appends a per-build GUID to the leaf, so no exact name survives the next build.
        /// <para>Pinned by <c>OpenVisualArchitectureTests.GuiScanScope_ToleratesTheCoverageInstrumentationTracker</c>,
        /// which asserts the measured type names against it on every platform — including the ones whose own runs
        /// never produce them.</para></summary>
        internal const string Root = "Microsoft.CodeCoverage.Instrumentation.";

        /// <summary>True when the name is a type the collector injected.</summary>
        internal static bool IsInjectedTypeName(string name) => name.StartsWith(Root, StringComparison.Ordinal);

        /// <summary>
        /// True when this run's collector rewrote <paramref name="assembly"/> — so its IL is no longer the IL
        /// anyone wrote, and a rule that reads instruction ADJACENCY cannot be answered from it.
        /// </summary>
        /// <remarks>The injected tracker is the evidence: static instrumentation adds it to every module it
        /// rewrites, and dynamic instrumentation adds nothing at all, so its presence is exactly the question
        /// "was this body rewritten".</remarks>
        internal static bool Rewrote(Assembly assembly) =>
            assembly.GetTypes().Any(t => t.FullName is { } name && IsInjectedTypeName(name));
    }
}
