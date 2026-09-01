using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// Runs the wrapped test and calls <c>capture</c> when — and only when — it genuinely failed.
    ///
    /// <para>Shared by the two Avalonia suites, which had this twice. The part worth having once is
    /// <see cref="ShouldCapture"/>: an outcome that is neither a pass nor a failure reaches here as an
    /// exception rather than a result, so the decision cannot be read off the result alone. A copy that got
    /// that wrong would not fail a test — it would quietly screenshot every inconclusive run, or none of the
    /// failing ones.</para>
    /// </summary>
    internal sealed class ScreenshotCaptureCommand : DelegatingTestCommand
    {
        private readonly Action capture;

        /// <param name="innerCommand">The test command being wrapped.</param>
        /// <param name="capture">
        /// The suite's own capture entry point. Passed as a delegate because each suite has its own
        /// <c>AvaloniaTestBase</c> holding its own window registration — the decision is shared, the window is not.
        /// </param>
        internal ScreenshotCaptureCommand(TestCommand innerCommand, Action capture) : base(innerCommand)
        {
            this.capture = capture;
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

            if (ShouldCapture(result, testException))
            {
                try
                {
                    capture();
                }
                catch (Exception captureEx)
                {
                    TestContext.Out.WriteLine($"Failed to capture screenshot: {captureEx.Message}");
                }
            }

            // The original outcome is the test's; this wrapper only observes it.
            if (testException != null)
            {
                throw testException;
            }

            return result!;
        }

        /// <summary>
        /// True for a real failure only. <c>Assert.Pass()</c> and <c>Assert.Inconclusive()</c> unwind as
        /// exceptions whose types NUnit does not expose publicly, so the name is what there is to match on.
        /// </summary>
        private static bool ShouldCapture(TestResult? result, Exception? testException)
        {
            if (result?.ResultState.Status == TestStatus.Failed)
            {
                return true;
            }
            if (testException == null)
            {
                return false;
            }
            string exceptionType = testException.GetType().Name;
            return exceptionType != "InconclusiveException" && exceptionType != "SuccessException";
        }
    }
}
