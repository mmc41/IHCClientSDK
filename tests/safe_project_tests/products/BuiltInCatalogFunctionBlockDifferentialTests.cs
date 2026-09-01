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
    /// <item><b>Reference-catalog differential</b> — every code-authored block reduces to the same canonical component
    /// <see cref="CatalogDiscovery.FromInstallDir"/> loads from the matching reference <c>.ifb</c> (position-paired,
    /// path-sorted, so all 72 — the four master_type-duplicating favorites included — are checked).</item>
    /// <item><b>Reference-independent round-trip</b> — an embedded block inserts into a fresh project, saves and re-loads
    /// structurally equal without reading the configured reference directory.</item>
    /// <item><b>Favorites / by-name</b> — the four favorites that duplicate a master_type are all listed, with the
    /// real-category copy winning the last-wins lookup; blocks resolve by display name.</item>
    /// <item><b>Documentation</b> — every block carries Danish help text baked into the generated source
    /// (reference-independent; a reference catalog need not ship any <c>.md</c> help documents).</item>
    /// </list>
    /// </summary>
    public class BuiltInCatalogFunctionBlockDifferentialTests
    {
        [Test]
        public void EveryFunctionBlock_MatchesReferenceCatalog()
        {
            ICatalog reference = ReferenceCatalog.OpenOrIgnore("block differential");
            ICatalog built = new BuiltInCatalog();

            Assert.That(built.FunctionBlocks.Count, Is.EqualTo(reference.FunctionBlocks.Count),
                "the generated catalog registers exactly the discovered blocks (favorites duplicates included)");

            for (int i = 0; i < reference.FunctionBlocks.Count; i++)
            {
                FunctionBlockDefinition expected = reference.FunctionBlocks[i];
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
                // reference-catalog parse yields (declarations, prolog datum, DOCTYPE root — the D1 primary model).
                Assert.That(actual.Grammar, Is.EqualTo(expected.Grammar), $"[{i}] structured grammar");
                ReferenceCatalog.AssertStructural(
                    $"Generated function block '{expected.DisplayName}' differs from the reference-catalog .ifb.",
                    DefinitionNormalizer.Normalize(expected.Body, expected.Grammar),
                    DefinitionNormalizer.Normalize(actual.Body, expected.Grammar));
            }
        }

        [Test]
        public void EmittedFunctionBlock_InsertsAndRoundTrips_WithoutReferenceCatalog()
        {
            var catalog = new BuiltInCatalog();   // no reference catalog directory involved
            var app = new ProjectAppService(TestSetup.Settings, catalog,
                new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
                    new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero)));
            Project blank = app.CreateNew(new ProjectDetails("P", "I", "DK"));

            FunctionBlockDefinition block = catalog.FunctionBlock("1.1.01");   // resolved without reading the reference catalog
            ProjectEditor editor = blank.Edit();
            editor.Group("Stue").AddFunctionBlock(block);
            Project built = editor.ToProject();

            using var ms = new MemoryStream();
            app.Save(built, ms, ProjectSaveOptions.PreserveExistingMetadata).GetAwaiter().GetResult();
            Project reloaded = app.Load(new MemoryStream(ms.ToArray())).GetAwaiter().GetResult();

            Assert.That(reloaded.Equals(built), Is.True,
                "the inserted block round-trips structurally without reading the reference catalog");
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
        public void EveryBlock_CarriesBakedDanishDocumentation_WithoutReferenceCatalog()
        {
            ICatalog catalog = new BuiltInCatalog();

            // The Danish help text is baked into the generated source, so no reference catalog is read.
            FunctionBlockDefinition kip = catalog.FunctionBlock("1.1.01");
            Assert.Multiple(() =>
            {
                Assert.That(kip.Documentation.IsEmpty, Is.False, "1.1.01 carries baked documentation");
                Assert.That(kip.Documentation.Summary, Is.Not.Null.And.Not.Empty);
                Assert.That(kip.Documentation.Resources, Is.Not.Empty, "per-pin help is baked in");
            });

            // Every block is documented — the non-vendor AutoProof block included.
            int documented = catalog.FunctionBlocks.Count(b => b.Documentation.Summary is { Length: > 0 });
            Assert.That(documented, Is.EqualTo(catalog.FunctionBlocks.Count),
                "every block carries a baked summary");
        }

        /// <summary>
        /// A baked help text that no pin hands back is silently invisible — the GUI's per-pin help panel simply shows
        /// nothing. Since the key is the pin's position, a text is reachable exactly when a pin surfaces it, so
        /// counting the two sides catches both an unreachable entry and a pin that lost its text.
        /// </summary>
        [Test]
        public void EveryPerResourceDocumentationEntry_ReachesAPinOfItsBlock()
        {
            ICatalog catalog = new BuiltInCatalog();

            var unreachable = new List<string>();
            var undocumented = new List<string>();
            int entries = 0;
            foreach (FunctionBlockDefinition block in catalog.FunctionBlocks)
            {
                int baked = block.Documentation.Resources.Count;
                int onPins = block.Inputs.Concat(block.Outputs).Concat(block.Settings).Concat(block.InternalVariables)
                    .Count(pin => pin.Documentation is not null);
                entries += baked;
                if (baked != onPins)
                {
                    unreachable.Add($"{block.DisplayName}: {baked} baked, {onPins} reach a pin");
                }
                if (baked == 0)
                {
                    undocumented.Add(block.DisplayName);
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(unreachable, Is.Empty, "every baked help text is handed back by the pin it documents");
                Assert.That(undocumented, Is.Empty, "every block carries per-resource help, not only a summary");
                // A deliberate documentation change updates this number; a silent loss does not. 709, not the 697 of
                // the name-keyed era: the 12 pins that shared a sibling's display name now hold their own entry.
                Assert.That(entries, Is.EqualTo(709), "the baked per-resource help entries");
            });
        }

    }
}
