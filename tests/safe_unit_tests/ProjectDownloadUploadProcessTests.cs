using System.Threading.Tasks;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <c>ihc_ProjectDownloadUpload</c> started with the controller-stub switch, as a process from a clean copy of its
    /// build output. The switch is no part of the command line, so <c>GET &lt;file&gt;</c> beside it is the two
    /// arguments the utility expects; the command line is checked before the settings file is read, so a command line
    /// it cannot use is answered without one; and the switch is applied before any service is built, so every service
    /// talks to the stub.
    /// </summary>
    [TestFixture]
    public class ProjectDownloadUploadProcessTests
    {
        private const string OutputKey = "IhcProjectDownloadUploadOutput";
        private const string Apphost = "ihc_ProjectDownloadUpload";

        [Test]
        public Task WithTheSwitch_TheProjectIsDownloadedFromTheStub() =>
            UtilityProcessRunner.AssertRunsAgainstAStubAsync(OutputKey, Apphost, "GET", "project.vis");

        [Test]
        public Task WithNoCommandAndNoSettingsFile_TheUtilityPrintsItsUsage() =>
            UtilityProcessRunner.AssertAnswersTheUsageWithoutSettingsAsync(OutputKey, Apphost,
                "Expected arguments: 'GET <destfile>' OR 'STORE <sourcefile>'");
    }
}
