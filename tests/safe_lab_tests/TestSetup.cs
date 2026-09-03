using System;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Ihc;
using Ihc.Bootstrap;
using IhcLab;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
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
    public sealed class TestContextLoggerProvider : Microsoft.Extensions.Logging.ILoggerProvider
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
    public sealed class SuppressLogging : IDisposable
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