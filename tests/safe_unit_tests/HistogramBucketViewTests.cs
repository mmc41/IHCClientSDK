using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Ihc.Bootstrap;
using NUnit.Framework;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Ihc.Tests
{
    /// <summary>
    /// That the configured bucket boundaries actually reach an export.
    ///
    /// Asserting the boundary array on its own would prove only that a constant exists. What matters is
    /// that the view is applied to the histogram the app really records into, and that the shape survives
    /// aggregation - so this builds a real MeterProvider, records real measurements, and reads the buckets
    /// back off the exported metric.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class HistogramBucketViewTests
    {
        /// <summary>
        /// The second-scale boundaries the duration view is expected to install. Stated here INDEPENDENTLY of
        /// the bootstrap's own array rather than read from it - a test that reads the value it is checking
        /// passes whatever that value becomes.
        /// </summary>
        private static readonly double[] SecondScaleBoundaries =
            { 0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.0, 5.0, 10.0, 30.0 };

        /// <summary>Collects exported metrics in memory. Hand-rolled to avoid a package for one test.</summary>
        private sealed class CollectingExporter : BaseExporter<Metric>
        {
            public List<Metric> Exported { get; } = new();

            public override ExportResult Export(in Batch<Metric> batch)
            {
                foreach (Metric metric in batch)
                {
                    Exported.Add(metric);
                }
                return ExportResult.Success;
            }
        }

        private static List<double> BoundariesOf(Metric metric)
        {
            foreach (ref readonly MetricPoint point in metric.GetMetricPoints())
            {
                var boundaries = new List<double>();
                foreach (HistogramBucket bucket in point.GetHistogramBuckets())
                {
                    boundaries.Add(bucket.ExplicitBound);
                }
                return boundaries;
            }
            return new List<double>();
        }

        [Test]
        public void ADurationHistogram_IsExportedWithTheConfiguredSecondScaleBoundaries()
        {
            string scope = "Ihc.BucketViewTest." + System.Guid.NewGuid().ToString("N");
            using var meter = new Meter(scope);
            var exporter = new CollectingExporter();

            using MeterProvider provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(scope)
                .ConfigureDurationHistogramViews()
                .AddReader(new BaseExportingMetricReader(exporter))
                .Build();

            Histogram<double> histogram = meter.CreateHistogram<double>("ihc.test.thing.duration", unit: "s");
            histogram.Record(0.004);
            histogram.Record(0.9);
            histogram.Record(44.0);

            provider.ForceFlush();

            Metric exported = exporter.Exported.Single(m => m.Name == "ihc.test.thing.duration");
            List<double> boundaries = BoundariesOf(exported);

            Assert.Multiple(() =>
            {
                // The last bucket is +infinity and is not an explicit bound, hence the trailing entry.
                Assert.That(boundaries.Take(10), Is.EqualTo(SecondScaleBoundaries),
                    "second-scale boundaries, not OpenTelemetry's unitless 0-10000 default");
                Assert.That(exported.MetricType, Is.EqualTo(MetricType.Histogram));
            });
        }

        /// <summary>
        /// The negative control. Without it the test above would also pass against a provider that applied
        /// the view to EVERYTHING, and the wildcard's precision would be untested.
        /// </summary>
        [Test]
        public void AHistogramThatIsNotADuration_KeepsTheDefaultBoundaries()
        {
            string scope = "Ihc.BucketViewTest." + System.Guid.NewGuid().ToString("N");
            using var meter = new Meter(scope);
            var exporter = new CollectingExporter();

            using MeterProvider provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(scope)
                .ConfigureDurationHistogramViews()
                .AddReader(new BaseExportingMetricReader(exporter))
                .Build();

            Histogram<long> sizes = meter.CreateHistogram<long>("ihc.test.thing.size", unit: "By");
            sizes.Record(512);

            provider.ForceFlush();

            List<double> boundaries = BoundariesOf(exporter.Exported.Single(m => m.Name == "ihc.test.thing.size"));

            Assert.That(boundaries.Take(10), Is.Not.EqualTo(SecondScaleBoundaries),
                "the view is scoped to duration histograms; a size histogram must not inherit second boundaries");
        }
    }
}
