using System;
using Avalonia.Controls;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal.Commands;
using Ihc.Tests.Shared;

// Not Ihc.Tests.Shared, where the rest of tests/shared lives, because these two types must be PUBLIC -- a
// public fixture cannot derive from a less accessible base -- and CA1716 refuses a public type in a
// namespace segment named for a reserved word. Every other shared helper is internal, so the rule never
// reached them.
namespace Ihc.Tests
{
    /// <summary>
    /// Captures a screenshot of <see cref="AvaloniaTestBase.CurrentTestWindow"/> when a test fails, handing
    /// the window to <see cref="HeadlessScreenshot.CaptureOnFailure"/>, which owns the capture contract.
    ///
    /// <para>Apply it together with <c>[AvaloniaTest]</c> on EACH test method: NUnit cannot apply an
    /// <see cref="IWrapSetUpTearDown"/> attribute at class or fixture level
    /// (<see href="https://github.com/nunit/nunit/issues/2220"/>). The test registers the window it shows
    /// via <see cref="AvaloniaTestBase.CurrentTestWindow"/>; without that the capture is a logged no-op.</para>
    ///
    /// <para>The capture is reached through a lambda rather than a method group because
    /// <see cref="AvaloniaTestBase.CaptureScreenshotOnFailure"/> carries an optional parameter, and C# does
    /// not fill optional parameters in a method-group conversion.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class CaptureScreenshotOnFailureAttribute : Attribute, IWrapSetUpTearDown
    {
        public TestCommand Wrap(TestCommand command) =>
            new ScreenshotCaptureCommand(command, () => AvaloniaTestBase.CaptureScreenshotOnFailure());
    }

    /// <summary>
    /// Base class for Avalonia UI tests wanting automatic failure screenshots: set
    /// <see cref="CurrentTestWindow"/> to the (shown) window under test and mark the test with
    /// <see cref="CaptureScreenshotOnFailureAttribute"/>.
    ///
    /// <para>The registration is a shared static, and what keeps it honest is not
    /// <c>[assembly: NonParallelizable]</c> but Avalonia's own dispatcher: every write happens inside an
    /// <c>[AvaloniaTest]</c>, and a suite has one headless dispatcher thread those tests are queued onto. The
    /// CAPTURE is the half that would not be safe, because <see cref="CaptureScreenshotOnFailureAttribute"/>
    /// is an <c>IWrapSetUpTearDown</c> and so wraps the OUTSIDE of that dispatch, running on the NUnit worker
    /// thread once the test body has returned. Run in parallel it could read a window a later test has since
    /// registered — which costs a wrong or missing screenshot on an already-failing test, never a verdict.</para>
    ///
    /// </summary>
    public abstract class AvaloniaTestBase
    {
        /// <summary>The window currently under test; registered by each test for failure capture.</summary>
        /// <remarks>
        /// <c>internal</c> as well as <c>protected</c> so a shared rig — which is not itself a fixture and so
        /// cannot reach a protected member — can register the window it shows. The capture is a shared static
        /// either way.
        /// </remarks>
        protected internal static Window? CurrentTestWindow { get; set; }

        /// <summary>
        /// Called by <see cref="CaptureScreenshotOnFailureAttribute"/> on failure; not to be called from test
        /// code. Delegates to <see cref="HeadlessScreenshot.CaptureOnFailure"/>, which owns the capture
        /// contract, and clears <see cref="CurrentTestWindow"/> so no window leaks into the next test.
        /// </summary>
        /// <param name="customDescription">Optional description for the attachment in the test result.</param>
        internal static void CaptureScreenshotOnFailure(string? customDescription = null)
        {
            try
            {
                HeadlessScreenshot.CaptureOnFailure(
                    CurrentTestWindow, typeof(AvaloniaTestBase).Assembly, customDescription);
            }
            finally
            {
                CurrentTestWindow = null;
            }
        }
    }
}
