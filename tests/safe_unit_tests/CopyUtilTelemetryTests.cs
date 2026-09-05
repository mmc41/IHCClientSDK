using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// That the deep-copy seam keeps its span name after moving onto the instrumentation core.
    ///
    /// This is asserted here rather than against the backend because the seam is NOT reachable from the
    /// editor: its only two callers are <c>AdminAppService.SaveAsJson</c> and <c>LoadFromJson</c>, both
    /// controller-facing. The name is what any existing query addresses it by, so it is worth pinning
    /// wherever it can be pinned.
    /// </summary>
    [TestFixture]
    public class CopyUtilTelemetryTests
    {
        private sealed class Sample
        {
            public string Name { get; set; } = "unchanged";
        }

        [Test]
        public void DeepCopyAndApply_EmitsItsSpanUnderTheOwnerDotOperationName()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "CopyUtil.DeepCopyAndApply" });

            object copy = CopyUtil.DeepCopyAndApply(new Sample(), (PropertyInfo? _, object? value) => value);

            Assert.Multiple(() =>
            {
                Assert.That(copy, Is.InstanceOf<Sample>(), "the copy itself must be unaffected by the migration");
                Assert.That(capture.Spans.Select(s => s.OperationName), Does.Contain("CopyUtil.DeepCopyAndApply"));
            });
        }

        /// <summary>
        /// A transformer that maps two distinct set elements onto ONE leaves a smaller set, and the copy said
        /// nothing about it.
        ///
        /// <para><c>HashSet&lt;T&gt;.Add</c> returns whether the element was new, and the result was discarded —
        /// so a non-injective transformation silently dropped elements. A WARNING rather than a throw, which is
        /// the file's own precedent: it distinguishes the lossy-but-defined copy (ComparerFallback,
        /// TypeFidelityLoss, and six more) from the unsafe MUTATION it does throw for, a few lines above this
        /// site. A collapsing transformer is the first kind: the result is well-defined, it is simply not what
        /// the caller probably meant.</para>
        /// </summary>
        [Test]
        public void DeepCopyAndApply_ACollapsingSetTransformer_WarnsThatElementsWereLost()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "CopyUtil.DeepCopyAndApply" });

            var original = new HashSet<int> { 1, 2, 3 };

            // Every element onto one: three go in, one comes out.
            object copy = CopyUtil.DeepCopyAndApply(original, (PropertyInfo? _, object? value) => value is int ? 0 : value);

            ActivityEvent collision = capture.Span("CopyUtil.DeepCopyAndApply").Events
                .Single(e => e.Tags.Any(t => t.Key == "type" && (string?)t.Value == "SetCollision"));
            Dictionary<string, object?> tags = collision.Tags.ToDictionary(t => t.Key, t => t.Value);

            Assert.Multiple(() =>
            {
                Assert.That(((HashSet<int>)copy), Has.Count.EqualTo(1), "the loss itself, which is not in dispute");
                Assert.That(tags["elementType"], Is.EqualTo(typeof(int).FullName));
                Assert.That(tags["collapsed"], Is.EqualTo(2), "three elements in, one out");
                Assert.That(tags.ContainsKey("path"), Is.True, "and where in the graph it happened");
            });
        }

        /// <summary>An injective transformer keeps every element, and says nothing.</summary>
        [Test]
        public void DeepCopyAndApply_AnInjectiveSetTransformer_WarnsAboutNothing()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "CopyUtil.DeepCopyAndApply" });

            object copy = CopyUtil.DeepCopyAndApply(new HashSet<int> { 1, 2, 3 },
                (PropertyInfo? _, object? value) => value is int n ? n * 10 : value);

            Assert.Multiple(() =>
            {
                Assert.That((HashSet<int>)copy, Is.EquivalentTo(new[] { 10, 20, 30 }));
                Assert.That(capture.Span("CopyUtil.DeepCopyAndApply").Events
                        .SelectMany(e => e.Tags).Where(t => t.Key == "type").Select(t => (string?)t.Value),
                    Does.Not.Contain("SetCollision"));
            });
        }

        /// <summary>A throwing transformer must still surface, now with the normalized error type.</summary>
        [Test]
        public void DeepCopyAndApply_WhenTheTransformerThrows_MarksTheSpanFailed()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "CopyUtil.DeepCopyAndApply" });

            Assert.Throws<System.ArgumentNullException>(() => CopyUtil.DeepCopyAndApply(new Sample(), null!));

            Assert.Multiple(() =>
            {
                Assert.That(capture.Spans, Is.Not.Empty);
                Activity captured = capture.Span("CopyUtil.DeepCopyAndApply");
                Assert.That(captured.Status, Is.EqualTo(ActivityStatusCode.Error));
                Assert.That(captured.GetTagItem("error.type"), Is.EqualTo("System.ArgumentNullException"));
            });
        }
    }
}
