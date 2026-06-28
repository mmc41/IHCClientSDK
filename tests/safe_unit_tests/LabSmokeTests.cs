using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ihc;
using Ihc.App;
using IhcLab;

namespace Ihc.Tests
{
    /// <summary>
    /// Smoke tests that exercise the IHC Lab backend (<see cref="LabAppService"/>) over EVERY operation it
    /// exposes, driven by the full set of mocked IHC services from <see cref="IhcFakeSetup"/>.
    ///
    /// <para>
    /// This is the automated counterpart to a human exploring the Lab GUI against the mocked controller: it
    /// selects each service+operation exactly as the GUI would (same default operation filter the app uses),
    /// supplies realistic arguments, and invokes the operation through the real dynamic-invocation engine
    /// (<see cref="LabAppService.DynCallSelectedOperation"/> / <see cref="LabAppService.StartStream"/>). It
    /// proves the fakes provide enough coverage that every exposed operation runs end-to-end through the real
    /// invocation engine - argument defaulting/validation, reflection dispatch, await, and result formatting -
    /// without crashing and produces a result.
    /// </para>
    ///
    /// <para>
    /// Each case rebuilds a fresh full fake set + LabAppService so per-operation state (e.g. the stateful
    /// UserManager store) never leaks between cases.
    /// </para>
    /// </summary>
    [TestFixture]
    public class LabSmokeTests
    {
        private static IhcSettings MockSettings() => new IhcSettings
        {
            Endpoint = SpecialEndpoints.MockedPrefix + "smoke",
            UserName = "test",
            Password = "test",
            Application = Application.administrator,
            LogSensitiveData = false,
            AsyncContinueOnCapturedContext = false
        };

        /// <summary>Builds the full set of mocked IHC services, exactly as <c>IhcSetup</c> does for the mocked endpoint.</summary>
        private static IIHCApiService[] BuildAllFakes()
        {
            var s = MockSettings();
            return new IIHCApiService[]
            {
                IhcFakeSetup.SetupAuthenticationService(s),
                IhcFakeSetup.SetupControllerService(s),
                IhcFakeSetup.SetupResourceInteractionService(s),
                IhcFakeSetup.SetupConfigurationService(s),
                IhcFakeSetup.SetupOpenAPIService(s),
                IhcFakeSetup.SetupNotificationManagerService(s),
                IhcFakeSetup.SetupMessageControlLogService(s),
                IhcFakeSetup.SetupModuleService(s),
                IhcFakeSetup.SetupTimeManagerService(s),
                IhcFakeSetup.SetupUserManagerService(s),
                IhcFakeSetup.SetupAirlinkManagementService(s),
                IhcFakeSetup.SetupSmsModemService(s),
                IhcFakeSetup.SetupInternalTestService(s),
            };
        }

        /// <summary>Builds a LabAppService configured with all fakes and the app's default operation filter.</summary>
        private static LabAppService BuildLab()
        {
            var lab = new LabAppService(
                globalSupportedServiceFilter: null,
                globalSupportedOperationFilter: OperationFilterConfiguration.CreateDefaultFilter());
            lab.Configure(MockSettings(), BuildAllFakes());
            return lab;
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
            Email = "smoke@mock.com",
            Firstname = "Smoke",
            Lastname = "Test",
            Phone = "+4500000000",
            Group = IhcUserGroup.Users,
            Project = "Mock Project"
        };

        /// <summary>
        /// Produces a realistic, type-correct argument for a parameter so the operation can actually run.
        /// Mirrors what the GUI parameter controls would yield. A few operation-specific overrides satisfy the
        /// deliberately strict UserManager fake (Add needs a new user, Update/Remove need an existing one).
        /// Complex "Set*" record parameters are left null - those fakes ignore the argument - to keep this small.
        /// </summary>
        private static object BuildArg(Type type, string opName)
        {
            if (type == typeof(IhcUser))
                return SmokeUser(opName == "AddUser" ? "smoke_newuser" : "testuser");
            if (opName == "RemoveUser" && type == typeof(string))
                return "testuser";

            if (type == typeof(string)) return "smoke";
            if (type == typeof(bool)) return true;
            if (type == typeof(int)) return 1;
            if (type == typeof(long)) return 1L;
            if (type == typeof(short)) return (short)1;
            if (type == typeof(byte)) return (byte)1;
            if (type == typeof(sbyte)) return (sbyte)1;
            if (type == typeof(uint)) return 1u;
            if (type == typeof(ulong)) return 1ul;
            if (type == typeof(ushort)) return (ushort)1;
            if (type == typeof(float)) return 1f;
            if (type == typeof(double)) return 1d;
            if (type == typeof(decimal)) return 1m;
            if (type == typeof(DateTime)) return DateTime.Now;
            if (type == typeof(DateTimeOffset)) return DateTimeOffset.Now;
            if (type == typeof(TimeSpan)) return TimeSpan.Zero;
            if (type == typeof(CancellationToken)) return default(CancellationToken);
            if (type.IsEnum) return Enum.GetValues(type).GetValue(0)!;

