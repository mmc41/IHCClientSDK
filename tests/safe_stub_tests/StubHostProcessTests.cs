using System;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <c>ihcclient_stub</c> run as a person runs it: a process serving a default stub, whose first line is the switch
    /// to start an application with, and which then prints one line per call and nothing secret.
    /// </summary>
    [TestFixture]
    public class StubHostProcessTests
    {
        private const string Password = "stub-host-process-password";

        [Test]
        public async Task TheHost_PrintsTheSwitchFirst_AndAnswersACallMadeThroughIt()
        {
            await using RunningUtility running = await UtilityProcessRunner.StartAsync("IhcClientStubOutput", "ihcclient_stub");
            string prefix = ControllerStubs.SwitchArgument + "=";
            Assert.That(running.FirstLine, Does.StartWith(prefix + "http://127.0.0.1:"),
                "the switch is the first line, with no hosting log before it");

            bool loggedIn;
            using (ControllerStubRegistration registration =
                await ControllerStubs.RegisterLoopbackAsync(new Uri(running.FirstLine[prefix.Length..]), CancellationToken.None))
            {
                loggedIn = await ControllerStubSwitchTests.LoginFrom(
                    new IhcSettings { Endpoint = registration.Endpoint, UserName = "stub-host-user", Password = Password });
            }
            string output = await running.StopAsync();

            Assert.Multiple(() =>
            {
                Assert.That(loggedIn, Is.True, "the host answered the login");
                Assert.That(output, Does.Contain("call AuthenticationService.authenticate"), "and printed the call");
                Assert.That(output, Does.Not.Contain(Password), "but never the password it carried");
                Assert.That(output, Does.Not.Contain("stub-host-user"), "or any other payload value");
            });
        }
    }
}
