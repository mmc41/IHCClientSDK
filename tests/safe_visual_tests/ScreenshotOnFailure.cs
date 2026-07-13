using System;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace safe_visual_tests;

/// <summary>
/// Captures a screenshot of <see cref="AvaloniaTestBase.CurrentTestWindow"/> when the test fails, saving it to
/// <c>TestFailureScreenshots/</c> in the test output directory and attaching it to the test result. Adapted from
/// the proven twin in tests/safe_lab_tests.
///
/// <para>Usage: apply together with <c>[AvaloniaTest]</c> on <b>each test method</b> (NUnit cannot apply
/// <see cref="IWrapSetUpTearDown"/> attributes at class level, see https://github.com/nunit/nunit/issues/2220),
/// and register the window under test via <see cref="AvaloniaTestBase.CurrentTestWindow"/>.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class CaptureScreenshotOnFailureAttribute : Attribute, IWrapSetUpTearDown
{
    public TestCommand Wrap(TestCommand command) => new ScreenshotCaptureCommand(command);

    private sealed class ScreenshotCaptureCommand : DelegatingTestCommand
    {
        public ScreenshotCaptureCommand(TestCommand innerCommand) : base(innerCommand)
        {
        }

        public override TestResult Execute(TestExecutionContext context)
        {
            TestResult? result;
            Exception? testException = null;

            try
            {
                result = innerCommand.Execute(context);
            }
            catch (Exception ex)
            {
                testException = ex;
                result = context.CurrentResult;
            }

            // Capture only on genuine failure — not for Assert.Pass()/Assert.Inconclusive() outcomes.
            bool shouldCapture = result?.ResultState.Status == TestStatus.Failed;
            if (testException != null && !shouldCapture)
            {
                var exceptionType = testException.GetType().Name;
                shouldCapture = exceptionType != "InconclusiveException" && exceptionType != "SuccessException";
            }

            if (shouldCapture)
            {
                try
                {
                    AvaloniaTestBase.CaptureScreenshotOnFailure();
                }
                catch (Exception captureEx)
                {
                    TestContext.Out.WriteLine($"Failed to capture screenshot: {captureEx.Message}");
                }
            }

            if (testException != null)
            {
                throw testException;
            }

            return result!;
        }
    }
}

/// <summary>
/// Base class for Avalonia UI tests wanting automatic failure screenshots: set <see cref="CurrentTestWindow"/>
/// to the (shown) window under test and mark the test with <see cref="CaptureScreenshotOnFailureAttribute"/>.
/// The whole assembly runs sequentially (<c>[assembly: NonParallelizable]</c>) because this registration is a
/// shared static.
/// </summary>
public abstract class AvaloniaTestBase
{
    /// <summary>The window currently under test; registered by each test for failure capture.</summary>
    protected static Window? CurrentTestWindow { get; set; }

    /// <summary>
    /// Called by <see cref="CaptureScreenshotOnFailureAttribute"/> on failure: renders
    /// <see cref="CurrentTestWindow"/> via the headless session's dispatcher (where the Skia render interface
    /// lives), saves a timestamped PNG under <c>TestFailureScreenshots/</c> and attaches it to the test result.
    /// A no-op (with a log line) when no window was registered.
    /// </summary>
    internal static void CaptureScreenshotOnFailure()
    {
        Window? window = CurrentTestWindow;   // local so the null check survives into the dispatch lambda
        if (window == null)
        {
            TestContext.Out.WriteLine("No window registered for screenshot capture");
            return;
        }

        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AvaloniaTestBase).Assembly);
        if (session == null)
        {
            TestContext.Out.WriteLine("ERROR: Avalonia headless session is null");
            return;
        }

        try
        {
            Bitmap? bitmap = null;
            Exception? captureException = null;
            using var completionSignal = new ManualResetEventSlim(false);

            // Capture must run on the session's thread — the render interface is unavailable on the NUnit thread.
            session.Dispatch(() =>
            {
                try
                {
                    Dispatcher.UIThread.RunJobs();
                    bitmap = window.CaptureRenderedFrame();
                }
                catch (Exception ex)
                {
                    captureException = ex;
                }
                finally
                {
                    completionSignal.Set();
                }
            }, CancellationToken.None);

            if (!completionSignal.Wait(TimeSpan.FromSeconds(5)))
            {
                TestContext.Out.WriteLine("Warning: Screenshot capture timed out after 5 seconds");
                return;
            }

            if (captureException != null)
            {
                throw captureException;
            }

            if (bitmap == null)
            {
                TestContext.Out.WriteLine("Warning: CaptureRenderedFrame() returned null");
                return;
            }

            var testName = TestContext.CurrentContext.Test.Name;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var safeTestName = string.Join("_", testName.Split(Path.GetInvalidFileNameChars()));

            var outputDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFailureScreenshots");
            Directory.CreateDirectory(outputDir);
            var filepath = Path.Combine(outputDir, $"{safeTestName}_{timestamp}.png");

            bitmap.Save(filepath, PngBitmapEncoderOptions.Default);

            TestContext.Out.WriteLine($"Test failure screenshot saved: {filepath}");
            TestContext.AddTestAttachment(filepath, "Test Failure Screenshot");
        }
        catch (Exception ex)
        {
            TestContext.Out.WriteLine($"Failed to capture test failure screenshot: {ex}");
        }
        finally
        {
            CurrentTestWindow = null;   // never leak a window reference into the next test
        }
    }
}
