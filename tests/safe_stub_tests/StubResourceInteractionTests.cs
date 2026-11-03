using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;
using static Ihc.ResourceValue;

namespace Ihc.Tests
{
    /// <summary>
    /// The resource interaction service's bulk operations answer from the one resource store, read and written
    /// through the real <see cref="ResourceInteractionService"/>.
    /// </summary>
    [TestFixture]
    public class StubResourceInteractionTests
    {
        internal const int Id = 41001;

        /// <summary>One value of each kind the SDK writes, each with every part it carries set.</summary>
        public static IEnumerable<TestCaseData> EveryWritableKind()
        {
            yield return Kind(new UnionValue { ValueKind = ValueKind.BOOL, BoolValue = true });
            yield return Kind(new UnionValue { ValueKind = ValueKind.DATE, DateValue = new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.FromHours(1)) });
            yield return Kind(new UnionValue { ValueKind = ValueKind.INT, IntValue = -123456 });
            yield return Kind(new UnionValue { ValueKind = ValueKind.DOUBLE, DoubleValue = 21.75 });
            // With the range a controller sends, which travels with the value both ways.
            yield return Kind(new UnionValue { ValueKind = ValueKind.INT, IntValue = 40, IntMinimum = 0, IntMaximum = 100 }, "INT with range");
            yield return Kind(new UnionValue { ValueKind = ValueKind.DOUBLE, DoubleValue = 21.75, DoubleMinimum = -40, DoubleMaximum = 85.5 }, "DOUBLE with range");
            yield return Kind(new UnionValue
            {
                ValueKind = ValueKind.ENUM,
                EnumValue = new EnumValue { DefinitionTypeID = 113735, EnumValueID = 114248, EnumName = "Kun ændringer" },
            });
            yield return Kind(new UnionValue { ValueKind = ValueKind.TIME, TimeValue = new TimeSpan(13, 45, 30) });
            yield return Kind(new UnionValue { ValueKind = ValueKind.TIMER, TimerValue = 90_000 });
            yield return Kind(new UnionValue { ValueKind = ValueKind.WEEKDAY, WeekdayValue = 5 });
            yield return Kind(new UnionValue { ValueKind = ValueKind.PhoneNumber, PhoneNumberValue = "+4512345678" });
            yield return Kind(new UnionValue { ValueKind = ValueKind.SceneDimmer, DimmerPercentage = 40, DimmerDelayTime = 2, DimmerRampTime = 3 });
            yield return Kind(new UnionValue { ValueKind = ValueKind.SceneRelay, RelayDelayTime = 4, RelayValue = true });
            yield return Kind(new UnionValue { ValueKind = ValueKind.SceneShutter, ShutterPositionIsUp = true, ShutterDelayTime = 6 });
        }

        private static TestCaseData Kind(UnionValue value, string? name = null) =>
            new TestCaseData(new ResourceValue { ResourceID = Id, IsValueRuntime = true, TypeString = "resource_test", Value = value })
                .SetArgDisplayNames(name ?? value.ValueKind.ToString());

