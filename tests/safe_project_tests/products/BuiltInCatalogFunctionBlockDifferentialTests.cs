#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Catalog;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Phase H fidelity gate for the generated <see cref="BuiltInCatalog"/> function-block catalog.
    /// <list type="bullet">
    /// <item><b>Install-gated differential</b> — every code-authored block reduces to the same canonical component
    /// <see cref="CatalogDiscovery.FromInstallDir"/> loads from the matching vendor <c>.ifb</c> (position-paired,
    /// path-sorted, so all 72 — the four master_type-duplicating favorites included — are checked).</item>
    /// <item><b>Install-free round-trip</b> — an embedded block inserts into a fresh project, saves and re-loads
    /// structurally equal, with no IHC Visual install present.</item>
    /// <item><b>Favorites / by-name</b> — the four favorites that duplicate a master_type are all listed, with the
    /// real-category copy winning the last-wins lookup; blocks resolve by display name.</item>
    /// <item><b>Documentation</b> — each block carries the syn_en help text baked into the generated source, matching a
    /// fresh parse of the sibling <c>syn_en*.md</c>.</item>
    /// </list>
    /// </summary>
    public class BuiltInCatalogFunctionBlockDifferentialTests
    {
        [Test]
        public void EveryFunctionBlock_MatchesInstallDir()
        {
            ICatalog installed = Installed();
            ICatalog built = new BuiltInCatalog();

            Assert.That(built.FunctionBlocks.Count, Is.EqualTo(installed.FunctionBlocks.Count),
                "the generated catalog registers exactly the discovered blocks (favorites duplicates included)");

            for (int i = 0; i < installed.FunctionBlocks.Count; i++)
            {
                FunctionBlockDefinition expected = installed.FunctionBlocks[i];
                FunctionBlockDefinition actual = built.FunctionBlocks[i];
                Assert.Multiple(() =>
                {
                    Assert.That(actual.MasterType, Is.EqualTo(expected.MasterType), $"[{i}] master_type");
                    Assert.That(actual.MasterVersion, Is.EqualTo(expected.MasterVersion), $"[{i}] master_version");
                    Assert.That(actual.MasterName, Is.EqualTo(expected.MasterName), $"[{i}] master_name");
                    Assert.That(actual.DisplayName, Is.EqualTo(expected.DisplayName), $"[{i}] display name");
                    Assert.That(actual.CategoryPath, Is.EqualTo(expected.CategoryPath), $"[{i}] category path");
                });
                // The structured grammar is value-comparable: the generated block must carry the exact grammar the
                // install-dir parse yields (declarations, prolog datum, DOCTYPE root — the D1 primary model).
                Assert.That(actual.Grammar, Is.EqualTo(expected.Grammar), $"[{i}] structured grammar");
                AssertStructural(expected.DisplayName,
                    DefinitionNormalizer.Normalize(expected.Body, expected.Grammar),
                    DefinitionNormalizer.Normalize(actual.Body, expected.Grammar));
            }
        }

        [Test]
        public void EmittedFunctionBlock_InsertsAndRoundTrips_WithoutInstall()
        {
            var catalog = new BuiltInCatalog();   // no IhcVisualInstallDir involved
            var app = new ProjectAppService(TestSetup.Settings, catalog,
                new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
                    new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero)));
            Project blank = app.CreateNew(new ProjectDetails("P", "I", "DK"));

            FunctionBlockDefinition block = catalog.FunctionBlock("1.1.01");   // a stock block, resolved install-free
            ProjectEditor editor = blank.Edit();
            editor.Group("Stue").AddFunctionBlock(block);
            Project built = editor.ToProject();

            using var ms = new MemoryStream();
            app.Save(built, ms, ProjectSaveOptions.PreserveExistingMetadata).GetAwaiter().GetResult();
            Project reloaded = app.Load(new MemoryStream(ms.ToArray())).GetAwaiter().GetResult();

            Assert.That(reloaded.Equals(built), Is.True, "the inserted block round-trips structurally, no install present");
        }

        [Test]
        public void FavoritesDuplicate_LastWins_AndBothListed()
        {
            ICatalog catalog = new BuiltInCatalog();

            // A master_type that two files share (a favorite duplicated under 00. Foretrukne and its real category).
            string dupType = catalog.FunctionBlocks
                .GroupBy(b => b.MasterType)
                .First(g => g.Count() > 1).Key;

            List<FunctionBlockDefinition> copies = catalog.FunctionBlocks.Where(b => b.MasterType == dupType).ToList();
            Assert.That(copies, Has.Count.GreaterThanOrEqualTo(2), "both favorites copies are listed");

            // Last-wins resolves to the copy registered latest (path-sorted), i.e. the real category, not 00. Foretrukne.
            FunctionBlockDefinition resolved = catalog.FunctionBlock(dupType);
            Assert.Multiple(() =>
            {
                Assert.That(resolved.MasterType, Is.EqualTo(dupType));
                Assert.That(resolved.CategoryPath, Is.EqualTo(copies[^1].CategoryPath), "last-wins by registration order");
                Assert.That(resolved.CategoryPath, Does.Not.StartWith("00."), "the real category wins over the favorite");
            });
        }

        [Test]
        public void FunctionBlock_ResolvesByDisplayName()
        {
            ICatalog catalog = new BuiltInCatalog();
            FunctionBlockDefinition any = catalog.FunctionBlocks[10];

            Assert.That(catalog.FunctionBlockByName(any.DisplayName).DisplayName, Is.EqualTo(any.DisplayName));
        }

        [Test]
        public void EveryBlock_CarriesBakedSynEnDocumentation_InstallFree()
        {
            ICatalog catalog = new BuiltInCatalog();

            // The syn_en help text is baked into the generated source, so it is present with no install dir.
            FunctionBlockDefinition kip = catalog.FunctionBlock("1.1.01");
            Assert.Multiple(() =>
            {
                Assert.That(kip.Documentation.IsEmpty, Is.False, "1.1.01 carries baked syn_en documentation");
                Assert.That(kip.Documentation.Summary, Is.Not.Null.And.Not.Empty);
                Assert.That(kip.Documentation.Resources, Is.Not.Empty, "per-pin help is baked in");
            });

            // The vast majority of blocks have a syn_en sibling, so most carry a summary.
            int documented = catalog.FunctionBlocks.Count(b => b.Documentation.Summary is { Length: > 0 });
            Assert.That(documented, Is.GreaterThan(catalog.FunctionBlocks.Count / 2),
                "most blocks carry baked documentation");
        }

        [Test]
        public void Documentation_MatchesFreshSynEnParse()
        {
            // Doc-equality is corpus-only: the baked syn_en text comes from the repo corpus the catalog was
            // generated from, and (unlike the version-stable bodies) help text differs across IHC Visual
            // versions — a configured-install fallback would fail on wording, not on machinery.
            string? root = FindRepoRoot(TestContext.CurrentContext.TestDirectory);
            string? dir = root is null ? null : Path.Combine(root, "tmp", "orginstall", "LK IHC Control", "IHC Visual");
            if (dir is null || !IsCompleteInstall(dir))
            {
                Assert.Ignore("Repo corpus (tmp/orginstall) not present; skipping corpus-gated doc-equality.");
            }
            IReadOnlyList<string> files = Directory
                .EnumerateFiles(Path.Combine(dir!, "FunctionBlocks"), "*.ifb", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();
            ICatalog built = new BuiltInCatalog();
            Assert.That(built.FunctionBlocks.Count, Is.EqualTo(files.Count), "block count matches the install .ifb count");

            for (int i = 0; i < files.Count; i++)
            {
                FunctionBlockDocumentation expected =
                    FunctionBlockDocReader.ForFunctionBlock(files[i], synEnOnly: true) ?? FunctionBlockDocumentation.Empty;
                FunctionBlockDocumentation actual = built.FunctionBlocks[i].Documentation;
                Assert.Multiple(() =>
                {
                    Assert.That(actual.Summary, Is.EqualTo(expected.Summary), $"[{i}] {Path.GetFileName(files[i])} summary");
                    Assert.That(actual.Resources, Is.EquivalentTo(expected.Resources),
                        $"[{i}] {Path.GetFileName(files[i])} per-resource docs");
                });
            }
        }

        // ---- install resolution (mirrors the product differential test) ----

        private static ICatalog Installed()
        {
            string? dir = ResolveCompleteInstall();
            if (dir is null)
            {
                Assert.Ignore("No complete IHC Visual install available; skipping install-gated block differential.");
            }
            return CatalogDiscovery.FromInstallDir(dir!);
        }

        private static void AssertStructural(string label, ProjectElement expected, ProjectElement actual)
        {
            if (!expected.Equals(actual))
            {
                Assert.Fail($"Generated function block '{label}' differs from the install-dir .ifb.\n"
                            + "EXPECTED (install):\n" + DefinitionNormalizer.Dump(expected)
                            + "\nACTUAL (built):\n" + DefinitionNormalizer.Dump(actual));
            }
        }

        // The committed catalog (bodies + baked syn_en docs) is generated from the repo corpus, so the install-gated
        // differentials verify against that SAME corpus first (bodies are identical across installs, but syn_en help
        // text can differ per install version). A configured install is the fallback for a checkout without the corpus.
        private static string? ResolveCompleteInstall()
        {
            string? root = FindRepoRoot(TestContext.CurrentContext.TestDirectory);
            if (root is not null)
            {
                string corpus = Path.Combine(root, "tmp", "orginstall", "LK IHC Control", "IHC Visual");
                if (IsCompleteInstall(corpus))
                {
                    return corpus;
                }
            }
            if (IsCompleteInstall(TestSetup.Settings.IhcVisualInstallDir))
            {
                return TestSetup.Settings.IhcVisualInstallDir;
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
