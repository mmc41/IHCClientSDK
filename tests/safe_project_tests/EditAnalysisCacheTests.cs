using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W4-1 (P1b): the edit-analysis cache. A session-committed (SDK-produced) project registers its open
    /// analysis so the next <c>Edit()</c> reuses it instead of re-scanning the tree + re-running the guards; a
    /// loaded/foreign project is not registered and takes the full safe path. The registered seed is exactly what a
    /// fresh <see cref="IdAllocator.ForProject"/> scan returns — the byte-fidelity-critical invariant (reuse must never
    /// allocate a different id than a from-scratch analysis would).
    /// </summary>
    [NonParallelizable]   // the reuse assertion reads a process-global full-analysis counter
    public class EditAnalysisCacheTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        [Test]
        public async Task CommittedProject_RegistersAnalysis_ForeignRunsFull_SeedMatchesFreshScan()
        {
            Project loaded = await Load("project3-KompleksWired.vis");

            long before = EditAnalysisCache.FullAnalysisCount;
            Project committed = loaded.Edit().ToProject();   // foreign open → full analysis; ToProject registers `committed`
            long afterFirst = EditAnalysisCache.FullAnalysisCount;

            committed.Edit();                                 // committed → reuse; no full analysis
            long afterSecond = EditAnalysisCache.FullAnalysisCount;

            Assert.Multiple(() =>
            {
                Assert.That(EditAnalysisCache.TryGet(loaded), Is.Null, "a loaded/foreign project is not registered");
                Assert.That(EditAnalysisCache.TryGet(committed), Is.Not.Null, "a session-committed project's analysis is registered");
                Assert.That(afterFirst - before, Is.EqualTo(1), "the foreign project ran one full open analysis");
                Assert.That(afterSecond - afterFirst, Is.EqualTo(0), "the committed project's second Edit reused the cached analysis");
                // byte-fidelity-critical: the cached seed equals a fresh scan, so reuse never allocates a different id.
                Assert.That(EditAnalysisCache.TryGet(committed)!.AllocatorSeed,
                    Is.EqualTo(IdAllocator.ForProject(committed).Counter), "the registered seed equals a fresh IdAllocator scan");
            });
        }
    }
}
