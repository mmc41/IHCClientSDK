using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Soap.Controller;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// A test steers and inspects a <see cref="ControllerStub"/> through its journal, <c>FailOn</c>,
    /// <c>OverrideRaw</c>, <c>Defects</c> and teardown check, as the stub's contract states.
    /// </summary>
    [TestFixture]
    public class ControllerStubContractTests
    {
        private const string Controller = "ControllerService";
        private const string GetState = "getState";

        [Test]
        public async Task ATypedAnswer_BeatsTheShapeDefault()
        {
            var answers = new SeededControllerAnswers();
            var stub = new ControllerStub([answers]);

            ControllerStubResponse typed = await stub.HandleAsync(StubExchanges.For(Controller, GetState), CancellationToken.None);
            await stub.HandleAsync(StubExchanges.For(Controller, "isSDCardReady"), CancellationToken.None);

            Assert.That(typed.Body, Does.Contain(SeededControllerAnswers.State));
            Assert.That(answers.Calls, Is.EqualTo(1));
            Assert.That(stub.Journal.All.Select(call => call.Outcome), Is.EqualTo(new[] { StubOutcome.Typed, StubOutcome.Shape }));
        }

        [Test]
        public async Task AnInjectedFailure_BeatsATypedAnswer_AndRunsNoAnswer()
        {
            var answers = new SeededControllerAnswers();
            var stub = new ControllerStub([answers]);
            stub.FailOn<IControllerService>(nameof(IControllerService.GetControllerState));

            ControllerStubResponse answer = await stub.HandleAsync(StubExchanges.For(Controller, GetState), CancellationToken.None);

            Assert.That(answer.StatusCode, Is.EqualTo(500));
            Assert.That(answer.Body, Does.Contain("<faultstring>ihcclient_stub: injected fault on ControllerService.getState</faultstring>"));
            Assert.That(answers.Calls, Is.Zero, "an injected failure short-circuits before any answer runs");
            Assert.That(stub.Journal.Calls<IControllerService>(nameof(IControllerService.GetControllerState)).Single().Outcome,
                Is.EqualTo(StubOutcome.FaultInjected));
        }

        [Test]
        public async Task ARawOverride_BeatsAnInjectedFailure()
        {
            var answers = new SeededControllerAnswers();
            var stub = new ControllerStub([answers]);
            stub.FailOn<IControllerService>(nameof(IControllerService.GetControllerState));
            stub.OverrideRaw(Controller, GetState, (_, _) => Task.FromResult(new ControllerStubResponse(200, "<not-soap/>", "session=override")));

            ControllerStubResponse answer = await stub.HandleAsync(StubExchanges.For(Controller, GetState, "session=sent"), CancellationToken.None);

            Assert.That(answer.Body, Is.EqualTo("<not-soap/>"));
            Assert.That(answers.Calls, Is.Zero);
            StubCall call = stub.Journal.All.Single();
            Assert.That((call.Outcome, call.Cookie, call.SetCookie), Is.EqualTo((StubOutcome.Overridden, "session=sent", "session=override")));
        }

        [Test]
        public async Task FailOn_FailsAMultiCallMethodAtItsPrimaryAction_Only()
        {
            var stub = new ControllerStub();
            stub.FailOn<IControllerService>(nameof(IControllerService.StoreProject));

            ControllerStubResponse step = await stub.HandleAsync(StubExchanges.For(Controller, GetState), CancellationToken.None);
            ControllerStubResponse primary = await stub.HandleAsync(StubExchanges.For(Controller, "storeIHCProject"), CancellationToken.None);

            Assert.That((step.StatusCode, primary.StatusCode), Is.EqualTo((200, 500)));
        }

        [Test]
        public async Task FailOn_CanLoseTheConnectionInsteadOfAnswering()
        {
            var stub = new ControllerStub();
            stub.FailOn<IControllerService>(nameof(IControllerService.GetControllerState), StubFault.ConnectionLost);

            ControllerStubResponse answer = await stub.HandleAsync(StubExchanges.For(Controller, GetState), CancellationToken.None);

            Assert.That(answer, Is.SameAs(ControllerStubResponse.ConnectionLost));
            Assert.That(stub.Journal.All.Single().Outcome, Is.EqualTo(StubOutcome.ConnectionLost));
        }

        [Test]
        public void FailOn_RefusesAMethodWhoseFailureCouldNeverHappen()
        {
            var stub = new ControllerStub();

            Assert.Multiple(() =>
            {
                Assert.That(() => stub.FailOn<IAuthenticationService>(nameof(IAuthenticationService.IsAuthenticated)),
                    Throws.ArgumentException.With.Message.Contains("sends nothing"));
                Assert.That(() => stub.FailOn<IControllerService>("NoSuchMethod"),
                    Throws.ArgumentException.With.Message.Contains("declares no method NoSuchMethod"));
                Assert.That(() => stub.FailOn<IOpenAPIService>(nameof(IOpenAPIService.GetResourceValueChanges)),
                    Throws.ArgumentException.With.Message.Contains("owns no wire operation"));
            });
        }

        /// <summary>
        /// The stub names an operation by the SDK's service interface. A service class shares the interface's
        /// methods, so naming one is an easy slip, and the refusal names the interface to use instead.
        /// </summary>
        [Test]
        public void AServiceClass_IsRefused_NamingItsInterface()
        {
            var stub = new ControllerStub();

            Assert.Multiple(() =>
            {
                Assert.That(() => stub.FailOn<ControllerService>(nameof(IControllerService.GetControllerState)),
                    Throws.ArgumentException.With.Message.Contains(nameof(IControllerService)));
                Assert.That(() => stub.Journal.Calls<ControllerService>(nameof(IControllerService.GetControllerState)),
                    Throws.ArgumentException.With.Message.Contains(nameof(IControllerService)));
            });
        }

        /// <summary>
        /// A journal lookup that could never match is refused, because the empty list it would answer is also
        /// what an assertion that nothing was sent expects, and would pass whatever the code under test did.
        /// </summary>
        [Test]
        public void TheJournal_RefusesALookupThatCouldNeverMatch()
        {
            var stub = new ControllerStub();

            Assert.Multiple(() =>
            {
                Assert.That(() => stub.Journal.Calls<IControllerService>("NoSuchMethod"),
                    Throws.ArgumentException.With.Message.Contains("declares no method NoSuchMethod"));
                Assert.That(() => stub.Journal.Calls<IAuthenticationService>(nameof(IAuthenticationService.IsAuthenticated)),
                    Throws.ArgumentException.With.Message.Contains("sends nothing"));
                Assert.That(() => stub.Journal.Calls<IOpenAPIService>(nameof(IOpenAPIService.GetResourceValueChanges)),
                    Throws.ArgumentException.With.Message.Contains("owns no wire operation").And.Message.Contains("waitForEvents"),
                    "the refusal names the steps, which are read from All by their wire action");
            });
        }

        /// <summary>
        /// A raw override is keyed by wire names, which differ from the SDK's names in exactly the places a test
        /// author would write <c>nameof</c>. An override no exchange can match would never fire, so the case it
        /// arranges would pass without its malformed answer ever being sent; the refusal names the wire name.
        /// </summary>
        [Test]
        public void ARawOverrideOfNoWireOperation_IsRefused_NamingTheWireName()
        {
            var stub = new ControllerStub();
            static Task<ControllerStubResponse> Answer(ControllerStubRequest request, CancellationToken token) =>
                Task.FromResult(new ControllerStubResponse(200, "<not-soap/>", null));

            Assert.Multiple(() =>
            {
                Assert.That(() => stub.OverrideRaw(nameof(SmsModemService), "getSMSModemInfo", Answer),
                    Throws.ArgumentException.With.Message.Contains("SMSModemService"));
                Assert.That(() => stub.OverrideRaw(nameof(IControllerService), GetState, Answer),
                    Throws.ArgumentException.With.Message.Contains(Controller));
                Assert.That(() => stub.OverrideRaw(Controller, nameof(IControllerService.GetProject), Answer),
                    Throws.ArgumentException.With.Message.Contains("getIHCProject"));
                Assert.That(() => stub.OverrideRaw(Controller, "noSuchAction", Answer),
                    Throws.ArgumentException.With.Message.Contains("no wire operation"));
                Assert.That(() => stub.OverrideRaw(Controller, "GetState", Answer),
                    Throws.ArgumentException.With.Message.Contains("compared with their case").And.Message.Contains(GetState));
                Assert.That(() => stub.OverrideRaw("controllerservice", GetState, Answer),
                    Throws.ArgumentException.With.Message.Contains("compared with their case").And.Message.Contains(Controller));
                Assert.That(() => stub.OverrideRaw("NoSuchService", GetState, Answer),
                    Throws.ArgumentException.With.Message.Contains("The wire services are").And.Message.Contains(Controller));
                Assert.That(() => stub.OverrideRaw("OpenAPIService", nameof(IOpenAPIService.GetResourceValueChanges), Answer),
                    Throws.ArgumentException.With.Message.Contains("wire actions it sends are").And.Message.Contains("waitForEvents"));
            });
        }

        [Test]
        public async Task TheJournal_NamesEachExchangeByItsOwnerAndItsWireNames()
        {
            var stub = new ControllerStub();

            await stub.HandleAsync(StubExchanges.For(Controller, GetState, "session=1"), CancellationToken.None);
            await stub.HandleAsync(StubExchanges.For("ConfigurationService", "testSendMessage"), CancellationToken.None);

            Assert.That(stub.Journal.All, Is.EqualTo(new[]
            {
                new StubCall(1, Controller, GetState, nameof(IControllerService), nameof(IControllerService.GetControllerState),
                    StubOutcome.Typed, null, "session=1", null),
                new StubCall(2, "ConfigurationService", "testSendMessage", null, null, StubOutcome.Shape, null, null, null),
            }));
        }

        [Test]
        public async Task AJournalledPayload_CarriesNoPassword_AndLeavesTheAnswersValueAlone()
        {
            var answers = new SeededControllerAnswers();
            var stub = new ControllerStub([answers]);

            await stub.HandleAsync(StubExchanges.For(Controller, GetState), CancellationToken.None);

            Assert.That(stub.Journal.All.Single().Payload, Is.EqualTo(new SeededCredentials("installer", null)));
            Assert.That(answers.Recorded, Is.EqualTo(new SeededCredentials("installer", "secret")));
        }

        [Test]
        public async Task APayloadWithNoPassword_IsJournalledAsRecorded()
        {
            var payload = new SeededCredentials("installer", null);
            var stub = new ControllerStub([new SeededControllerAnswers(payload)]);

            await stub.HandleAsync(StubExchanges.For(Controller, GetState), CancellationToken.None);

            Assert.That(stub.Journal.All.Single().Payload, Is.SameAs(payload));
        }

        [Test]
        public async Task APayloadWhosePasswordCannotBeCleared_IsADefect()
        {
            var stub = new ControllerStub([new SeededControllerAnswers(new SeededFixedCredentials())]);

            Assert.That(async () => await stub.HandleAsync(StubExchanges.For(Controller, GetState), CancellationToken.None),
                Throws.InvalidOperationException.With.Message.Contains("cannot be cleared"));
            Assert.That(stub.Journal.All.Single().Payload, Is.Null);
        }

        /// <summary>Positive control: a throwing answer the code under test swallowed still turns teardown red.</summary>
        [Test]
        public async Task AThrowingAnswer_IsADefect_ThatTheTeardownCheckReports()
        {
            var stub = new ControllerStub([new SeededThrowingAnswers()]);

            Assert.That(async () => await stub.HandleAsync(StubExchanges.For(Controller, GetState), CancellationToken.None),
                Throws.TypeOf<NotSupportedException>(), "a defect is rethrown, never turned into a SOAP fault");
            await stub.HandleAsync(StubExchanges.For(Controller, "isSDCardReady"), CancellationToken.None);

            Assert.That(stub.Defects.Select(defect => (defect.SoapAction, defect.Exception.GetType())),
                Is.EqualTo(new[] { (GetState, typeof(NotSupportedException)) }));
            Assert.That(stub.Journal.All.Select(call => call.Outcome), Is.EqualTo(new[] { StubOutcome.Defect, StubOutcome.Shape }));
            Assert.That(stub.VerifyNoFaults, Throws.InvalidOperationException.With.Message
                .Contains("defect in ControllerService.getState: System.NotSupportedException"));
        }

        /// <summary>Positive control: an unmodelled call turns teardown red, naming the call and why.</summary>
        [Test]
        public async Task AnUnmodelledCall_IsReportedByTheTeardownCheck()
        {
            var stub = new ControllerStub();

            await stub.HandleAsync(new ControllerStubRequest(Controller, "noSuchOperation", string.Empty, null), CancellationToken.None);

            Assert.That(stub.Journal.All.Single().Outcome, Is.EqualTo(StubOutcome.Unmodelled));
            Assert.That(stub.VerifyNoFaults, Throws.InvalidOperationException.With.Message
                .Contains("unmodelled ControllerService.noSuchOperation: no generated operation has this service and wire action"));
        }

        [Test]
        public async Task ACleanStub_PassesTheTeardownCheck()
        {
            var stub = new ControllerStub();

            await stub.HandleAsync(StubExchanges.For(Controller, GetState), CancellationToken.None);

            Assert.That(stub.VerifyNoFaults, Throws.Nothing);
        }

        [Test]
        public void ACancellation_PassesThroughAndIsNoDefect()
        {
            var stub = new ControllerStub();

            Assert.That(async () => await stub.HandleAsync(StubExchanges.For(Controller, GetState), new CancellationToken(canceled: true)),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(stub.Defects, Is.Empty);
        }

        [Test]
        public void ARawOverrideReturningNoResponse_IsADefect()
        {
            var stub = new ControllerStub();
            stub.OverrideRaw(Controller, GetState, (_, _) => Task.FromResult<ControllerStubResponse>(null!));

            Assert.That(async () => await stub.HandleAsync(StubExchanges.For(Controller, GetState), CancellationToken.None),
                Throws.InvalidOperationException);
            Assert.That(stub.Defects, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// Each stub holds one <see cref="Lock"/>, and the stub's code takes it only through the <c>lock</c>
        /// statement, which compiles to <see cref="Lock.EnterScope"/>: a direct enter could be held across an
        /// <c>await</c>, which the statement makes a compile error.
        /// </summary>
        [Test]
        public void EachStubHoldsOneLock_TakenOnlyWithTheLockStatement()
        {
            Assembly stubAssembly = typeof(ControllerStub).Assembly;
            var locks = typeof(ControllerStub).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => field.FieldType == typeof(Lock));
            var lockCalls = AuthoredMembers.Of(stubAssembly)
                .SelectMany(member => IlBody.Instructions(member))
                .Select(instruction => instruction.Called)
                .Where(called => called is MethodInfo && called.DeclaringType == typeof(Lock))
                .Select(called => called!.Name)
                .ToList();

            Assert.That(locks, Has.Exactly(1).Items);
            Assert.That(lockCalls, Does.Contain(nameof(Lock.EnterScope)), "the scan must see the lock statements it judges");
            Assert.That(lockCalls, Is.All.EqualTo(nameof(Lock.EnterScope)));
        }
    }

    internal sealed record SeededCredentials(string UserName, string? Password);

    /// <summary>A payload whose password has no setter, so the journal cannot clear it.</summary>
    internal sealed class SeededFixedCredentials
    {
        public string Password { get; } = "fixed";
    }

    /// <summary>A typed answer for <c>getState</c> that counts its calls and records a payload with a password.</summary>
    internal sealed class SeededControllerAnswers(object? payload = null) : IWireAnswers
    {
        internal const string State = "seeded-typed-state";

        public string Service => "ControllerService";

        internal int Calls { get; private set; }

        internal object Recorded { get; } = payload ?? new SeededCredentials("installer", "secret");

        internal Task<outputMessageName1> getStateAsync(inputMessageName1 request)
        {
            Calls++;
            AnswerContext.RecordPayload(Recorded);
            return Task.FromResult(new outputMessageName1 { getState1 = new WSControllerState { state = State } });
        }
    }

    internal sealed class SeededThrowingAnswers : IWireAnswers
    {
        public string Service => "ControllerService";

        internal Task<outputMessageName1> getStateAsync(inputMessageName1 request) =>
            throw new NotSupportedException("a defect in a seeded answer");
    }
}
