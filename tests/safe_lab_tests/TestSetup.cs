using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;
using Ihc;
using System.Reflection;
using IhcLab;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Avalonia.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Configure Avalonia test application for headless NUnit testing
[assembly: AvaloniaTestApplication(typeof(Ihc.Tests.TestAppBuilder))]

// Disable parallel test execution for entire assembly due to shared static CurrentTestWindow property
[assembly: NonParallelizable]

namespace Ihc.Tests
{
    /// <summary>
    /// Attribute that automatically captures screenshots when tests fail.
    ///
    /// <para>
    /// This attribute implements NUnit's <see cref="IWrapSetUpTearDown"/> interface to hook into
    /// the test execution pipeline. It works alongside the <c>[AvaloniaTest]</c> attribute by
    /// wrapping the test command and capturing screenshots on failure.
    /// </para>
    ///
    /// <para><strong>Usage:</strong></para>
    /// <code>
    /// [AvaloniaTest]
    /// [CaptureScreenshotOnFailure]
    /// public async Task MyTest()
    /// {
    ///     var window = await new MainWindow().Start();
    ///     CurrentTestWindow = window;  // Register window for capture
    ///
    ///     // Test code - screenshot captured automatically on failure
    /// }
    /// </code>
    ///
    /// <para><strong>Important Limitations:</strong></para>
    /// <list type="bullet">
    /// <item>Must be applied to <strong>each test method individually</strong> - cannot be applied at class/fixture level
    /// (NUnit framework limitation: <see href="https://github.com/nunit/nunit/issues/2220"/>)</item>
    /// <item>Requires <c>[AvaloniaTest]</c> attribute to be present on the same test method</item>
    /// <item>Test must set <c>CurrentTestWindow</c> property for screenshot capture to work</item>
    /// </list>
    ///
    /// <para><strong>Technical Details:</strong></para>
    /// <para>
    /// Screenshot capture executes through Avalonia's headless session dispatcher to ensure
    /// the platform render interface is available. The capture is synchronous and waits up to
    /// 5 seconds for completion.
    /// </para>
    ///
    /// <para>
    /// Screenshots are saved to: <c>tests/safe_lab_tests/bin/Debug/net9.0/TestFailureScreenshots/</c>
    /// with format: <c>{TestName}_{Timestamp}.png</c>
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class CaptureScreenshotOnFailureAttribute : Attribute, NUnit.Framework.Interfaces.IWrapSetUpTearDown
    {
        public NUnit.Framework.Internal.Commands.TestCommand Wrap(NUnit.Framework.Internal.Commands.TestCommand command)
        {
            return new ScreenshotCaptureCommand(command);
        }

        /// <summary>
        /// Custom test command that wraps test execution with screenshot capture on failure.
        /// </summary>
        private class ScreenshotCaptureCommand : NUnit.Framework.Internal.Commands.DelegatingTestCommand
        {
            public ScreenshotCaptureCommand(NUnit.Framework.Internal.Commands.TestCommand innerCommand)
                : base(innerCommand)
            {
            }

            public override NUnit.Framework.Internal.TestResult Execute(NUnit.Framework.Internal.TestExecutionContext context)
            {
                TestResult result;
                Exception? testException = null;

                try
                {
                    // Execute the actual test (which includes AvaloniaTest's command wrapping)
                    result = innerCommand.Execute(context);
                }
                catch (Exception ex)
                {
                    testException = ex;
                    result = context.CurrentResult;
                }

                // Capture screenshot only if test actually failed (not for Inconclusive or Passed)
                // Check result status first to avoid capturing for Assert.Inconclusive() or Assert.Pass()
                bool shouldCaptureScreenshot = result?.ResultState.Status == TestStatus.Failed;

                // Also capture if there's an unhandled exception that's not from Assert.Inconclusive or Assert.Pass
                if (testException != null && !shouldCaptureScreenshot)
                {
                    // Don't capture for test outcome exceptions (Inconclusive, Success)
                    var exceptionType = testException.GetType().Name;
                    shouldCaptureScreenshot = exceptionType != "InconclusiveException" && exceptionType != "SuccessException";
                }

                if (shouldCaptureScreenshot)
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

                // Re-throw original test exception if there was one
                if (testException != null)
                {
                    throw testException;
                }

                return result!;
            }
        }
    }

