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
    [TestFixture]
    public class AuthTest
    { 
        [Test]
        public async Task AuthenticateAndDisconnectTest()
        {
            var authService = new AuthenticationService(Setup.settings);

            var result = await authService.Authenticate(Setup.settings.UserName, Setup.settings.Password,  Setup.settings.Application);
            if (Setup.settings.Endpoint!="http://usb") // Username can't be tested like this when using a usb connection, where "usb" is returned istead of name.
            {
                Assert.That(result.Username, Is.EqualTo(Setup.settings.UserName));
            }

            var disResult = await authService.Disconnect();
            Assert.That(disResult, Is.EqualTo(true));
        }

        [Test]
        public async Task PingTest()
        {
            var authService = new AuthenticationService(Setup.settings);

            var pingResult = await authService.Ping();
            Assert.That(pingResult, Is.EqualTo(true));
        }
    }
}