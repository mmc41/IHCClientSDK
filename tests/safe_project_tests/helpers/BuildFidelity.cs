using System;
using System.IO;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// BL-E0 — the shared harness for the from-scratch authoring byte-fidelity track (BL-E1…E4): build a project
    /// through the public builder API, save it, and assert the bytes are identical to the authentic vendor oracle.
    /// Wraps <see cref="TestData.AssertBytesIdentical"/> (whose diff dump drives the first-divergence loop: read the
    /// reported byte offset + line/col, fix the one divergence, repeat) and centralizes the install-dir gate the
    /// content builds share. Complements <see cref="ProjectByteFidelityTests"/> (which proves the <em>round-trip</em>
    /// path) by proving the <em>builder</em> emits vendor-faithful output, not merely structurally-equal output.
    /// </summary>
    internal static class BuildFidelity
    {
        /// <summary>The install-dir gate the catalog-backed content builds share; skips gracefully when unconfigured.</summary>
        public static ICatalog RequireCatalog(IhcSettings settings)
        {
            string dir = settings.IhcVisualInstallDir;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Assert.Ignore($"No IHC Visual install dir configured ('{dir}'); skipping install-dir-gated build.");
            }
            return CatalogDiscovery.FromInstallDir(dir);
        }

        /// <summary>
        /// Runs <paramref name="build"/> to produce the project, saves it through <paramref name="app"/> with
        /// <paramref name="options"/>, and asserts the saved bytes equal <paramref name="oracle"/> byte-for-byte.
        /// </summary>
        public static async Task AssertByteIdentical(ProjectAppService app, string oracle, Func<Project> build,
            ProjectSaveOptions options, string? dumpActualToPath = null)
        {
            byte[] expected = TestData.ReadBytes("projects/" + oracle);
            Project built = build();
            using var ms = new MemoryStream();
            await app.Save(built, ms, options);
            byte[] actual = ms.ToArray();
            if (dumpActualToPath is not null)
            {
                File.WriteAllBytes(dumpActualToPath, actual);   // diagnostic: feed to dump_alloc.ps1 for the alloc-map phase
            }
            TestData.AssertBytesIdentical(expected, actual, $"authored build → {oracle}");
        }
    }
}
