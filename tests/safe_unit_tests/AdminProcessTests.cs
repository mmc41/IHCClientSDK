using System.Threading.Tasks;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <c>ihc_admin</c> started with the controller-stub switch, as a process from a clean copy of its build output.
    /// The switch is no part of the command line, so <c>GET &lt;file&gt;</c> beside it is the two arguments the utility
    /// expects, and the command line is checked before the settings file is read, so a command line the utility cannot
    /// use is answered without one.
    /// </summary>
    [TestFixture]
    public class AdminProcessTests
    {
        private const string OutputKey = "IhcAdminOutput";
        private const string Apphost = "ihc_admin";

        [Test]
        public Task WithTheSwitch_AdminGetsItsSetupFromTheStub() =>
            UtilityProcessRunner.AssertRunsAgainstAStubAsync(OutputKey, Apphost, "GET", "admin.json");

        [Test]
        public Task WithNoCommandAndNoSettingsFile_AdminPrintsItsUsage() =>
            UtilityProcessRunner.AssertAnswersTheUsageWithoutSettingsAsync(OutputKey, Apphost,
                "Expected arguments: 'GET <destfile>' OR 'STORE <sourcefile>'");
    }
}
