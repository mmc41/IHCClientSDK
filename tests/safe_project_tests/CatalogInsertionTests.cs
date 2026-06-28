using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Install-dir-gated: <strong>every</strong> discovered product/function-block descriptor inserts one-at-a-time
    /// into a fresh project, saves, and re-loads structurally equal. Under the open-world model the static registry
    /// no longer needs to declare every catalog type — a type the registry does not contain is inserted using the
    /// grammar captured from its own catalog descriptor's inline DTD (merged into the project on insert), so the full
    /// ~173-descriptor catalog round-trips structurally regardless of what the (now lean) registry covers. Each
    /// inserted project also runs through <see cref="ProjectValidator"/>; all validate clean except the S0 device,
    /// whose vendor catalog data ships a malformed <c>kWh@accessibility="readwrite"</c> (outside the DTD enumeration)
    /// — a faithfully-copied source-data quirk the validator rightly flags, tolerated here but asserted to be the
    /// <em>only</em> kind of validity issue.
    /// </summary>
    public class CatalogInsertionTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static CatalogDiscovery RequireCatalog()
        {
            string dir = Settings.IhcVisualInstallDir;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Assert.Ignore($"No IHC Visual install dir configured ('{dir}'); skipping install-dir-gated test.");
            }
            return CatalogDiscovery.FromInstallDir(dir);
        }

        [Test]
        public void EveryDiscoveredDescriptor_InsertsValidatesAndRoundTrips()
        {
            CatalogDiscovery catalog = RequireCatalog();
            var app = new ProjectAppService(Settings, catalog,
                new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 6, 27, 16, 5, 51, TimeSpan.Zero)));
            Project blank = app.CreateNew(new ProjectDetails("P", "I", "DK"));

            int inserted = 0;
            var failures = new List<string>();
            var validationQuirks = new List<string>();   // pre-existing malformed VENDOR catalog data, not machinery bugs

            void InsertOne(string name, Action<GroupRef> add)
            {
                try
                {
                    ProjectEditor editor = blank.Edit();
                    add(editor.Group("Stue"));
                    Project built = editor.ToProject();

                    // Structural round-trip — the core insert-machinery promise, required for every descriptor. For a
                    // type the registry no longer declares, the grammar comes from the descriptor's own inline DTD.
                    using var ms = new MemoryStream();
                    app.Save(built, ms, ProjectSaveOptions.PreserveExistingMetadata).GetAwaiter().GetResult();
                    Project reloaded = app.Load(new MemoryStream(ms.ToArray())).GetAwaiter().GetResult();
                    if (!reloaded.Equals(built))
                    {
                        failures.Add($"{name}: did not round-trip structurally equal");
                        return;
                    }
                    inserted++;

                    // Semantic validation — clean for all but the S0 device's malformed vendor <kWh accessibility="readwrite">.
                    ProjectValidationResult validation = app.Validate(built);
                    if (!validation.IsValid)
                    {
                        validationQuirks.AddRange(validation.Errors.Select(e => $"{name}: {e}"));
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            foreach (ProductDescriptor product in catalog.Products)
            {
                InsertOne(product.DisplayName, room => room.AddProduct(product));
            }
            foreach (FunctionBlockDescriptor block in catalog.FunctionBlocks)
            {
                InsertOne(block.DisplayName, room => room.AddFunctionBlock(block));
            }

            TestContext.Out.WriteLine($"inserted+round-tripped: {inserted}");
            TestContext.Out.WriteLine($"tolerated vendor-data validation quirks ({validationQuirks.Count}): {string.Join(" | ", validationQuirks)}");

            Assert.Multiple(() =>
            {
                Assert.That(failures, Is.Empty,
                    "every descriptor must insert + round-trip structurally (open-world: unregistered types use their own descriptor's DTD)");
                Assert.That(inserted, Is.GreaterThan(150), "the full discovered catalog (~173 descriptors) inserts + round-trips");
                Assert.That(validationQuirks.All(q => q.Contains("accessibility") && q.Contains("readwrite")), Is.True,
                    "the only tolerated validity issues are the known vendor-catalog accessibility=\"readwrite\" quirk; got: "
                    + string.Join(" | ", validationQuirks));
            });
        }
    }
}
