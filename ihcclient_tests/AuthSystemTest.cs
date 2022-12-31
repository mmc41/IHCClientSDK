using NUnit.Framework;
using System;
using System.Linq;
using System.IO;
using Ihc;
using System.Threading.Tasks;

/// <summary>
/// System authentication and basic access tests against live IHC system. Requires use of test name/password specified in configuration file.
/// </summary>
namespace Ihc.Tests
{
    [NonParallelizable]
    public class AuthTest
    { 
        [Test]
        public async Task AuthenticateAndDisconnectTest()
        {
            var authService = new AuthenticationService(Setup.logger, Setup.endpoint);

            var result = await authService.Authenticate(Setup.userName, Setup.password, Setup.application);
            Assert.That(result.Username, Is.EqualTo(Setup.userName));

            var disResult = await authService.Disconnect();
            Assert.That(disResult, Is.EqualTo(true));
        }

        [Test]
        public async Task PingTest()
        {
            var authService = new AuthenticationService(Setup.logger, Setup.endpoint);

            var pingResult = await authService.Ping();
            Assert.That(pingResult, Is.EqualTo(true));
        }
    }
}