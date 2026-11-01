using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Ihc;
using Ihc.App;
using Ihc.Stubbing;
using Ihc.Tests.Shared;
using IhcLab;

namespace Ihc.Tests
{
    /// <summary>
    /// Smoke tests that exercise the IHC Lab backend (<see cref="LabAppService"/>) over EVERY operation it
    /// exposes, driven by the real SDK services over a <see cref="ControllerStub"/> each case registers itself.
    ///
    /// <para>
    /// This is the automated counterpart to a human exploring the Lab GUI against a stubbed controller: it
    /// selects each service+operation exactly as the GUI would (same default operation filter the app uses),
    /// supplies realistic arguments, and invokes the operation through the real dynamic-invocation engine
    /// (<see cref="LabAppService.DynCallSelectedOperation"/> / <see cref="LabAppService.StartStream"/>). Every
    /// request crosses the real SDK mappers and reaches the stub, so this is the stub's breadth test: every
    /// default answer, typed or generated from its shape, has to map. It proves mapping, not plausible success.
    /// </para>
    ///
    /// <para>
    /// Each case registers a fresh stub so per-operation state (e.g. the user store) never leaks between cases,
    /// and releases it at teardown after checking that the stub met no call it could not answer.
    /// </para>
    /// </summary>
    [TestFixture]
    public class LabSmokeTests
    {
        private ControllerStub? _stub;
        private ControllerStubRegistration? _registration;
        private AuthenticationService? _session;
        private TelemetryCapture? _capture;

        private static IhcSettings SmokeSettings(string endpoint) => new()
        {
            Endpoint = endpoint,
            UserName = "smoke",
            Password = "smoke-pw",
            Application = Application.administrator,
            LogSensitiveData = false,
            AsyncContinueOnCapturedContext = false
        };

        /// <summary>
        /// The one place this suite builds real services, admitted in ControllerReachGuard: their endpoint is a
        /// stub endpoint, which the SDK routes only to a registration and refuses when none is registered. Every
        /// service rides one session, as Lab's own setup builds them.
        /// </summary>
        private static IIHCApiService[] ServicesOn(IhcSettings settings, out AuthenticationService session)
        {
            session = new AuthenticationService(settings);
            return
            [
                session,
                new ControllerService(session),
                new ResourceInteractionService(session),
                new ConfigurationService(session),
                new OpenAPIService(session),
                new NotificationManagerService(session),
                new MessageControlLogService(session),
                new ModuleService(session),
                new TimeManagerService(session),
                new UserManagerService(session),
                new AirlinkManagementService(session),
                new SmsModemService(session),
                new InternalTestService(session),
                new LedDimmerManagementService(session),
            ];
        }

        /// <summary>A Lab over every service, with the app's default operation filter.</summary>
        private static LabAppService BuildLab(IhcSettings settings, IIHCApiService[] services)
        {
            var lab = new LabAppService(
                globalSupportedServiceFilter: null,
                globalSupportedOperationFilter: OperationFilterConfiguration.CreateDefaultFilter());
            lab.Configure(settings, services);
            return lab;
        }

        /// <summary>Registers a fresh stub for this case and builds a Lab over it.</summary>
        private LabAppService StartCase()
        {
            _capture = TelemetryCapture.Listen(Ihc.Telemetry.ActivitySourceName);
            _stub = new ControllerStub();
            _registration = ControllerStubs.Register(_stub);
            IhcSettings settings = SmokeSettings(_registration.Endpoint);
            return BuildLab(settings, ServicesOn(settings, out _session));
        }

        /// <summary>
        /// Ends the session and releases the registration, then checks that the stub could answer everything the
        /// case sent, and that everything the SDK sent went to the stub. The SDK raises on a fault before it reads
        /// the body, and the engine reports it as a result, so the stub's own lists are where an unanswered call
        /// shows; the SDK's HTTP spans are where a request that went anywhere else shows.
        /// </summary>
        [TearDown]
        public void ReleaseTheStub()
        {
            _session?.Dispose();
            _registration?.Dispose();
            if (_stub is { } stub && _capture is { } capture)
            {
                IReadOnlyList<Activity> requests = RequestDestinations.SdkRequests(capture.Spans);
                Assert.Multiple(() =>
                {
                    Assert.That(stub.Unmodelled, Is.Empty, "calls the stub could not answer");
                    Assert.That(stub.Defects, Is.Empty, "exceptions the stub threw while answering");
                    Assert.That(RequestDestinations.NonStubDestinations(requests), Is.Empty, "SDK requests whose host is not a stub host");
                    Assert.That(requests, Has.Count.EqualTo(stub.Journal.All.Count),
                        "one SDK request per exchange the stub journaled, so a check over no requests cannot pass");
                });
            }
            _capture?.Dispose();
            _capture = null;
            _stub = null;
            _registration = null;
            _session = null;
        }

