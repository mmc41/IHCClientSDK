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
