using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Ihc;
using Ihc.Tests.Shared;
using Ihc.Vis.Reporting;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The last three Tier-3 seams: report generation, catalog materialization, and the per-edit analysis
    /// cache's miss counter.
    ///
    /// The analysis one is a MIRROR rather than a replacement. <c>FullAnalysisCount</c> stays exactly as it
    /// is, because a test reads it directly and a metric is not readable from a test without a listener.
    /// Mirroring means the two can be compared - which is what the last test here does, so a future change
    /// that moves one and not the other is caught.
    /// </summary>
    public class Tier3TelemetryTests
    {
        private static Project Fixture() =>
            new ProjectAppService(TestSetup.Settings).Load("testdata/projects/Project1-SimpelWired.vis")
                .GetAwaiter().GetResult();

        [Test]
        public void AGeneratedReportSaysWhatItWasAndHowBigItGot()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "ReportGenerator.Generate" });

            byte[] rendered = ReportGenerator.Generate(
                Fixture(), ReportKind.Installation, ReportMode.Standard, ReportMimeTypes.PlainText,
                iconProvider: null, generatedAt: System.DateTimeOffset.UnixEpoch);

            Activity report = capture.Span("ReportGenerator.Generate");
            Assert.Multiple(() =>
            {
                Assert.That(report.GetTagItem("ihc.report.kind"), Is.EqualTo("Installation"));
                Assert.That(report.GetTagItem("ihc.report.mode"), Is.EqualTo("Standard"));
                Assert.That(report.GetTagItem("ihc.report.mime"), Is.EqualTo(ReportMimeTypes.PlainText));
                Assert.That(report.GetTagItem("ihc.report.bytes"), Is.EqualTo(rendered.Length),
                    "the size on the span IS the size returned, not a separately measured number");
            });
        }

        /// <summary>
        /// The existing assertion must still hold, AND the mirrored instrument must agree with it. Comparing
        /// the two is the whole point of mirroring rather than replacing.
        /// </summary>
        [Test]
        public void TheAnalysisMissCounterAgreesWithTheExistingCount()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                instruments: new[] { "ihc.edit.analysis.miss" });

            long before = EditAnalysisCache.FullAnalysisCount;

            Project project = Fixture();
            ProjectEditor editor = project.Edit();
            editor.ToProject();

            long after = EditAnalysisCache.FullAnalysisCount;
            double recorded = capture.PointsOf("ihc.edit.analysis.miss").Sum(p => p.Value);

            Assert.Multiple(() =>
            {
                Assert.That(after, Is.GreaterThanOrEqualTo(before), "the existing counter still moves as it did");
                Assert.That(recorded, Is.EqualTo(after - before),
                    "the mirrored instrument counts exactly what the existing counter counted");
            });
        }

        /// <summary>
        /// Materialization is timed as a histogram because the guarding Lazy is PublicationOnly - a rare
        /// concurrent double-run is legal, and a counter asserting "once" would either be wrong or hide it.
        /// The catalog is process-wide and may already be materialized, so this asserts the instrument
        /// EXISTS and is recordable rather than that it fired during this test.
        /// <para>
        /// The same process-wide Lazy is why the <c>BuiltInCatalog.Materialize</c> SPAN that now accompanies
        /// this histogram is not pinned here: whether it fires during any given test depends on which test ran
        /// first, and a conditional assertion pins nothing. It is verified against the live backend instead -
        /// on a launch it appears under the operation that first touched the catalog, which is what the span
        /// exists to reveal - and §11 of the OpenVisual telemetry-points document carries that check.
        /// </para>
        /// </summary>
        [Test]
        public void TheCatalogMaterializationInstrumentIsLiveAndRecordable()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                instruments: new[] { "ihc.catalog.materialization.duration" });

            // Touching the catalog materializes it if this process has not already done so.
            _ = new ProjectAppService(TestSetup.Settings).GetAvailableProducts();
            SdkTelemetryRegistry.CatalogMaterializationDuration.Record(0.0);

            Assert.That(capture.PointsOf("ihc.catalog.materialization.duration"), Is.Not.Empty,
                "the instrument is constructed and enabled - a never-constructed one records nothing at all");
        }
    }
}