            if (type == typeof(ResourceValue)) return SampleResourceValue();
            if (type == typeof(ProjectFile)) return new ProjectFile("smoke.vis", "<?xml version=\"1.0\"?>");
            if (type == typeof(SceneProject)) return new SceneProject("smoke.icw", new byte[] { 1, 2, 3, 4 });

            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var arr = Array.CreateInstance(elementType, 1);
                arr.SetValue(BuildArg(elementType, opName), 0);
                return arr;
            }

            if (typeof(IEnumerable<int>).IsAssignableFrom(type)) return new List<int> { 1, 2, 3 };
            if (typeof(IEnumerable<ResourceValue>).IsAssignableFrom(type)) return new List<ResourceValue> { SampleResourceValue() };

            // Complex Set* parameter records (NetworkSettings, SMTPSettings, ...): the fakes ignore the value,
            // so null is sufficient. SetMethodArgument allows null for reference-type parameters.
            return null!;
        }

        private static ResourceValue SampleResourceValue() => new ResourceValue
        {
            ResourceID = 1,
            Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.INT, IntValue = 1 }
        };

        private static void SetRealisticArgs(LabAppService.OperationItem op)
        {
            var parameters = op.OperationMetadata.Parameters;
            if (parameters.Length == 0)
                return;

            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                args[i] = BuildArg(parameters[i].Type, op.DisplayName);

            op.SetMethodArgumentsFromArray(args);
        }

        /// <summary>Enumerates every operation the Lab exposes under the default filter (one NUnit case each).</summary>
        public static IEnumerable<TestCaseData> AllExposedOperations()
        {
            var lab = BuildLab();
            foreach (var service in lab.Services)
            {
                foreach (var op in service.OperationItems)
                {
                    yield return new TestCaseData(service.DisplayName, op.DisplayName, op.MethodParameterCount, op.OperationMetadata.Kind)
                        .SetName($"{service.DisplayName}.{op.DisplayName}({op.MethodParameterCount})");
                }
            }
        }

        /// <summary>
        /// Every exposed operation, across all 13 mocked services, can be selected and invoked through
        /// LabAppService with realistic arguments and returns a non-null result without crashing.
        /// </summary>
        [TestCaseSource(nameof(AllExposedOperations))]
        public async Task EveryExposedOperation_InvokesThroughLab_WithoutCrashing(
            string serviceName, string opName, int paramCount, ServiceOperationKind kind)
        {
            var lab = BuildLab();
            var op = FindOp(lab, serviceName, opName, paramCount);
            lab.SelectedOperation = op;
            SetRealisticArgs(op);

            if (kind == ServiceOperationKind.AsyncEnumerable)
            {
                // Streaming op: start, immediately request stop, and confirm it ends cleanly. The demo stream
                // delays ~1s before its first item; StopStream cancels that wait so this does not block.
                var streamTask = lab.StartStream(_ => { });
                lab.StopStream();
                var finished = await Task.WhenAny(streamTask, Task.Delay(TimeSpan.FromSeconds(10)));
                Assert.That(finished, Is.SameAs(streamTask), $"{serviceName}.{opName} stream did not stop within the timeout");
                await streamTask; // a cancelled stream is a normal end (handled inside StartStream)
                return;
            }

            var result = await lab.DynCallSelectedOperation();
            Assert.That(result, Is.Not.Null, $"{serviceName}.{opName} returned a null OperationResult");
            Assert.That(result.DisplayResult, Is.Not.Null, $"{serviceName}.{opName} produced a null DisplayResult");
        }

        /// <summary>
        /// Guards the smoke surface itself: all 13 services are present and a substantial number of operations
        /// are exposed, so a regression in the filter/metadata cannot silently shrink coverage to nothing.
        /// </summary>
        [Test]
        public void Lab_ExposesAllServicesAndManyOperations()
        {
            var lab = BuildLab();

            Assert.That(lab.Services.Length, Is.EqualTo(13), "All 13 mocked IHC services should be exposed");

            int operationCount = lab.Services.Sum(s => s.OperationItems.Length);
            Assert.That(operationCount, Is.GreaterThan(120),
                $"Expected the Lab to expose well over 120 operations, but only {operationCount} were found");
        }
    }
}
