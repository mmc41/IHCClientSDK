using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ihc;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <see cref="OperationScope"/>'s disposal is FAIL-OPEN by design: instrumentation is never the point of the
    /// operation, so a broken tag or a disposed meter must not fail the caller's work. That contract is not in
    /// question.
    ///
    /// <para>What is in question is that it used to fail open into NOTHING. Measured by injecting a fault into
    /// <c>ApplyOutcomeToSpan</c> during one project open: twelve spans lost their status, their error type and
    /// their instruments, the operations all still reported success, and no channel anywhere recorded that the
    /// recording had failed. The one mechanism whose job is to make failures visible was the one that could fail
    /// invisibly — so a whole session could read clean because the machinery that would have said otherwise was
    /// broken.</para>
    ///
    /// <para>Reported through the OWNER's fault port, which is the same port an operation's own exception goes
    /// to, and still fail-open: the report is guarded in turn, so a sink that throws cannot become the failure
    /// reported on top of the one being reported.</para>
    /// </summary>
    [TestFixture]
    public class InstrumentationFaultTests
    {
        private const string Scope = "Ihc.Tests.InstrumentationFault";

        /// <summary>One owner, its capture and the list its fault port writes to — the three every case needs
        /// together, so no case can capture one scope and report through another.</summary>
        private sealed class Rig : IDisposable
        {
            private readonly TelemetryCapture capture = TelemetryCapture.Listen(Scope);

            public List<(string Operation, Exception Failure)> Faults { get; } = [];

            public OperationTelemetry Telemetry { get; }

            public Rig() =>
                Telemetry = new(new TelemetrySurface(Scope), "Owner",
                    (operation, failure) => Faults.Add((operation, failure)));

            public void Dispose() => capture.Dispose();
        }

        [Test]
        public void AFaultWhileRecordingTheOutcome_IsReportedToTheOwnersFaultPort()
        {
            using Rig rig = new();

            using (OperationScope scope = rig.Telemetry.Start("Work"))
            {
                scope.SetOutcome(OperationOutcome.Failed(new UnreadableException()));
            }

            Assert.That(rig.Faults, Is.Not.Empty,
                "the mechanism that records failures must not be able to fail invisibly");
        }

        [Test]
        public void TheCallersWorkIsStillNotFailed()
        {
            using Rig rig = new();

            Assert.That(() =>
            {
                using OperationScope scope = rig.Telemetry.Start("Work");
                scope.SetOutcome(OperationOutcome.Failed(new UnreadableException()));
            }, Throws.Nothing, "fail-open is the contract, and it is unchanged");
        }

        [Test]
        public void AnOperationThatRecordsCleanlyReportsNothing()
        {
            using Rig rig = new();

            using (OperationScope scope = rig.Telemetry.Start("Work"))
            {
                scope.SetOutcome(OperationOutcome.Ok);
            }

            Assert.That(rig.Faults, Is.Empty);
        }

        /// <summary>A sink that throws must not become the failure reported on top of the one being reported.</summary>
        [Test]
        public void AThrowingFaultPortIsItselfContained()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Scope);
            OperationTelemetry telemetry = new(
                new TelemetrySurface(Scope), "Owner", (_, _) => throw new InvalidOperationException("bad sink"));

            Assert.That(() =>
            {
                using OperationScope scope = telemetry.Start("Work");
                scope.SetOutcome(OperationOutcome.Failed(new UnreadableException()));
            }, Throws.Nothing);
        }

        /// <summary>
        /// The classifier is the OTHER half of the same story. A body with many outcome-producing exits is
        /// classified once over what it returned, so a classifier that throws leaves the operation reporting
        /// <c>Ok</c> — indistinguishable from one that genuinely succeeded, and until this was reported it was
        /// the one remaining way for the recording to fail with nothing said anywhere.
        /// </summary>
        [Test]
        public void AFaultWhileClassifyingTheResult_IsReportedToo()
        {
            using Rig rig = new();

            int answer = rig.Telemetry.Run("Work", _ => 42,
                classify: _ => throw new InvalidOperationException("the classifier is broken"));

            Assert.Multiple(() =>
            {
                Assert.That(answer, Is.EqualTo(42), "the caller's work is unaffected, which is the contract");
                Assert.That(rig.Faults.Select(f => f.Operation), Does.Contain("Work"),
                    "an operation whose outcome could not be derived reports Ok, so nothing else would say this");
            });
        }
    }
}
