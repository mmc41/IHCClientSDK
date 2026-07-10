#nullable enable
using System;
using System.IO;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Shared vendor install/corpus resolution for the install-gated differential fixtures: repo-root discovery,
    /// install-layout validation, the <c>tmp/orginstall</c> corpus location, catalog opening with a graceful skip,
    /// and the structural-diff assertion. Two resolution precedences exist deliberately: component/template
    /// <b>bodies</b> are version-stable, so a configured install serves them (<see cref="ResolveInstallThenCorpus"/>);
    /// baked <c>syn_en</c> documentation comes from the repo corpus the catalog was generated from, so comparisons
    /// against generation inputs prefer it (<see cref="ResolveCorpusThenInstall"/>).
    /// </summary>
    internal static class VendorCorpus
    {
        /// <summary>The repo root (the directory holding <c>IHCClientSDK.sln</c>), or null outside a checkout.</summary>
        public static string? FindRepoRoot()
        {
            for (DirectoryInfo? dir = new(TestContext.CurrentContext.TestDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "IHCClientSDK.sln")))
                {
                    return dir.FullName;
                }
            }
            return null;
        }

        /// <summary>The repo root; throws when the test does not run under a checkout.</summary>
        public static string RequireRepoRoot() =>
            FindRepoRoot() ?? throw new InvalidOperationException(
                "repo root (IHCClientSDK.sln) not found above the test directory");

        /// <summary>The repo's <c>tmp/orginstall</c> vendor corpus, or null when absent or incomplete.</summary>
        public static string? CorpusDir()
        {
            string? root = FindRepoRoot();
            if (root is null)
            {
                return null;
            }
            string corpus = Path.Combine(root, "tmp", "orginstall", "LK IHC Control", "IHC Visual");
            return IsCompleteInstall(corpus) ? corpus : null;
        }

        /// <summary>Whether the directory has the complete IHC Visual layout (Products + FunctionBlocks + Data).</summary>
        public static bool IsCompleteInstall(string? dir) =>
            !string.IsNullOrWhiteSpace(dir)
            && Directory.Exists(Path.Combine(dir, "Products"))
            && Directory.Exists(Path.Combine(dir, "FunctionBlocks"))
            && Directory.Exists(Path.Combine(dir, "Data"));

        /// <summary>Configured install first, repo corpus fallback — for version-stable body differentials.</summary>
        public static string? ResolveInstallThenCorpus() =>
            IsCompleteInstall(TestSetup.Settings.IhcVisualInstallDir)
                ? TestSetup.Settings.IhcVisualInstallDir
                : CorpusDir();

        /// <summary>Repo corpus first, configured install fallback — for comparisons against generation inputs.</summary>
        public static string? ResolveCorpusThenInstall() =>
            CorpusDir() ?? (IsCompleteInstall(TestSetup.Settings.IhcVisualInstallDir)
                ? TestSetup.Settings.IhcVisualInstallDir
                : null);

        /// <summary>Opens the resolved dir as a catalog; <c>Assert.Ignore</c>-skips the test when it is null.</summary>
        public static ICatalog InstalledOrIgnore(string? dir, string gateDescription)
        {
            if (dir is null)
            {
                Assert.Ignore($"No complete IHC Visual install available; skipping install-gated {gateDescription}.");
            }
            return CatalogDiscovery.FromInstallDir(dir!);
        }

        /// <summary>Fails with a readable normalized structural diff when the built tree differs from the install's.</summary>
        public static void AssertStructural(string mismatchDescription, ProjectElement expected, ProjectElement actual)
        {
            if (!expected.Equals(actual))
            {
                Assert.Fail(mismatchDescription + "\n"
                            + "EXPECTED (install):\n" + DefinitionNormalizer.Dump(expected)
                            + "\nACTUAL (built):\n" + DefinitionNormalizer.Dump(actual));
            }
        }
    }
}
