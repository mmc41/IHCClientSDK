using NUnit.Framework;
using System.Net.Http;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc;
using Ihc.Soap.Controller;

namespace Ihc.Tests
{
    /// <summary>
    /// <see cref="ControllerService.StoreProject"/> failure semantics against a faked SOAP layer:
    /// a transport failure mid-upload must surface as the original exception (never replaced by the
    /// change-mode cleanup's own failure), and non-Latin-1 project text must fail before the controller
    /// is put into change mode instead of being silently stored as '?' replacements.
    /// </summary>
    [TestFixture]
    public class ControllerServiceStoreProjectTests
    {
        private static ControllerService NewService(Ihc.Soap.Controller.ControllerService soap)
        {
            var settings = new IhcSettings { Endpoint = "http://unit.test.local" };
            var auth = A.Fake<IAuthenticationService>();
            A.CallTo(() => auth.IhcSettings).Returns(settings);
            return new ControllerService(auth, soap);
        }

        private static Ihc.Soap.Controller.ControllerService HappySoap()
        {
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            A.CallTo(() => soap.getStateAsync(A<inputMessageName1>._))
                .Returns(Task.FromResult(new outputMessageName1(new WSControllerState { state = "text.ctrl.state.ready" })));
            A.CallTo(() => soap.isSDCardReadyAsync(A<inputMessageName9>._))
                .Returns(Task.FromResult(new outputMessageName9(true)));
            A.CallTo(() => soap.isIHCProjectAvailableAsync(A<inputMessageName14>._))
                .Returns(Task.FromResult(new outputMessageName14(true)));
            A.CallTo(() => soap.enterProjectChangeModeAsync(A<inputMessageName12>._))
                .Returns(Task.FromResult(new outputMessageName12(true)));
            A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._))
                .Returns(Task.FromResult(new outputMessageName13(true)));
            A.CallTo(() => soap.storeIHCProjectAsync(A<inputMessageName4>._))
                .Returns(Task.FromResult(new outputMessageName4(true)));
            A.CallTo(() => soap.waitForControllerStateChangeAsync(A<inputMessageName19>._))
                .ReturnsNextFromSequence(
                    Task.FromResult(new outputMessageName19(new WSControllerState { state = "text.ctrl.state.initialize" })),
                    Task.FromResult(new outputMessageName19(new WSControllerState { state = "text.ctrl.state.initialize" })),
                    Task.FromResult(new outputMessageName19(new WSControllerState { state = "text.ctrl.state.ready" })));
            return soap;
        }

        [Test]
        public async Task StoreProject_HappyPath_ReturnsTrue_AndExitsChangeMode()
        {
            var soap = HappySoap();
            var service = NewService(soap);

            bool stored = await service.StoreProject(new ProjectFile("Project.ihc", "<utcs_project/>"));

            Assert.That(stored, Is.True);
            A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void StoreProject_TransportFailure_SurfacesTheTransportException_NotACleanupError()
        {
            var soap = HappySoap();
            A.CallTo(() => soap.storeIHCProjectAsync(A<inputMessageName4>._))
                .ThrowsAsync(new HttpRequestException("network dropped mid-upload"));
            A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._))
                .ThrowsAsync(new HttpRequestException("still down"));
            var service = NewService(soap);

            Assert.That(async () => await service.StoreProject(new ProjectFile("Project.ihc", "<utcs_project/>")),
                Throws.TypeOf<HttpRequestException>().With.Message.Contains("mid-upload"),
                "the change-mode cleanup failure must never replace the root-cause upload exception");
        }

        [Test]
        public void StoreProject_NonLatin1Data_FailsBeforeEnteringChangeMode()
        {
            var soap = HappySoap();
            var service = NewService(soap);
            var project = new ProjectFile("Project.ihc", "<utcs_project note=\"pris 20 €\"/>");

            Assert.That(async () => await service.StoreProject(project),
                Throws.InvalidOperationException.With.Message.Contains("U+20AC"),
                "an out-of-repertoire character must fail loudly, never be stored as '?'");
            A.CallTo(() => soap.enterProjectChangeModeAsync(A<inputMessageName12>._)).MustNotHaveHappened();
        }
    }
}
