#nullable enable
using System.Runtime.CompilerServices;
using System.Threading;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Editing
{
    /// <summary>
    /// fablerefac W4-1 (P1b): the SDK-internal cache of a project's open edit-analysis. Opening a
    /// <see cref="ProjectEditor"/> otherwise re-walks the whole tree every time — the id high-water mark (the physical
    /// id scan + the IDREF scan of <c>IdAllocator.ForProject</c>) plus the duplicate-id and unknown-attribute
    /// guards. Because an SDK commit produces a canonical, contract-checked project whose id counter the editor already
    /// holds (<see cref="ProjectEditor.ToProject"/> writes <c>last_unique_id</c> from it, so a fresh scan would return
    /// exactly that counter), the SDK REGISTERS that analysis and the next <see cref="ProjectEditor"/> over the same
    /// instance reuses it instead of re-scanning.
    /// <para>Keyed by project INSTANCE through a <see cref="ConditionalWeakTable{TKey,TValue}"/> (the
    /// <c>ProjectSchemaView</c> precedent), so an entry vanishes when its snapshot is collected. ONLY SDK-produced
    /// (<see cref="ProjectEditor.ToProject"/>) instances are registered — a consumer-created or freshly-loaded project
    /// is absent from the cache, so it takes the full, safe analysis path (no public trust bypass). A <c>with</c>-clone
    /// mints a new instance the cache does not contain, so hand-mutated projects also fall to the safe path.</para>
    /// </summary>
    internal static class EditAnalysisCache
    {
        /// <summary>The reusable result of a project's open analysis — currently the id allocator seed (the counter
        /// high-water mark). Its presence also asserts the project passed the open guards (an SDK commit is canonical,
        /// undeclared-attribute-free and duplicate-id-free by construction).</summary>
        internal sealed record EditAnalysis(long AllocatorSeed);

        private static readonly ConditionalWeakTable<Project, EditAnalysis> byProject = new();

        // Diagnostic: how many full open analyses ran (a cache MISS) — the timing-independent observable the reuse test
        // reads to prove a registered project skips the re-scan.
        private static long fullAnalysisCount;

        internal static long FullAnalysisCount => Interlocked.Read(ref fullAnalysisCount);

        /// <summary>Records an SDK-produced project's analysis so its next <see cref="ProjectEditor"/> reuses it.</summary>
        internal static void Register(Project project, long allocatorSeed) =>
            byProject.AddOrUpdate(project, new EditAnalysis(allocatorSeed));

        /// <summary>The cached analysis for <paramref name="project"/>, or null when it must be analysed fresh.</summary>
        internal static EditAnalysis? TryGet(Project project) =>
            byProject.TryGetValue(project, out EditAnalysis? analysis) ? analysis : null;

        /// <summary>Records that a full open analysis just ran (a cache miss).</summary>
        internal static void CountFullAnalysis() => Interlocked.Increment(ref fullAnalysisCount);
    }
}
