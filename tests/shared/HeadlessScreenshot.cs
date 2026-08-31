#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NUnit.Framework;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// Captures a failing test's window to <c>TestFailureScreenshots/</c> and attaches it to the test result.
    ///
    /// <para>Shared because the two Avalonia suites had this twice and the copies had drifted. The code is the
    /// kind where a mistake is a deadlock or a swallowed exception rather than a red test: a cross-thread
    /// dispatch onto the headless session, a <see cref="ManualResetEventSlim"/>, a five-second timeout and an
    /// exception marshalled back across the boundary. A fix applied to one copy left the other broken, which is
    /// exactly what had happened — the later copy had taken two hardenings the original never got, both
    /// preserved here.</para>
    /// </summary>
    internal static class HeadlessScreenshot
    {
        /// <summary>How long to wait for the session thread to finish rendering before giving up.</summary>
        private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Captures <paramref name="window"/> and attaches the PNG to the current test result. Never throws:
        /// a capture failure must not replace the assertion failure that prompted it, so every fault is
        /// reported to the test output and swallowed.
        /// </summary>
        /// <param name="window">The window under test, or null when the test registered none.</param>
        /// <param name="sessionAssembly">
        /// The suite's own assembly — <see cref="HeadlessUnitTestSession.GetOrStartForAssembly"/> keys the
        /// session on it, so each suite must pass its own or it would attach to the wrong session.
        /// </param>
        /// <param name="description">Attachment description; defaults to "Test Failure Screenshot".</param>
        internal static void CaptureOnFailure(Window? window, Assembly sessionAssembly, string? description = null)
        {
            if (window == null)
            {
                TestContext.Out.WriteLine("No window registered for screenshot capture");
                return;
            }

            HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(sessionAssembly);
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

                // Capture must run on the session's thread — the render interface is unavailable on the NUnit
                // thread. `window` is the parameter, not the caller's mutable static: a copy is what keeps the
                // null check above true inside the lambda.
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

                if (!completionSignal.Wait(CaptureTimeout))
                {
                    TestContext.Out.WriteLine(
                        $"Warning: Screenshot capture timed out after {CaptureTimeout.TotalSeconds} seconds");
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

                string filepath = Path.Combine(EnsureOutputDirectory(), FileName());
                bitmap.Save(filepath, PngBitmapEncoderOptions.Default);

                TestContext.Out.WriteLine($"Test failure screenshot saved: {filepath}");
                TestContext.AddTestAttachment(filepath, description ?? "Test Failure Screenshot");
            }
            catch (Exception ex)
            {
                TestContext.Out.WriteLine($"Failed to capture test failure screenshot: {ex}");
            }
        }

        private static string EnsureOutputDirectory()
        {
            string outputDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFailureScreenshots");
            Directory.CreateDirectory(outputDir);
            return outputDir;
        }

        // Invariant: a screenshot filename is a sortable machine key, not display text. Under a non-Gregorian
        // default calendar (ar-SA, th-TH) the ambient culture would stamp a different YEAR into the name, so two
        // runs on differently-configured machines would not sort or diff against each other.
        private static string FileName()
        {
            string testName = TestContext.CurrentContext.Test.Name;
            string safeTestName = string.Join("_", testName.Split(Path.GetInvalidFileNameChars()));
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            return $"{safeTestName}_{timestamp}.png";
        }
    }
}
