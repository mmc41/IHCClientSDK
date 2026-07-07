#nullable enable
using System.IO;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Install-gated differential (plan Phase C, C1–C3): the three hand-authored <see cref="BuiltInCatalog"/>
    /// File→New templates must be <b>structurally identical</b> to what
    /// <see cref="CatalogDiscovery.FromInstallDir"/> loads from the vendor <c>NewDoc.idf</c>,
    /// <c>EnumeratorDefinitions.def</c> and <c>fb.def</c> (i.e. their POST-parse, DTD-defaulted shape). Resolves a
    /// complete IHC Visual install — the configured install dir, or the repo's <c>tmp/orginstall</c> corpus in a
    /// dev tree — and skips gracefully when neither is present (clean CI).
    /// </summary>
    /// <remarks>
    /// The empty-FB template's <c>InlineDtdBlocks</c> are deliberately excluded from the comparison: the
    /// code-authored template leaves them empty because every <c>fb.def</c> tag is registry-declared and never
    /// merged (see <see cref="BuiltInCatalog"/>.Templates remarks). Body + identity are the functional surface.
    /// </remarks>
    public class BuiltInCatalogTemplateDifferentialTests
    {
        private static ICatalog Installed()
        {
            string? dir = ResolveCompleteInstall();
            if (dir is null)
            {
                Assert.Ignore("No complete IHC Visual install available; skipping install-gated template differential.");
            }
            return CatalogDiscovery.FromInstallDir(dir!);
        }

        [Test]
        public void NewProjectSkeleton_MatchesInstallDir()
        {
            ICatalog installed = Installed();
            ICatalog built = new BuiltInCatalog();
            AssertStructural(installed.NewProjectSkeleton, built.NewProjectSkeleton);
        }

        [Test]
        public void BuiltInEnumerators_MatchInstallDir()
        {
            ICatalog installed = Installed();
            ICatalog built = new BuiltInCatalog();
            AssertStructural(installed.BuiltInEnumerators, built.BuiltInEnumerators);
        }

        [Test]
        public void EmptyFunctionBlockTemplate_BodyAndIdentityMatchInstallDir()
        {
            ICatalog installed = Installed();
            ICatalog built = new BuiltInCatalog();
            var expected = installed.EmptyFunctionBlockTemplate;
            var actual = built.EmptyFunctionBlockTemplate;
            Assert.Multiple(() =>
            {
                AssertStructural(expected.Body, actual.Body);
                Assert.That(actual.IsEmptyTemplate, Is.True, "template flag");
                Assert.That(actual.MasterName, Is.EqualTo(expected.MasterName), nameof(actual.MasterName));
                Assert.That(actual.DisplayName, Is.EqualTo(expected.DisplayName), nameof(actual.DisplayName));
                Assert.That(actual.MasterType, Is.EqualTo(expected.MasterType), nameof(actual.MasterType));
                Assert.That(actual.MasterVersion, Is.EqualTo(expected.MasterVersion), nameof(actual.MasterVersion));
                Assert.That(actual.CategoryPath, Is.EqualTo(expected.CategoryPath), nameof(actual.CategoryPath));
            });
        }

        private static void AssertStructural(ProjectElement expected, ProjectElement actual)
        {
            if (!expected.Equals(actual))
            {
                Assert.Fail("Structural mismatch between the code-authored template and the install-dir file.\n"
                            + "EXPECTED (install):\n" + DefinitionNormalizer.Dump(expected)
                            + "\nACTUAL (built):\n" + DefinitionNormalizer.Dump(actual));
            }
        }

        // Prefer a configured, complete install; otherwise the repo corpus (dev tree); otherwise null → skip.
        private static string? ResolveCompleteInstall()
        {
            if (IsCompleteInstall(TestSetup.Settings.IhcVisualInstallDir))
            {
                return TestSetup.Settings.IhcVisualInstallDir;
            }
            string? root = FindRepoRoot(TestContext.CurrentContext.TestDirectory);
            if (root is not null)
            {
                string corpus = Path.Combine(root, "tmp", "orginstall", "LK IHC Control", "IHC Visual");
                if (IsCompleteInstall(corpus))
                {
                    return corpus;
                }
            }
            return null;
        }

        private static bool IsCompleteInstall(string? dir) =>
            !string.IsNullOrWhiteSpace(dir)
            && Directory.Exists(Path.Combine(dir, "Products"))
            && Directory.Exists(Path.Combine(dir, "FunctionBlocks"))
            && Directory.Exists(Path.Combine(dir, "Data"));

        private static string? FindRepoRoot(string start)
        {
            for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "IHCClientSDK.sln")))
                {
                    return dir.FullName;
                }
            }
            return null;
        }
    }
}
