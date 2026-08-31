using System;
using Avalonia.Controls;
using Ihc.Tests.Shared;
using NUnit.Framework.Interfaces;
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
    public TestCommand Wrap(TestCommand command) =>
        new ScreenshotCaptureCommand(command, AvaloniaTestBase.CaptureScreenshotOnFailure);
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
    /// <remarks>
    /// <c>internal</c> as well as <c>protected</c> so a shared rig — which is not itself a fixture and so cannot
    /// reach a protected member — can register the window it shows. The capture is a shared static either way.
    /// </remarks>
    protected internal static Window? CurrentTestWindow { get; set; }

    /// <summary>
    /// Called by <see cref="CaptureScreenshotOnFailureAttribute"/> on failure: renders
    /// <see cref="CurrentTestWindow"/> via the headless session's dispatcher (where the Skia render interface
    /// lives), saves a timestamped PNG under <c>TestFailureScreenshots/</c> and attaches it to the test result.
    /// A no-op (with a log line) when no window was registered.
    /// </summary>
    internal static void CaptureScreenshotOnFailure()
    {
        try
        {
            HeadlessScreenshot.CaptureOnFailure(CurrentTestWindow, typeof(AvaloniaTestBase).Assembly);
        }
        finally
        {
            CurrentTestWindow = null;   // never leak a window reference into the next test
        }
    }
}
