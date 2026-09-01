using System.Collections.Generic;
using System.Linq;
using FakeItEasy;
using Ihc.App;
using IhcLab;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// A selection that the service refuses is contained so the combo box can snap back — but the fault behind it
    /// has to reach the log, and in the shipped build it did not.
    ///
    /// <para><b>The defect this pins.</b> Both selection setters caught the fault and wrote it with
    /// <c>System.Diagnostics.Debug.WriteLine</c>. That method is <c>[Conditional("DEBUG")]</c>, so the compiler
    /// REMOVES the call in a Release build: the catch body reduced to the revert, and the exception's type,
    /// message and stack were gone with no trace anywhere. The only symptom a user or a support case could see
    /// was a control that silently snapped back to its previous value. The comment above it said "Log the
    /// exception for diagnostics", which was true only for a developer running a Debug build.</para>
    ///
    /// <para><b>Why a test can see it at all.</b> The view-model takes its logger from
    /// <c>Program.loggerFactory</c> in its constructor, so swapping that static before construction routes the
    /// record somewhere assertable. That is the same seam the suite's own setup already uses.</para>
    /// </summary>
    [TestFixture]
    public class SelectionFaultLoggingTests
    {
        /// <summary>Collects what the view-model logs, standing in for the real pipeline.</summary>
        private sealed class CapturingProvider : ILoggerProvider
        {
            internal List<string> Entries { get; } = [];

            public ILogger CreateLogger(string categoryName) => new Capturing(Entries);

            public void Dispose()
            {
            }

            private sealed class Capturing(List<string> entries) : ILogger
            {
                public System.IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                    System.Exception? exception, System.Func<TState, System.Exception?, string> formatter)
                {
                    // The EXCEPTION, not merely the rendered message: what the defect lost was the type and the
                    // stack, so a test that only looked at the text could pass on a fix that still dropped them.
                    entries.Add($"{logLevel}|{formatter(state, exception)}|{exception?.GetType().Name}");
                }
            }

            private sealed class NullScope : System.IDisposable
            {
                internal static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }

        /// <summary>A service holding exactly one service with no operations, so any non-zero operation index is refused.</summary>
        private static LabAppService WithOneEmptyService()
        {
            LabAppService service = new(null, null);
            service.Services = [new LabAppService.ServiceItem(A.Fake<IAuthenticationService>(), o => true)];
            return service;
        }

        [Test]
        public void ARefusedOperationSelection_IsLoggedWithItsException()
        {
            ILoggerFactory? original = Program.loggerFactory;
            CapturingProvider captured = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(captured));
            Program.loggerFactory = factory;
            try
            {
                // Constructed AFTER the swap: the logger is captured in the constructor.
                MainWindowViewModel viewModel = new() { LabAppService = WithOneEmptyService() };

                // The service refuses any operation index but 0 when the service has no operations, and the
                // view-model applies this one without a range check of its own.
                viewModel.SelectedOperationIndex = 99;

                Assert.Multiple(() =>
                {
                    Assert.That(captured.Entries.Any(e => e.StartsWith("Error", System.StringComparison.Ordinal)),
                        Is.True, "the refusal must reach the log, and at Error");
                    Assert.That(captured.Entries.Any(e => e.EndsWith("ArgumentOutOfRangeException", System.StringComparison.Ordinal)),
                        Is.True, "carrying the EXCEPTION, so the type and stack survive into the record");
                });
            }
            finally
            {
                Program.loggerFactory = original;
            }
        }

        /// <summary>
        /// The recovery is unchanged: the control still snaps back to what the service actually holds, which is
        /// the behaviour the catch exists for.
        /// </summary>
        [Test]
        public void ARefusedOperationSelection_StillRevertsToTheServicesValue()
        {
            ILoggerFactory? original = Program.loggerFactory;
            using ILoggerFactory factory = LoggerFactory.Create(builder => { });
            Program.loggerFactory = factory;
            try
            {
                LabAppService service = WithOneEmptyService();
                MainWindowViewModel viewModel = new() { LabAppService = service };

                viewModel.SelectedOperationIndex = 99;

                Assert.That(viewModel.SelectedOperationIndex, Is.EqualTo(service.SelectedOperationIndex),
                    "the view-model resynchronises with the service rather than keeping the refused value");
            }
            finally
            {
                Program.loggerFactory = original;
            }
        }
    }
}
