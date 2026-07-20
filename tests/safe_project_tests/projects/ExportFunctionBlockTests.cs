#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// R3/T011: <see cref="ProjectAppService.ExportFunctionBlock(Project, ElementId, string, string, string, DateOnly?, string?)"/>
    /// is the single-door replacement for the app-side "Gem…" composition (US-021). It reuses the vendor .ifb byte
    /// oracle (<c>gemoracle-kip.ifb</c>), stamps a deterministic explicit author and a clock-defaulted date (never
    /// <c>Environment.UserName</c>/<c>DateTime.Now</c>), writes the path overload atomically and offers a stream
    /// primitive, does not mutate the project, and rejects a non-function-block id.
    /// </summary>
    public class ExportFunctionBlockTests
    {
        private const string Original = "testdata/projects/project3-KompleksWired.vis";
        private const string GemOracle = "functionblocks/gemoracle-kip.ifb";
        private static ProjectAppService App => new(TestSetup.Settings);

        private static ElementId KipBlockId(Project project) =>
            project.Groups.First(g => project.View(g).Name == "Stue & Køkken \"åben\"")
                .DescendantsAndSelf()
                .First(e => e.Kind == ElementKind.FunctionBlock && project.View(e).Name == "1.1.01.e. Kip tænd sluk")
                .Id!.Value;

        [Test]
        public async Task ExportFunctionBlock_Stream_MatchesVendorIfb_TextIdentical()
        {
            ProjectAppService app = App;
            Project project = await app.Load(Original);
            using var ms = new MemoryStream();

            app.ExportFunctionBlock(project, KipBlockId(project), ms, "GemOracle", "Morten Christensen",
                new DateOnly(2026, 7, 11), "Oracle tooltip");

            Assert.That(CatalogTextCompare.Equivalent(TestData.ReadBytes(GemOracle), ms.ToArray()), Is.True,
                "the exported .ifb matches the vendor oracle (whitespace-normalized)");
        }

        [Test]
        public async Task ExportFunctionBlock_Path_WritesTheSameBytesAtomically()
        {
            ProjectAppService app = App;
            Project project = await app.Load(Original);
            ElementId fbId = KipBlockId(project);

            using var ms = new MemoryStream();
            app.ExportFunctionBlock(project, fbId, ms, "GemOracle", "Morten Christensen",
                new DateOnly(2026, 7, 11), "Oracle tooltip");
            byte[] viaStream = ms.ToArray();

            string path = Path.Combine(Path.GetTempPath(), $"ihc-export-{Guid.NewGuid():N}.ifb");
            try
            {
                await app.ExportFunctionBlock(project, fbId, path, "GemOracle", "Morten Christensen",
                    new DateOnly(2026, 7, 11), "Oracle tooltip");
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(viaStream), "the path overload writes the same bytes");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public async Task ExportFunctionBlock_NonFunctionBlockId_ThrowsInvalidOperation()
        {
            ProjectAppService app = App;
            Project project = await app.Load(Original);
            ElementId locality = project.Groups.First().Id!.Value;
            using var ms = new MemoryStream();

            Assert.That(() => app.ExportFunctionBlock(project, locality, ms, "Blk", "Author"),
                Throws.InvalidOperationException, "a non-function-block id is rejected");
        }

        [Test]
        public async Task ExportFunctionBlock_DefaultsCreatedDateFromTheServiceClock()
        {
            var clock = new FakeTimeProvider(new DateTimeOffset(2030, 5, 6, 7, 8, 9, TimeSpan.Zero));
            var app = new ProjectAppService(TestSetup.Settings, A.Fake<ICatalog>(), clock);
            Project project = await app.Load(Original);
            ElementId fbId = KipBlockId(project);

            using var msDefault = new MemoryStream();
            app.ExportFunctionBlock(project, fbId, msDefault, "Blk", "Author");   // created omitted → from the clock

            using var msExplicit = new MemoryStream();
            app.ExportFunctionBlock(project, fbId, msExplicit, "Blk", "Author",
                DateOnly.FromDateTime(clock.GetLocalNow().DateTime));

            Assert.That(msDefault.ToArray(), Is.EqualTo(msExplicit.ToArray()),
                "the default created date is today from the service clock (not DateTime.Now)");
        }

        [Test]
        public async Task ExportFunctionBlock_DoesNotMutateTheProject()
        {
            ProjectAppService app = App;
            Project project = await app.Load(Original);
            ElementId fbId = KipBlockId(project);

            using var before = new MemoryStream();
            await app.Save(project, before, ProjectSaveOptions.PreserveExistingMetadata);

            using var export = new MemoryStream();
            app.ExportFunctionBlock(project, fbId, export, "Blk", "Author");

            using var after = new MemoryStream();
            await app.Save(project, after, ProjectSaveOptions.PreserveExistingMetadata);
            Assert.That(after.ToArray(), Is.EqualTo(before.ToArray()), "export is read-only over the project");
        }
    }
}