        /// <summary>
        /// The destination check's positive control: a request to a controller's address is reported, and one to a
        /// stub host is not, so the check above is not a filter that passes everything.
        /// </summary>
        [Test]
        public void TheDestinationCheck_ReportsARequestToANonStubHost()
        {
            using var toController = new Activity("POST").SetTag("url.full", "http://10.0.0.5/ws/ControllerService");
            using var toStub = new Activity("POST").SetTag("url.full", "http://s1.controllerstub.invalid/ws/ControllerService");

            Assert.That(RequestDestinations.NonStubDestinations([toController, toStub]), Is.EqualTo(new[] { "http://10.0.0.5/ws/ControllerService" }));
        }

        private static LabAppService.OperationItem FindOp(LabAppService lab, string serviceName, string opName, int paramCount)
        {
            var service = lab.Services.FirstOrDefault(s => s.DisplayName == serviceName)
                ?? throw new InvalidOperationException($"Service '{serviceName}' not found in Lab");
            return service.OperationItems.FirstOrDefault(o => o.DisplayName == opName && o.MethodParameterCount == paramCount)
                ?? throw new InvalidOperationException($"Operation '{serviceName}.{opName}' with {paramCount} parameter(s) not found");
        }

        private static IhcUser SmokeUser(string username) => new IhcUser
        {
            Username = username,
            Password = "Pass123",
            Email = "smoke@stub.invalid",
            Firstname = "Smoke",
            Lastname = "Test",
            Phone = "+4500000000",
            Group = IhcUserGroup.Users,
            Project = "Stub Project"
        };

        /// <summary>
        /// A valid argument for a parameter, from the shared <see cref="SdkArguments"/> builder. The user-name
        /// overrides follow the stub's default users: an add needs a name the store does not hold, and an update
        /// or removal one it does.
        /// </summary>
        private static object BuildArg(Type type, string opName, string existingUser)
        {
            if (type == typeof(IhcUser))
                return SmokeUser(opName == "AddUser" ? "smoke_newuser" : existingUser);
            if (opName == "RemoveUser" && type == typeof(string))
                return existingUser;
            return SdkArguments.For(type);
        }

        private static void SetRealisticArgs(LabAppService.OperationItem op, string existingUser)
        {
            var parameters = op.OperationMetadata.Parameters;
            if (parameters.Length == 0)
                return;

            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                args[i] = BuildArg(parameters[i].Type, op.DisplayName, existingUser);

            op.SetMethodArgumentsFromArray(args);
        }

        /// <summary>The operation table's entry for a Lab operation, found by the interface its service implements.</summary>
        private static SdkMethodEntry EntryOf(LabAppService.OperationItem op) =>
            SdkOperations.Entries.Single(entry => entry.SdkService.IsInstanceOfType(op.Service.Service) && entry.Method == op.DisplayName);

        /// <summary>Every operation the Lab exposes under the default filter, read from a Lab over a stub.</summary>
        private static IReadOnlyList<(string Service, string Operation, int ParameterCount, ServiceOperationKind Kind, SdkMethodEntry Entry)> ExposedOperations()
        {
            var stub = new ControllerStub();
            using ControllerStubRegistration registration = ControllerStubs.Register(stub);
            IhcSettings settings = SmokeSettings(registration.Endpoint);
            LabAppService lab = BuildLab(settings, ServicesOn(settings, out AuthenticationService session));
            using (session)
            {
                return
                [
                    .. lab.Services.SelectMany(service => service.OperationItems.Select(op =>
                        (service.DisplayName, op.DisplayName, op.MethodParameterCount, op.OperationMetadata.Kind, EntryOf(op)))),
                ];
            }
        }

        public static IEnumerable<TestCaseData> AllExposedOperations() =>
            ExposedOperations().Select(op => new TestCaseData(op.Service, op.Operation, op.ParameterCount, op.Kind)
                .SetName($"{op.Service}.{op.Operation}({op.ParameterCount})"));

