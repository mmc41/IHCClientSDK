using System.Collections.Generic;
using System.Threading.Tasks;
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
    }
}
