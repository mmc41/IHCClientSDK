using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FakeItEasy;
using Ihc.Soap.Controller;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// What the controller's reported state maps to, including when it reports none.
    ///
    /// <see cref="ControllerState"/> already declares <see cref="ControllerState.Unknown"/>, and the mapping's
    /// default arm returns it for a string nobody recognises. An ABSENT state answered
    /// <see cref="ControllerState.Uninitialized"/> instead - a real controller state, the value
    /// <c>WaitForControllerStateChange</c> polls against during a project store, and the enum's zero member
    /// besides. So a never-assigned variable, a genuinely uninitialized controller and an unreadable answer
    /// were one value.
    /// </summary>
    [TestFixture]
    public class ControllerStateMappingTests
    {
        private static ControllerService ServiceAnswering(outputMessageName1 state)
        {
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            A.CallTo(() => soap.getStateAsync(A<inputMessageName1>._)).Returns(Task.FromResult(state));
            return new ControllerService(FakeSession.Over(), soap);
        }

        private static IEnumerable<TestCaseData> UnreadableAnswers()
        {
            yield return new TestCaseData(new outputMessageName1(null)).SetName("{m}(no state element at all)");
            yield return new TestCaseData(new outputMessageName1(new WSControllerState { state = null })).SetName("{m}(a state element with no text)");
            yield return new TestCaseData(new outputMessageName1(new WSControllerState { state = "" })).SetName("{m}(an empty state)");
        }

        [TestCaseSource(nameof(UnreadableAnswers))]
        public async Task AnAbsentOrEmptyState_ReadsAsUnknownRatherThanUninitialized(outputMessageName1 answer)
        {
            ControllerState state = await ServiceAnswering(answer).GetControllerState();

            Assert.That(state, Is.EqualTo(ControllerState.Unknown));
        }

        /// <summary>Every state the controller actually names still maps to itself - so the guard above is
        /// proven to have taken only the answers that name nothing.</summary>
        [TestCase("text.ctrl.state.ready", ControllerState.Ready)]
        [TestCase("text.ctrl.state.initialize", ControllerState.Initialize)]
        [TestCase("text.ctrl.state.failed", ControllerState.Failed)]
        [TestCase("text.ctrl.state.rfconfiguration", ControllerState.RfConfiguration)]
        [TestCase("text.ctrl.state.simulation", ControllerState.Simulation)]
        [TestCase("text.ctrl.state.uninitialized", ControllerState.Uninitialized)]
        [TestCase("text.ctrl.state.somethingnew", ControllerState.Unknown)]
        public async Task ANamedState_StillMapsToItself(string wire, ControllerState expected)
        {
            ControllerState state = await ServiceAnswering(
                new outputMessageName1(new WSControllerState { state = wire })).GetControllerState();

            Assert.That(state, Is.EqualTo(expected));
        }

        /// <summary>
        /// The SDK reads <see cref="ControllerState.Unknown"/> from a reply that names no state, so a caller that
        /// keeps polling must be able to send it back. It goes out as a nil state, which is what IHC Administrator
        /// sends when it has none and what a 3.3.44 controller was captured answering.
        /// </summary>
        [Test]
        public async Task WaitForControllerStateChange_SendsUnknownAsANilState()
        {
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            inputMessageName19? sent = null;
            A.CallTo(() => soap.waitForControllerStateChangeAsync(A<inputMessageName19>._))
                .Invokes((inputMessageName19 request) => sent = request)
                .Returns(Task.FromResult(new outputMessageName19(new WSControllerState { state = "text.ctrl.state.ready" })));

            ControllerState answered = await new ControllerService(FakeSession.Over(), soap)
                .WaitForControllerStateChange(ControllerState.Unknown, 10);

            Assert.That(answered, Is.EqualTo(ControllerState.Ready));
            Assert.That(sent?.waitForControllerStateChange1, Is.Null);
            XElement state = XDocument.Parse(SoapRequestText.Of(sent!)).Descendants(Utcs + "waitForControllerStateChange1").Single();
            Assert.That((string?)state.Attribute(Xsi + "nil"), Is.EqualTo("true"), "the serializer may pick any prefix for xsi");
            Assert.That(state.HasElements, Is.False);
        }

        private static readonly XNamespace Utcs = "utcs";
        private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    }
}