        /// <summary>
        /// Every exposed operation, across all 14 services, can be selected and invoked through LabAppService with
        /// realistic arguments and returns a non-null result without crashing; a gated one is refused by the SDK's
        /// gate before it sends anything, because these settings never allow the dangerous calls.
        /// </summary>
        [TestCaseSource(nameof(AllExposedOperations))]
        public async Task EveryExposedOperation_InvokesThroughLab_WithoutCrashing(
            string serviceName, string opName, int paramCount, ServiceOperationKind kind)
        {
            var lab = StartCase();
            var op = FindOp(lab, serviceName, opName, paramCount);
            lab.SelectedOperation = op;
            SetRealisticArgs(op, _stub!.UserManager.Users[0].Username!);

            if (kind == ServiceOperationKind.AsyncEnumerable)
            {
                await RunStreamToItsStop(lab, $"{serviceName}.{opName}");
                return;
            }

            if (EntryOf(op).Role == SdkMethodRole.Gated)
            {
                Assert.That(async () => await lab.DynCallSelectedOperation(), Throws.InstanceOf<InvalidOperationException>());
                Assert.That(_stub.Journal.All, Is.Empty, $"{serviceName}.{opName}: the gate refuses before it sends");
                return;
            }

            var result = await lab.DynCallSelectedOperation();
            Assert.That(result, Is.Not.Null, $"{serviceName}.{opName} returned a null OperationResult");
            Assert.That(result.DisplayResult, Is.Not.Null, $"{serviceName}.{opName} produced a null DisplayResult");
        }

        /// <summary>
        /// Every wire action an exposed, sending operation owns reaches the stub when that operation runs: the
        /// journal, not the engine's result, is what shows a request was really sent. Rows owned by nothing Lab
        /// exposes are proven at the stub's dispatcher instead.
        /// </summary>
        [Test]
        public async Task EverySentRowLabExposes_IsJournaled()
        {
            var journaled = new HashSet<(string Service, string Action)>();
            var owned = new HashSet<(string Service, string Action)>();
            foreach (var exposed in ExposedOperations().Where(op => op.Entry.Role == SdkMethodRole.Sent && op.Kind != ServiceOperationKind.AsyncEnumerable))
            {
                owned.UnionWith(SdkOperations.Rows
                    .Where(row => row.OwnerService == exposed.Entry.SdkService && row.OwnerMethod == exposed.Entry.Method)
                    .Select(row => (row.Service, row.WireAction)));

                var lab = StartCase();
                var op = FindOp(lab, exposed.Service, exposed.Operation, exposed.ParameterCount);
                lab.SelectedOperation = op;
                SetRealisticArgs(op, _stub!.UserManager.Users[0].Username!);
                await lab.DynCallSelectedOperation();
                journaled.UnionWith(_stub.Journal.All.Select(call => (call.Service, call.SoapAction)));
                ReleaseTheStub();
            }

            Assert.That(owned, Is.Not.Empty);
            Assert.That(owned.Except(journaled), Is.Empty, "sent rows Lab exposes that no case journaled");
        }

        /// <summary>
        /// Starts a stream, stops it, and waits for it to end before the case's teardown releases its registration:
        /// a stream left running would end on a disposed session, and its long poll would still be reaching a stub
        /// that has gone.
        /// </summary>
        private static async Task RunStreamToItsStop(LabAppService lab, string name)
        {
            var streamTask = lab.StartStream(_ => { });
            lab.StopStream();
            var finished = await Task.WhenAny(streamTask, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.That(finished, Is.SameAs(streamTask), $"{name} stream did not stop within the timeout");
            await streamTask; // a cancelled stream is a normal end (handled inside StartStream)
        }

        /// <summary>
        /// Guards the smoke surface itself: all 14 services are present and a substantial number of operations
        /// are exposed, so a regression in the filter/metadata cannot silently shrink coverage to nothing.
        /// </summary>
        [Test]
        public void Lab_ExposesAllServicesAndManyOperations()
        {
            var lab = StartCase();

            Assert.That(lab.Services.Length, Is.EqualTo(14), "All 14 IHC services should be exposed");

            int operationCount = lab.Services.Sum(s => s.OperationItems.Length);
            Assert.That(operationCount, Is.GreaterThan(120),
                $"Expected the Lab to expose well over 120 operations, but only {operationCount} were found");
        }
    }
}
