using Avalonia.Headless;
using NUnit.Framework;

[assembly: AvaloniaTestApplication(typeof(Ihc.Tests.Shared.OpenVisualHeadlessApp))]

// Run sequentially. The reason is NOT the shared AvaloniaTestBase.CurrentTestWindow static, obvious suspect
// though it is: every write to it happens inside an [AvaloniaTest], and Avalonia starts ONE headless dispatcher
// thread per assembly and queues every such test onto it. Those writers cannot race each other whatever NUnit
// is told, and the UI tests carry the run's serial floor with or without this attribute.
//
// What parallelism would really break is the process-global state fixtures read back afterwards:
// TelemetryCapture listens by instrumentation SCOPE rather than by owner, so a concurrent test's spans land in
// another's capture (TraceProbe is how a fixture claims its own, and not every telemetry fixture uses it);
// TaskSupervisor's fault port is a single static that one SupervisedFaults detaches from under another; and
// NoLeakedHarnessAttribute is an assembly-level ITestAction, so the count it takes before a test is shared with
// every test running beside it and the leak is charged to whichever one finishes first.
[assembly: NonParallelizable]

namespace safe_visual_tests;