        [TestCaseSource(nameof(EveryWritableKind))]
        public async Task AValueOfEveryKind_SetThroughTheWrapper_ReadsBack(ResourceValue value)
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.Resources.SetResourceValues([value]), Is.True);

            Assert.That((await sdk.Resources.GetRuntimeValues([Id])).Select(Timeless), Is.EqualTo(new[] { Timeless(value) }));
            Assert.That(Timeless(sdk.Stub.Resources.Values[Id]), Is.EqualTo(Timeless(value)));
        }

        [Test]
        public async Task ASceneResource_ReadsAsHavingNoValue()
        {
            var scene = new ResourceValue { ResourceID = Id, TypeString = "resource_scene", Value = new UnionValue { ValueKind = ValueKind.NONE } };
            var stub = new ControllerStub();
            stub.Resources.Values = stub.Resources.Values.SetItem(Id, scene);
            using var sdk = StubbedSdk.Start(stub);

            ResourceValue read = (await sdk.Resources.GetRuntimeValues([Id])).Single();

            Assert.That((read.Value.ValueKind, read.TypeString), Is.EqualTo((ValueKind.NONE, "resource_scene")));
        }

        [Test]
        public async Task ABulkRead_AnswersOneValuePerId_InTheOrderAsked_WithFalseForAnUnknownId()
        {
            var stub = new ControllerStub();
            stub.Resources.Values = ImmutableDictionary<int, ResourceValue>.Empty
                .Add(1, ResourceValue.CreateBooleanRuntimeOutput(1, true))
                .Add(2, ResourceValue.CreateBooleanRuntimeOutput(2, true));
            using var sdk = StubbedSdk.Start(stub);

            IReadOnlyList<ResourceValue> read = await sdk.Resources.GetInitialValues([2, 99, 1]);

            Assert.That(read.Select(value => (value.ResourceID, value.Value.BoolValue, value.IsValueRuntime)),
                Is.EqualTo(new[] { (2, (bool?)true, false), (99, (bool?)false, false), (1, (bool?)true, false) }));
        }

        [Test]
        public async Task TheDefaults_AreTheDatalineInputsARealControllerListed_EachOff()
        {
            using var sdk = StubbedSdk.Start();

            IReadOnlyList<DatalineResource> inputs = await sdk.Resources.GetAllDatalineInputs();
            IReadOnlyList<ResourceValue> values = await sdk.Resources.GetRuntimeValues([.. inputs.Select(input => input.ResourceID)]);

            Assert.That(inputs.Select(input => (input.DatalineNumber, input.ResourceID)),
                Is.EqualTo(new[] { (1, 23386), (9, 23898), (17, 28762), (32, 23642), (128, 25946) }));
            Assert.That(values.Select(value => (value.TypeString, value.Value.BoolValue)), Has.All.EqualTo(("dataline_input", (bool?)false)));
        }

        [Test]
        public async Task DatalineInputs_LoggedData_AndScenePositions_RoundTripThroughTheRealWrapper()
        {
            // Millisecond precision: the controller stamps a sample in milliseconds since 1970.
            var logged = new LoggedData { Id = 7, Value = "21.5", Timestamp = new DateTimeOffset(2024, 5, 6, 7, 8, 9, 456, TimeSpan.Zero) };
            var position = new SceneResourceIdAndLocation { SceneResourceId = 80206, ScenePositionSeenFromProduct = "Stue/Lys", ScenePositionSeenFromFunctionBlock = "Blok 1" };
            var input = new DatalineResource { DatalineNumber = 64, ResourceID = Id };
            var stub = new ControllerStub();
            stub.Resources.DatalineInputs = [input];
            stub.Resources.LoggedData = stub.Resources.LoggedData.Add(Id, [logged]);
            stub.Resources.SceneGroupPositions = stub.Resources.SceneGroupPositions.Add(Id, [position]);
            stub.Resources.ScenePositions = stub.Resources.ScenePositions.Add(80206, position);
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Resources.GetAllDatalineInputs(), Is.EqualTo(new[] { input }));
            Assert.That(await sdk.Resources.GetLoggedData(Id), Is.EqualTo(new[] { logged }));
            Assert.That(await sdk.Resources.GetSceneGroupResourceIdAndPositions(Id), Is.EqualTo(new[] { position }));
            Assert.That(await sdk.Resources.GetScenePositionsForSceneValueResource(80206), Is.EqualTo(position));
        }

        /// <summary>Nothing logged, no scene group: the lists come back empty, and a resource with no positions
        /// answers its own id with empty positions, as a real controller does.</summary>
        [Test]
        public async Task NothingStored_AnswersEmpty()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.Resources.GetLoggedData(Id), Is.Empty);
            Assert.That(await sdk.Resources.GetSceneGroupResourceIdAndPositions(Id), Is.Empty);
            Assert.That(await sdk.Resources.GetScenePositionsForSceneValueResource(79693),
                Is.EqualTo(new SceneResourceIdAndLocation { SceneResourceId = 79693, ScenePositionSeenFromProduct = "", ScenePositionSeenFromFunctionBlock = "" }));
        }

        [Test]
        public async Task EnablingNotifications_AnswersTheCurrentValues_AndDisablingAcknowledges()
        {
            using var sdk = StubbedSdk.Start();
            await sdk.Resources.SetResourceValues([ResourceValue.CreateBooleanRuntimeOutput(Id, true)]);

            IReadOnlyList<ResourceValue> runtime = await sdk.Resources.EnableRuntimeValueNotifications([Id]);
            IReadOnlyList<ResourceValue> initial = await sdk.Resources.EnableInitialValueNotifications([Id]);

            Assert.That(runtime.Select(value => (value.ResourceID, value.Value.BoolValue, value.IsValueRuntime)), Is.EqualTo(new[] { (Id, (bool?)true, true) }));
            Assert.That(initial.Select(value => (value.ResourceID, value.Value.BoolValue, value.IsValueRuntime)), Is.EqualTo(new[] { (Id, (bool?)true, false) }));
            Assert.That(await sdk.Resources.DisableRuntimeValueNotifications([Id]), Is.True);
            Assert.That(await sdk.Resources.DisableInitialValueNotifications([Id]), Is.True);
        }

        [Test]
        public async Task ASet_QueuesAChange_WhichTheNextWaitReportsOnce()
        {
            using var sdk = StubbedSdk.Start();
            ResourceValue on = ResourceValue.CreateBooleanRuntimeOutput(Id, true);
            await sdk.Resources.SetResourceValues([on]);

            IReadOnlyList<ResourceValue> first = await sdk.Resources.WaitForResourceValueChanges(1);
            IReadOnlyList<ResourceValue> second = await sdk.Resources.WaitForResourceValueChanges(0);

            Assert.That(first.Select(Timeless), Is.EqualTo(new[] { Timeless(on) }));
            Assert.That(second, Is.Empty);
            Assert.That(sdk.Stub.Journal.Calls<IResourceInteractionService>(nameof(IResourceInteractionService.SetResourceValues)).Single().Payload,
                Is.EqualTo(new[] { on with { ValueTime = ShapeDefaults.Instant } }), "the journal holds the decoded values");
        }

        [Test]
        public async Task AChangeOnTheController_IsReportedByTheNextWait()
        {
            var stub = new ControllerStub();
            using var sdk = StubbedSdk.Start(stub);
            ResourceValue changed = ResourceValue.CreateBooleanRuntimeInput(23386, true);

            stub.Resources.Change(changed);

            Assert.That((await sdk.Resources.WaitForResourceValueChanges(1)).Select(Timeless), Is.EqualTo(new[] { Timeless(changed) }));
            Assert.That(stub.Resources.Values[23386].Value.BoolValue, Is.True);
        }

        [Test]
        public async Task ReplacingTheValues_QueuesNoChange()
        {
            var stub = new ControllerStub();
            stub.Resources.Values = stub.Resources.Values.SetItem(Id, ResourceValue.CreateBooleanRuntimeOutput(Id, true));
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Resources.WaitForResourceValueChanges(0), Is.Empty);
        }

        /// <summary>An idle wait holds for the client's timeout capped at one second, then answers an empty list
        /// the SDK can read, not an absent one it would throw on.</summary>
        [Test]
        public async Task AnIdleWait_HoldsForAtMostOneSecond_ThenAnswersAnEmptyList()
        {
            using var sdk = StubbedSdk.Start();

            var clock = Stopwatch.StartNew();
            IReadOnlyList<ResourceValue> changes = await sdk.Resources.WaitForResourceValueChanges(15);
            TimeSpan held = clock.Elapsed;

            Assert.That(changes, Is.Empty);
            Assert.That(held, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900)).And.LessThan(TimeSpan.FromSeconds(10)));
        }

        [Test]
        public async Task AnIdleWait_WithNoTimeout_AnswersAtOnce()
        {
            using var sdk = StubbedSdk.Start();

            var clock = Stopwatch.StartNew();
            Assert.That(await sdk.Resources.WaitForResourceValueChanges(0), Is.Empty);
            Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(900)));
        }

        /// <summary>A set made while a wait holds is answered at once, and wakes the wait: the hold keeps no lock.</summary>
        [Test]
        public async Task ASetDuringAHeldWait_IsAnswered_AndWakesTheWait()
        {
            using var sdk = StubbedSdk.Start();
            ResourceValue on = ResourceValue.CreateBooleanRuntimeOutput(Id, true);

            var clock = Stopwatch.StartNew();
            Task<IReadOnlyList<ResourceValue>> wait = sdk.Resources.WaitForResourceValueChanges(1);
            await Task.Delay(100);
            Assert.That(await sdk.Resources.SetResourceValues([on]), Is.True);
            IReadOnlyList<ResourceValue> changes = await wait;

            Assert.That(changes.Select(Timeless), Is.EqualTo(new[] { Timeless(on) }));
            Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(900)), "the wait answered when the change arrived");
        }

        /// <summary>A read stamps the moment it was made, which is no part of the value.</summary>
        internal static ResourceValue Timeless(ResourceValue value) => value with { ValueTime = default };
    }
}
