#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W0-2 / W4-2 / W4-5 — headless perf benchmark for the LOGIC of the five budgeted hot paths.
    ///
    /// <para><b>[Explicit] — never runs in the normal gate/CI.</b> Run manually, in Release, with:</para>
    /// <code>
    /// dotnet test tests/safe_project_tests/safe_project_tests.csproj -c Release \
    ///   --filter "FullyQualifiedName~PerfBaselineBenchmark" -l "console;verbosity=detailed"
    /// </code>
    ///
    /// <para><b>Scope.</b> These are the in-memory LOGIC portions of the hot paths, measured with a real
    /// <see cref="Stopwatch"/> (warm-up + samples → median/p95). Two paths are pure logic with no UI at all and
    /// so are measured end-to-end against their budgets: the <b>drag-over probe</b> (&lt; 5 ms) and <b>save</b>
    /// (&lt; 1 s). <b>Open</b> (&lt; 2 s) is measured as file parse + tree build (render excluded). <b>Commit</b>
    /// (&lt; 50 ms) and <b>undo/redo</b> (&lt; 50 ms) are measured as their logic — the scratch-session open +
    /// command apply / history step that <c>ProjectWorkflow.ApplyAsync</c> performs — which is the lower bound of
    /// the budget's "→ UI settled" figure. The Avalonia render / UI-settle remainder and the OTel-span
    /// end-to-end capture are NOT measured here; they need an interactive app run (see
    /// <c>tmp/refac-perf-baseline.md</c>). Numbers are machine-specific; the header records the machine.</para>
    /// </summary>
    [Explicit("Perf benchmark: run manually in Release; not part of the gate.")]
    [Category("Benchmark")]
    public class PerfBaselineBenchmark
    {
        private const int Warmup = 5;
        private const int Samples = 30;
        private const string LargestPath = "testdata/projects/project3-KompleksWired.vis";

        private static ProjectAppService App => new(TestSetup.Settings);

        [Test]
        public async Task Measure_LogicHotPaths()
        {
            Project projectA = await App.Load(LargestPath);
            Project projectB = Widen(projectA, extraLocalities: 300);

            // Persist B so "open" can be measured on a genuinely wider file, not just the committed one.
            string pathB = Path.Combine(Path.GetTempPath(), "fablerefac_benchB.vis");
            await App.Save(projectB, pathB);

            TestContext.Out.WriteLine("=== fablerefac headless perf benchmark (logic hot paths) ===");
            TestContext.Out.WriteLine($"machine : {RuntimeInformation.OSDescription} | {RuntimeInformation.FrameworkDescription} " +
                $"| cores={Environment.ProcessorCount} | build={BuildConfig()}");
            TestContext.Out.WriteLine($"samples : warm-up={Warmup}, sampled={Samples}, reporting median + p95 (ms)");
            TestContext.Out.WriteLine($"A-largest : {LargestPath} — {Describe(projectA)}");
            TestContext.Out.WriteLine($"B-wide    : A + 300 localities — {Describe(projectB)}");
            TestContext.Out.WriteLine("");

            foreach ((string tag, Project p) in new[] { ("A-largest", projectA), ("B-wide", projectB) })
            {
                ElementId src = p.Groups.First().Id!.Value;
                ElementId dst = p.Groups.Skip(1).First().Id!.Value;

                // drag-over probe (< 5 ms): the per-pointer cost — open a fresh scratch session over the project
                // and evaluate a move, exactly as ProjectWorkflow.CanApply/OpenScratch does.
                Measure($"drag-over probe   [{tag}]", () =>
                {
                    var s = new ProjectDocumentSession();
                    s.Open(p);
                    s.CanApply(new MoveNode(src, dst));
                });

                // commit logic (< 50 ms): scratch open + apply a locality insert + canonicalize (ProjectWorkflow.ApplyAsync path).
                Measure($"commit (apply)    [{tag}]", () =>
                {
                    var s = new ProjectDocumentSession();
                    s.Open(p);
                    s.Apply(new AddLocality("bench"));
                });

                // undo/redo (< 50 ms): one history step each way on a session that already has an edit.
                var undoSession = new ProjectDocumentSession();
                undoSession.Open(p);
                undoSession.Apply(new AddLocality("bench-undo"));
                Measure($"undo + redo       [{tag}]", () =>
                {
                    undoSession.Undo();
                    undoSession.Redo();
                });

                // save (< 1 s): canonicalize + serialize to memory (disk write excluded — the pure logic).
                var buffer = new MemoryStream();
                Measure($"save (serialize)  [{tag}]", () =>
                {
                    buffer.SetLength(0);
                    App.Save(p, buffer).GetAwaiter().GetResult();
                });
            }

            // open (< 2 s): file parse + tree build (render excluded), on both the committed and the wider file.
            Measure("open (parse+build)[A-largest]", () => App.Load(LargestPath).GetAwaiter().GetResult(), samples: 15);
            Measure("open (parse+build)[B-wide]   ", () => App.Load(pathB).GetAwaiter().GetResult(), samples: 15);

            try { File.Delete(pathB); } catch { /* best-effort temp cleanup */ }
        }

        // Applies N locality inserts to a snapshot of the project and returns the resulting wider project.
        private static Project Widen(Project project, int extraLocalities)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            for (int i = 0; i < extraLocalities; i++)
                session.Apply(new AddLocality($"bench-{i}"));
            return session.Current!;
        }

        // Warm-up + sampled Stopwatch timing → median and p95 (ms), written to the test output.
        private static void Measure(string label, Action body, int samples = Samples)
        {
            for (int i = 0; i < Warmup; i++)
                body();
            var times = new double[samples];
            for (int i = 0; i < samples; i++)
            {
                var sw = Stopwatch.StartNew();
                body();
                sw.Stop();
                times[i] = sw.Elapsed.TotalMilliseconds;
            }
            Array.Sort(times);
            double median = times[samples / 2];
            double p95 = times[(int)Math.Ceiling(0.95 * samples) - 1];
            TestContext.Out.WriteLine($"{label,-30} median={median,9:F3} ms   p95={p95,9:F3} ms");
        }

        private static string Describe(Project project) =>
            $"{project.Groups.Count} localities, {project.Root.DescendantsAndSelf().Count()} elements";

        private static string BuildConfig() =>
#if DEBUG
            "DEBUG";
#else
            "RELEASE";
#endif
    }
}
