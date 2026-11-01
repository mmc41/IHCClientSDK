using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Ihc.Stubbing;
using Ihc.Vis;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The controller service's state and one-call project operations, answered from one coherent default
    /// controller and read through the real <see cref="ControllerService"/>.
    /// </summary>
    [TestFixture]
    public class StubControllerTests
    {
        /// <summary>CRLF and LF line endings and ISO-8859-1 letters, which an upload must keep byte for byte.</summary>
        private static readonly ProjectFile Uploaded = new("uploaded.vis",
            "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<utcs_project>\r\n  <name>Køkken og Spisestue æøå ÆØÅ</name>\n</utcs_project>\r\n");

        [Test]
        public async Task TheDefaultController_IsReady_WithAValidProject()
        {
            using var sdk = StubbedSdk.Start();

            ProjectFile? project = await sdk.Controller.GetProject();

            Assert.That(await sdk.Controller.GetControllerState(), Is.EqualTo(ControllerState.Ready));
            Assert.That(await sdk.Controller.IsSDCardReady(), Is.True);
            Assert.That(await sdk.Controller.IsIHCProjectAvailable(), Is.True);
            Assert.That(project?.Filename, Is.EqualTo("stub.vis"));
            using var bytes = new MemoryStream(ProjectFile.Encoding.GetBytes(project!.Data));
            Assert.That(async () => await new ProjectAppService(new IhcSettings()).Load(bytes), Throws.Nothing,
                "the default project is one the SDK's own loader reads");
        }

        /// <summary>The default project is stamped by the stub's fixed clock, not by the process's: created and saved
        /// at 2000-01-01 00:00:00, which packs to day 1 and nothing else.</summary>
        [Test]
        public void TheDefaultProject_IsDeterministic()
        {
            string data = new ControllerStub().Controller.Project!.Data;

            Assert.That(data, Does.Contain("id1=\"_0x1000000\"").And.Contain("id2=\"_0x1000000\""));
            Assert.That(data, Does.Match("<modified year=\"2000\" month=\"1\" day=\"1\" hour=\"0\" minute=\"0\""));
        }

        [Test]
        public async Task TheDefaultProjectInfo_DescribesTheDefaultProject()
        {
            using var sdk = StubbedSdk.Start();

            ProjectInfo? info = await sdk.Controller.GetProjectInfo();

            Assert.That(info, Is.EqualTo(sdk.Stub.Controller.ProjectInfo));
            Assert.That((info!.VisualMajorVersion, info.InstallerName, info.Lastmodified),
                Is.EqualTo((4, "Stub Installer", new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero))));
            Assert.That(sdk.Stub.Controller.Project!.Data, Does.Contain("version_major=\"4\"").And.Contain("Stub Installer"));
        }

        [Test]
        public async Task AOneCallStore_ThenGet_RoundTripsByteForByte()
        {
            using var sdk = StubbedSdk.Start();

            bool stored = await sdk.Controller.StoreProject(Uploaded);
            ProjectFile? fetched = await sdk.Controller.GetProject();

            Assert.That(stored, Is.True);
            Assert.That(ProjectFile.Encoding.GetBytes(fetched!.Data), Is.EqualTo(ProjectFile.Encoding.GetBytes(Uploaded.Data)));
            Assert.That(fetched.Filename, Is.EqualTo(Uploaded.Filename));
            Assert.That(sdk.Stub.Journal.Calls<IControllerService>(nameof(IControllerService.StoreProject)).Single().Payload,
                Is.EqualTo(Uploaded), "the journal holds the decoded project, not its gzip");
        }

        /// <summary>One upload sends its wire actions in the order the operation table lists for it, read by SOAP
        /// action because a step is journalled under the method that owns it.</summary>
        [Test]
        public async Task OneUpload_IsJournalledInOrder()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.Controller.StoreProject(Uploaded);

            Assert.That(sdk.Stub.Journal.All.Select(call => call.SoapAction),
                Is.EqualTo(SdkOperations.Entry(typeof(IControllerService), nameof(IControllerService.StoreProject))!.WireActions));
            Assert.That(sdk.Stub.Controller.State, Is.EqualTo(ControllerState.Ready), "the upload left change mode");
        }

        [Test]
        public async Task ADeclinedStore_AnswersFalse_AndLeavesTheProject()
        {
            var stub = new ControllerStub();
            ProjectFile before = stub.Controller.Project!;
            stub.Controller.StoreProjectAccepted = false;
            using var sdk = StubbedSdk.Start(stub);

            bool stored = await sdk.Controller.StoreProject(Uploaded);

            Assert.That(stored, Is.False);
            Assert.That(await sdk.Controller.GetProject(), Is.EqualTo(before));
        }

        [Test]
        public async Task AFailedStore_RaisesTheProductionError_AndLeavesTheProject()
        {
            var stub = new ControllerStub();
            ProjectFile before = stub.Controller.Project!;
            stub.FailOn<IControllerService>(nameof(IControllerService.StoreProject));
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(async () => await sdk.Controller.StoreProject(Uploaded), Throws.TypeOf<HttpRequestException>());

            Assert.That(await sdk.Controller.GetProject(), Is.EqualTo(before));
            Assert.That(await sdk.Controller.GetControllerState(), Is.EqualTo(ControllerState.Ready),
                "the upload's cleanup still left change mode");
        }

        [Test]
        public async Task ChangeMode_MovesTheState_AndAWaitAnswersTheCurrentStateAtOnce()
        {
            using var sdk = StubbedSdk.Start();

            bool entered = await sdk.Controller.EnterProjectChangeMode();
            ControllerState inChange = await sdk.Controller.WaitForControllerStateChange(ControllerState.Ready, 10);
            bool exited = await sdk.Controller.ExitProjectChangeMode();

            Assert.That((entered, inChange, exited), Is.EqualTo((true, ControllerState.Initialize, true)));
            Assert.That(await sdk.Controller.GetControllerState(), Is.EqualTo(ControllerState.Ready));
        }

        [Test]
        public async Task AControllerWithNoProject_ReportsNone()
        {
            var stub = new ControllerStub();
            stub.Controller.Project = null;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Controller.IsIHCProjectAvailable(), Is.False);
            Assert.That(await sdk.Controller.GetProject(), Is.Null);
            Assert.That(await sdk.Controller.GetProjectInfo(), Is.Null);
        }

        /// <summary>Each controller state a test can set reads back through the SDK's own state mapper.</summary>
        [TestCase(ControllerState.Uninitialized)]
        [TestCase(ControllerState.Initialize)]
        [TestCase(ControllerState.Failed)]
        [TestCase(ControllerState.RfConfiguration)]
        [TestCase(ControllerState.Simulation)]
        [TestCase(ControllerState.Unknown)]
        public async Task ATypedState_RoundTripsThroughTheRealWrapper(ControllerState state)
        {
            var stub = new ControllerStub();
            stub.Controller.State = state;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Controller.GetControllerState(), Is.EqualTo(state));
            Assert.That(await sdk.Controller.WaitForControllerStateChange(ControllerState.Ready, 1), Is.EqualTo(state));
        }

        [Test]
        public async Task TypedProjectValues_RoundTripThroughTheRealWrapper()
        {
            var info = new ProjectInfo
            {
                VisualMajorVersion = 3,
                VisualMinorVersion = 7,
                ProjectMajorRevision = 12,
                ProjectMinorRevision = 34,
                Lastmodified = new DateTimeOffset(2019, 3, 4, 5, 6, 7, TimeSpan.FromHours(1)),
                ProjectNumber = "P-42",
                CustomerName = "Kunde",
                InstallerName = "Installatør",
            };
            var stub = new ControllerStub();
            stub.Controller.ProjectInfo = info;
            stub.Controller.Project = Uploaded;
            stub.Controller.SdCardReady = false;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Controller.GetProjectInfo(), Is.EqualTo(info));
            Assert.That(await sdk.Controller.GetProject(), Is.EqualTo(Uploaded));
            Assert.That(await sdk.Controller.IsSDCardReady(), Is.False);
        }
    }
}
