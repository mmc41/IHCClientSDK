using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Bootstrap;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>What one utility process left behind.</summary>
    /// <param name="ExitCode">The process's exit code.</param>
    /// <param name="StandardError">Its standard error, one entry per line.</param>
    /// <param name="StandardOutput">Its standard output, kept to explain a failure.</param>
    internal sealed record UtilityRun(int ExitCode, IReadOnlyList<string> StandardError, string StandardOutput);

    /// <summary>
    /// A utility started by <see cref="UtilityProcessRunner.StartAsync"/> that runs until it is stopped. Disposing it
    /// stops it, if it is still running, and removes its copy.
    /// </summary>
    internal sealed class RunningUtility : IAsyncDisposable
    {
        private readonly Process process;
        private readonly DirectoryInfo copy;
        private readonly Task<string> rest;
        private readonly Task<string> errors;

        internal RunningUtility(Process process, DirectoryInfo copy, string firstLine)
        {
            this.process = process;
            this.copy = copy;
            FirstLine = firstLine;
            // Both pipes are drained from now on, so a utility that keeps writing never blocks on a full pipe.
            rest = process.StandardOutput.ReadToEndAsync();
            errors = process.StandardError.ReadToEndAsync();
        }

        /// <summary>The first line the utility wrote to standard output.</summary>
        internal string FirstLine { get; }

        /// <summary>Stops the utility and answers everything it wrote after its first line: standard output, then
        /// standard error.</summary>
        internal async Task<string> StopAsync()
        {
            Stop();
            await process.WaitForExitAsync();
            return await rest + await errors;
        }

        public async ValueTask DisposeAsync()
        {
            Stop();
            await process.WaitForExitAsync();
            process.Dispose();
            await UtilityProcessRunner.RemoveCopyAsync(copy);
        }

        private void Stop()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    /// <summary>
    /// Starts a console utility under the controller-stub switch from a clean copy of its build output. Run from its
    /// own output folder, a utility would read the settings file staged there, which is the developer's real one; the
    /// copy holds a settings file this runner writes instead, or none at all for a run that shows what the utility does
    /// before it reads one.
    /// </summary>
    internal static class UtilityProcessRunner
    {
        /// <summary>
        /// The endpoint the written file names. It is a real-controller host, not a stub one: only the switch can then
        /// make the utility announce a stub, so a utility that dropped the switch fails rather than passing on the
        /// file's own endpoint.
        /// </summary>
        internal const string FileEndpoint = "http://no-stub-switch.invalid";

        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

        /// <summary>The exit code a console utility answers a command line it cannot use with.</summary>
        private const int UsageExitCode = 2;

        /// <summary>
        /// The written settings file. Every console loader reads the telemetry section and refuses to start without
        /// one, so it is present, and its endpoints are empty, which turns telemetry off.
        /// </summary>
        private const string SettingsFile = """
            {
              "ihcclient": {
                "endpoint": "http://no-stub-switch.invalid",
                "userName": "file-user",
                "password": "file-password"
              },
              "telemetry": {
                "host": "",
                "traces": "",
                "logs": "",
                "metrics": "",
                "environment": "utility-process-test"
              }
            }
            """;

        /// <summary>
        /// Runs the utility built to the folder that <paramref name="outputKey"/> names, with
        /// <c>--controller-stub=<paramref name="switchValue"/></c> and then <paramref name="arguments"/>.
        /// </summary>
        /// <param name="outputKey">The assembly-metadata key holding the utility's output folder.</param>
        /// <param name="apphost">The utility's executable name, without an extension.</param>
        /// <param name="switchValue">The switch's value.</param>
        /// <param name="arguments">The utility's own arguments.</param>
        internal static Task<UtilityRun> RunAsync(string outputKey, string apphost, string switchValue,
                                                 params string[] arguments) =>
            RunInCopyAsync(outputKey, apphost, withSettingsFile: true, switchValue, arguments);

        /// <summary>
        /// Runs the utility as <see cref="RunAsync"/> does, from a copy that holds the written settings file only when
        /// <paramref name="withSettingsFile"/> is set.
        /// </summary>
        private static async Task<UtilityRun> RunInCopyAsync(string outputKey, string apphost, bool withSettingsFile,
                                                             string switchValue, string[] arguments)
        {
            DirectoryInfo copy = Directory.CreateTempSubdirectory("ihc_utility_");
            try
            {
                using Process process = Start(copy, outputKey, apphost, withSettingsFile,
                    [ControllerStubs.SwitchArgument + "=" + switchValue, .. arguments]);
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                using (CancellationTokenSource limit = new(Limit))
                {
                    try
                    {
                        await process.WaitForExitAsync(limit.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Well inside the run's hang timeout, so a hung utility fails this test instead of the run.
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync();
                        Assert.Fail($"{apphost} did not exit within {Limit.TotalSeconds} s and was killed");
                    }
                }
                string[] errorLines = (await error).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.TrimEnd('\r'))
                    .ToArray();
                return new UtilityRun(process.ExitCode, errorLines, await output);
            }
            finally
            {
                await RemoveCopyAsync(copy);
            }
        }

        /// <summary>
        /// Removes a utility's copy, retrying while Windows still holds the executable of a process that has just
        /// exited, which it can for a moment under load. A copy that outlasts the retries stays in the temporary
        /// folder: leftover scratch is no reason to fail the test that made it.
        /// </summary>
        internal static async Task RemoveCopyAsync(DirectoryInfo copy)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    copy.Delete(recursive: true);
                    return;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    if (attempt == 20)
                    {
                        TestContext.Out.WriteLine($"Left the utility copy {copy.FullName}: {error.Message}");
                        return;
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }
        }

        /// <summary>
        /// Runs the utility against a fresh loopback stub, and asserts what every switched utility owes: the stub line
        /// before any other standard-error output, a switch that identified the host, calls that reached the stub, a
        /// login, where there is one, presenting the switch's user, and a successful exit.
        /// </summary>
        /// <param name="outputKey">The assembly-metadata key holding the utility's output folder.</param>
        /// <param name="apphost">The utility's executable name, without an extension.</param>
        /// <param name="arguments">The utility's own arguments; a relative path lands in the temporary copy.</param>
        internal static async Task AssertRunsAgainstAStubAsync(string outputKey, string apphost, params string[] arguments)
        {
            ControllerStub stub = new();
            await using ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(stub, CancellationToken.None);

            UtilityRun run = await RunAsync(outputKey, apphost, host.BaseAddress.ToString(), arguments);

            Assert.Multiple(() =>
            {
                Assert.That(run.StandardError.FirstOrDefault(), Does.StartWith(ControllerStubConsole.LinePrefix),
                    "the stub line comes before any other standard-error output");
                Assert.That(host.IdentifyRequests, Is.GreaterThanOrEqualTo(1), "the switch identified this host");
                Assert.That(stub.Journal.All, Is.Not.Empty, "the utility's calls reached the stub");
                // A login's journal entry carries the user name it presented, and nothing else.
                Assert.That(stub.Journal.All.Where(call => call.SoapAction == "authenticate").Select(call => call.Payload),
                    Is.Not.Empty.And.All.EqualTo(ControllerStubSwitch.SwitchUserName),
                    "the utility logs in, and as the switch's user, never the file's");
                Assert.That(run.ExitCode, Is.Zero, run.StandardOutput);
            });
        }

        /// <summary>
        /// Runs the utility under the switch to a fresh loopback stub, with <paramref name="arguments"/> and from a copy
        /// that holds no settings file, and asserts what a command line the utility cannot use owes even then:
        /// <paramref name="usage"/> on standard output, the usage exit code, and nothing attempted, not even the
        /// switch's identity check. A utility that reads its settings file before it checks its command line fails here.
        /// </summary>
        /// <param name="outputKey">The assembly-metadata key holding the utility's output folder.</param>
        /// <param name="apphost">The utility's executable name, without an extension.</param>
        /// <param name="usage">What the utility prints about its command line.</param>
        /// <param name="arguments">The utility's own arguments.</param>
        internal static async Task AssertAnswersTheUsageWithoutSettingsAsync(string outputKey, string apphost, string usage,
                                                                           params string[] arguments)
        {
            ControllerStub stub = new();
            await using ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(stub, CancellationToken.None);

            UtilityRun run = await RunInCopyAsync(outputKey, apphost, withSettingsFile: false, host.BaseAddress.ToString(),
                arguments);

            Assert.Multiple(() =>
            {
                Assert.That(run.StandardOutput, Does.Contain(usage), "the utility says what its command line must be");
                Assert.That(host.IdentifyRequests, Is.Zero, "nothing is attempted, not even the switch's identity check");
                Assert.That(run.ExitCode, Is.EqualTo(UsageExitCode),
                    run.StandardOutput + string.Join(Environment.NewLine, run.StandardError));
            });
        }

        /// <summary>
        /// Starts a utility that runs until it is stopped, from a clean copy of its build output, and answers once it
        /// has written its first line of standard output.
        /// </summary>
        /// <param name="outputKey">The assembly-metadata key holding the utility's output folder.</param>
        /// <param name="apphost">The utility's executable name, without an extension.</param>
        internal static async Task<RunningUtility> StartAsync(string outputKey, string apphost)
        {
            DirectoryInfo copy = Directory.CreateTempSubdirectory("ihc_utility_");
            Process? process = null;
            try
            {
                process = Start(copy, outputKey, apphost, withSettingsFile: true, []);
                using CancellationTokenSource limit = new(Limit);
                string firstLine = await process.StandardOutput.ReadLineAsync(limit.Token)
                    ?? throw new InvalidOperationException($"{apphost} ended before writing a line");
                return new RunningUtility(process, copy, firstLine);
            }
            catch
            {
                process?.Kill(entireProcessTree: true);
                process?.Dispose();
                await RemoveCopyAsync(copy);
                throw;
            }
        }

        /// <summary>Copies the utility's build output into <paramref name="copy"/>, with the written settings file when
        /// <paramref name="withSettingsFile"/> is set, and starts it there with <paramref name="arguments"/>.</summary>
        private static Process Start(DirectoryInfo copy, string outputKey, string apphost, bool withSettingsFile,
                                     IEnumerable<string> arguments)
        {
            CleanCopy.Mirror(new DirectoryInfo(OutputFolder(outputKey)), copy);
            if (withSettingsFile)
            {
                File.WriteAllText(Path.Combine(copy.FullName, IhcConfiguration.SettingsFileName), SettingsFile);
            }

            ProcessStartInfo start = new(Path.Combine(copy.FullName, OperatingSystem.IsWindows() ? apphost + ".exe" : apphost))
            {
                WorkingDirectory = copy.FullName,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }
            return Process.Start(start) ?? throw new InvalidOperationException("The utility did not start: " + start.FileName);
        }

        private static string OutputFolder(string key) =>
            typeof(UtilityProcessRunner).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == key).Value
            ?? throw new InvalidOperationException("No output folder recorded under " + key);
    }
}