    /// <summary>
    /// Avalonia test application builder for headless NUnit tests.
    /// This configures the Avalonia application instance used by all [AvaloniaTest] tests.
    /// </summary>
    public class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            // Logger factory is initialized in Setup.RunBeforeAnyTests() which runs first
            if (IhcLab.Program.loggerFactory == null)
            {
                throw new InvalidOperationException(
                    "Logger factory not initialized. Ensure Setup.RunBeforeAnyTests() has run.");
            }

            // Build Avalonia app with headless platform and logging configured
            return AppBuilder.Configure<IhcLab.App>()
                .UseSkia()  // Enable Skia renderer for screenshot capture support in case of errors.
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false  // Use real Skia renderer to enable Window.CaptureRenderedFrame()
                })
                 // Forward to our Nunit test logger (log level here applies to internal Avalonia UI logging only)
                .LogToSink(IhcLab.Program.loggerFactory, LogEventLevel.Information);
        }
    }

    /// <summary>
    /// Custom logger provider that writes to NUnit TestContext for test visibility.
    ///
    /// <para>
    /// This provider writes all log messages to NUnit's TestContext.Out, making them visible in test results.
    /// To temporarily suppress logging in specific tests (e.g., when testing error/warning functionality),
    /// use the <see cref="SuppressLogging"/> helper class.
    /// </para>
    /// </summary>
    public class TestContextLoggerProvider : Microsoft.Extensions.Logging.ILoggerProvider
    {
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return new TestContextLogger(categoryName);
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Custom logger that writes formatted log messages to NUnit TestContext.
    ///
    /// <para>
    /// Formats messages as: <c>{Timestamp} [{LogLevel}] {Category}: {Message}</c>
    /// </para>
    /// <para>
    /// Exception details are written on a separate indented line when present.
    /// This logger is created by <see cref="TestContextLoggerProvider"/> and can be
    /// suppressed using <see cref="SuppressLogging"/> when needed.
    /// </para>
    /// </summary>
    public class TestContextLogger : Microsoft.Extensions.Logging.ILogger
    {
        private readonly string _categoryName;

        public TestContextLogger(string categoryName)
        {
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = $"{DateTime.Now:HH:mm:ss.fff} [{logLevel}] {_categoryName}: {formatter(state, exception)}";

            // Also write to TestContext if available
            try
            {
                TestContext.Out.WriteLine(message);
            }
            catch
            {
                // TestContext might not be available in setup phase, ignore
            }

            if (exception != null)
            {
                var exMsg = $"    Exception: {exception}";
                try
                {
                    TestContext.Out.WriteLine(exMsg);
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Disposable helper that temporarily suppresses logging by replacing the logger factory with <see cref="NullLoggerFactory"/>.
    ///
    /// <para><strong>When to Use:</strong></para>
    /// <para>
    /// Use this helper in tests that intentionally trigger log messages (errors/warnings) as part of their test logic.
    /// This prevents expected test messages from cluttering test output and making it harder to spot real issues.
    /// </para>
    ///
    /// <para><strong>Important - ViewModel Logger Capture:</strong></para>
    /// <para>
    /// ViewModels capture their logger instance in the constructor from <c>Program.loggerFactory</c>.
    /// To suppress ViewModel logging, you must create the ViewModel <strong>inside</strong> the using block
    /// so it captures the null logger. Creating the ViewModel before the using block will not suppress its logging.
    /// </para>
    ///
    /// <para><strong>Restoration:</strong></para>
    /// <para>
    /// The original logger factory is automatically restored when the using block ends (via <see cref="Dispose"/>).
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [Test]
    /// public void SetError_Test()
    /// {
    ///     MainWindowViewModel viewModel;
    ///     using (new SuppressLogging())
    ///     {
    ///         // Create ViewModel INSIDE using block so it captures null logger
    ///         viewModel = new MainWindowViewModel();
    ///         viewModel.SetError("Test error");
    ///     }
    ///     // Original logger restored, assertions can be made here
    ///     Assert.That(viewModel.ErrorWarningText, Is.EqualTo("Test error"));
    /// }
    /// </code>
    /// </example>
    public class SuppressLogging : IDisposable
    {
        private readonly ILoggerFactory? _originalLoggerFactory;

        public SuppressLogging()
        {
            _originalLoggerFactory = IhcLab.Program.loggerFactory;
            IhcLab.Program.loggerFactory = NullLoggerFactory.Instance;
        }

        public void Dispose()
        {
            IhcLab.Program.loggerFactory = _originalLoggerFactory;
        }
    }

    /// <summary>
    /// Base test class for Avalonia UI tests with automatic screenshot capture support.
    /// 
    /// All tests run sequentially due to shared use of static CurrentTestWindow property (setup for entire assembly).
    ///
    /// <para>
    /// This class provides infrastructure for capturing screenshots when tests fail.
    /// Tests inherit from this class and use the <see cref="CaptureScreenshotOnFailureAttribute"/>
    /// to enable automatic screenshot capture.
    /// </para>
    ///
    /// <para><strong>Usage Pattern:</strong></para>
    /// <code>
    /// [AvaloniaTest]
    /// [CaptureScreenshotOnFailure]  // Enables automatic screenshots
    /// public async Task MyTest()
    /// {
    ///     var window = await new MainWindow().Start();
    ///     CurrentTestWindow = window;  // Register window for capture
    ///
    ///     // Test code here - screenshot captured automatically on failure
    /// }
    /// </code>
    ///
    /// <para><strong>Requirements:</strong></para>
    /// <list type="bullet">
    /// <item>Test must set <see cref="CurrentTestWindow"/> property after creating the window</item>
    /// <item><c>[CaptureScreenshotOnFailure]</c> attribute must be applied to each test method</item>
    /// <item>Cannot be applied at class level due to NUnit framework limitation (<see href="https://github.com/nunit/nunit/issues/2220"/>)</item>
    /// </list>
    ///
    /// <para><strong>Screenshot Details:</strong></para>
    /// <para>
    /// Screenshots are captured using Avalonia's <c>CaptureRenderedFrame()</c> method through
    /// the headless session dispatcher. Files are saved to <c>TestFailureScreenshots/</c> directory
    /// in the test output folder with format <c>{TestName}_{Timestamp}.png</c>.
    /// </para>
    /// </summary>
    public abstract class AvaloniaTestBase
    {
        /// <summary>
        /// The window currently being tested. Set this property when creating a window in tests.
        /// Used by automatic screenshot capture on failure.
        /// </summary>
        protected static Window? CurrentTestWindow { get; set; }

        /// <summary>
        /// Common setup method for tests that creates, initializes, shows, and returns a MainWindow.
        /// This method sets <see cref="CurrentTestWindow"/> for automatic screenshot capture on failure.
        /// </summary>
        /// <returns>The initialized and shown MainWindow instance.</returns>
        protected static async Task<MainWindow> SetupMainWindowAsync()
        {
            CurrentTestWindow = await new MainWindow().Start();
            CurrentTestWindow.Show();
            Dispatcher.UIThread.RunJobs();
            return (MainWindow)CurrentTestWindow;
        }

        /// <summary>
        /// Captures a screenshot of the current test window when a test fails.
        ///
        /// <para>
        /// This method is called automatically by <see cref="CaptureScreenshotOnFailureAttribute"/>
        /// when a test fails. It should not be called directly in test code.
        /// </para>
        ///
        /// <para><strong>Technical Implementation:</strong></para>
        /// <para>
        /// The method executes screenshot capture through Avalonia's headless session dispatcher
        /// to ensure the platform render interface is available. It uses <c>ManualResetEventSlim</c>
        /// to synchronously wait up to 5 seconds for the asynchronous capture to complete.
        /// </para>
        ///
        /// <para>
        /// If <see cref="CurrentTestWindow"/> is null, the method logs a warning and returns without
        /// capturing. Screenshots are saved to the <c>TestFailureScreenshots/</c> directory with
        /// timestamped filenames.
        /// </para>
        /// </summary>
        /// <param name="customDescription">Optional custom description for the screenshot attachment in test results.</param>
        internal static void CaptureScreenshotOnFailure(string? customDescription = null)
        {
            if (CurrentTestWindow == null)
            {
                TestContext.Out.WriteLine("No window registered for screenshot capture");
                return;
            }

            // Screenshot capture must happen through the session's dispatcher to ensure render interface is available
            var session = Avalonia.Headless.HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AvaloniaTestBase).Assembly);

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

                // Dispatch screenshot capture on the session's thread where render interface is available
                session.Dispatch(() =>
                {
                    try
                    {
                        Dispatcher.UIThread.RunJobs();
                        bitmap = CurrentTestWindow.CaptureRenderedFrame();
                    }
                    catch (Exception ex)
                    {
                        captureException = ex;
                    }
                    finally
                    {
                        completionSignal.Set(); // Signal that dispatch is complete
                    }
                }, CancellationToken.None);

                // Wait for dispatch to complete
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

                // Create screenshots directory in test output
                var testName = TestContext.CurrentContext.Test.Name;
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var safeTestName = string.Join("_", testName.Split(Path.GetInvalidFileNameChars()));

                var outputDir = Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "TestFailureScreenshots");
                Directory.CreateDirectory(outputDir);

                var filename = $"{safeTestName}_{timestamp}.png";
                var filepath = Path.Combine(outputDir, filename);

                // Save the bitmap as PNG
                bitmap.Save(filepath);

                var description = customDescription ?? "Test Failure Screenshot";
                TestContext.Out.WriteLine($"Test failure screenshot saved: {filepath}");
                TestContext.AddTestAttachment(filepath, description);
            }
            catch (Exception ex)
            {
                TestContext.Out.WriteLine($"Failed to capture test failure screenshot: {ex}");
            }
            finally
            {
                // Clear the window reference for next test
                CurrentTestWindow = null;
            }
        }
    }

    /// <summary>
    /// NUnit SetUpFixture that configures global test infrastructure before any tests run.
    ///
    /// <para><strong>Configuration Responsibilities:</strong></para>
    /// <list type="bullet">
    /// <item>Initializes <c>IhcLab.Program.config</c> with mocked IHC settings for safe testing</item>
    /// <item>Configures <c>IhcLab.Program.loggerFactory</c> with <see cref="TestContextLoggerProvider"/> to route logs to test output</item>
    /// <item>Sets minimum log level to <see cref="LogLevel.Warning"/> to show errors and warnings by default</item>
    /// </list>
    ///
    /// <para><strong>Logger Configuration:</strong></para>
    /// <para>
    /// The logger factory is used by both application code and Avalonia UI framework (configured in <see cref="TestAppBuilder"/>).
    /// Individual tests can temporarily suppress logging using <see cref="SuppressLogging"/> when testing
    /// functionality that intentionally generates log messages.
    /// </para>
    ///
    /// <para><strong>Execution Order:</strong></para>
    /// <para>
    /// This runs once before any tests in the assembly via <c>[OneTimeSetUp]</c>. <see cref="TestAppBuilder"/>
    /// depends on this setup completing first to access the initialized logger factory.
    /// </para>
    /// </summary>
    [SetUpFixture]
    public class Setup
    {
        /// <summary>
        /// Initializes global test configuration and logger factory.
        /// Runs once before any tests in the assembly execute.
        /// </summary>
        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            // Configure IHC settings for mocked tests
            var settings = new IhcSettings()
            {
                Endpoint = SpecialEndpoints.MockedPrefix,
                UserName = "test",
                Password = "test",
                Application = Application.administrator,
                LogSensitiveData = true,
                AsyncContinueOnCapturedContext = false
            };

            IhcLab.Program.config = new Configuration(settings);

            // Setup logger factory with Warning level logging for tests
            // Using custom TestContextLogger to ensure output is visible in test results
            // This is used by both the application code and Avalonia UI (configured in TestAppBuilder)
            IhcLab.Program.loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddProvider(new TestContextLoggerProvider());
                builder.SetMinimumLevel(LogLevel.Warning); // Shows errors and warnings; use SuppressLogging to temporarily suppress in individual tests
            });
        }
    }
}