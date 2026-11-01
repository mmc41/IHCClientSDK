using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Ihc.Bootstrap;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <c>ihc_info</c> started with the controller-stub switch, as a process from a clean copy of its build output:
    /// it announces the stub before anything else, reaches only the stub, and reports a refused switch through its
    /// exit code.
    /// </summary>
    [TestFixture]
    public class InfoProcessTests
    {
        private const string OutputKey = "IhcInfoOutput";
        private const string Apphost = "ihc_info";

        [Test]
        public Task WithTheSwitch_InfoRunsAgainstTheStub_AnnouncingItFirst() =>
            UtilityProcessRunner.AssertRunsAgainstAStubAsync(OutputKey, Apphost);

        /// <summary>
        /// A switch value nothing answers is refused before any service is built: no stub line, and an exit code that
        /// tells whatever started the utility that it failed.
        /// </summary>
        [Test]
        public async Task WithNoHostListening_NoStubLineIsPrinted_AndTheRunFails()
        {
            UtilityRun run = await UtilityProcessRunner.RunAsync(OutputKey, Apphost, $"http://127.0.0.1:{ClosedPort()}/s1/");

            Assert.Multiple(() =>
            {
                Assert.That(run.StandardError, Has.None.StartsWith(ControllerStubConsole.LinePrefix));
                Assert.That(run.ExitCode, Is.Not.Zero, run.StandardOutput);
            });
        }

        /// <summary>
        /// The utility takes no argument but the switch. One it ignored could be a mistyped option the person
        /// meant something by, so it is refused with the usage before anything is read or sent.
        /// </summary>
        [Test]
        public Task AnArgumentOtherThanTheSwitch_IsRefusedWithTheUsage_BeforeAnythingIsAttempted() =>
            UtilityProcessRunner.AssertAnswersTheUsageWithoutSettingsAsync(OutputKey, Apphost,
                "Expected no arguments but " + Ihc.Stubbing.ControllerStubs.SwitchArgument, "--verbose");

        /// <summary>A loopback port that was just free, and that nothing listens on once this returns.</summary>
        private static int ClosedPort()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
