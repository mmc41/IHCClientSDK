using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Soap.Resourceinteraction;
using NUnit.Framework;
// The SDK service and the generated SOAP contract it wraps differ only by namespace; alias the contract, as
// StreamingTelemetryTests does, so the two stay apart at a glance.
using SoapContract = Ihc.Soap.Resourceinteraction.ResourceInteractionService;

namespace Ihc.Tests
{
    /// <summary>
    /// The resource service around its mapper: the reads and writes an installation's runtime state travels
    /// through, and the subscription lifecycle the change stream is built on.
    ///
    /// The value mapping itself is covered twice over already - by
    /// <c>ResourceValuePropertyTests</c> as a law and by <c>OpenApiResourceValueMappingTests</c> as a
    /// differential - so what is left, and what this fixture is for, is the service logic AROUND that mapper:
    /// which wire field each operation reads, what it does with an entry the controller left empty, and what
    /// the change stream does with a subscription once the consumer stops listening. Those are separate
    /// hand-written bodies per operation, and none of them was reachable before the mapper was extracted.
    ///
    /// <b>No test here waits for a duration.</b> The polling loop rests briefly between calls, so a test that
    /// asserted on elapsed time would be measuring the machine; every case below is driven by an explicit
    /// signal - a poll that cancels, a consumer that breaks - and asserts on what was called.
    /// </summary>
    [TestFixture]
    public class ResourceInteractionServiceTests
    {
        private static ResourceInteractionService NewService(SoapContract soap)
        {
            return new ResourceInteractionService(FakeSession.Over(), soap);
        }

        private static WSResourceValueEnvelope Envelope(int resourceId, bool value = true) => new()
        {
            resourceID = resourceId,
            typeString = TypeStrings.DatalineInput,
            isValueRuntime = true,
            value = new WSBooleanValue { value = value }
        };

        private static ResourceValue Value(int resourceId, bool value = true) => new()
        {
            ResourceID = resourceId,
            TypeString = TypeStrings.DatalineOutput,
            IsValueRuntime = true,
            Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.BOOL, BoolValue = value }
        };

        // ---------------------------------------------------------------- the dataline inventories

        /// <summary>
        /// The dataline inventories are separate bodies over one mapper, and each reads its OWN wire
        /// field - the shape where a copy-pasted body reads the neighbour's response and reports the wrong half
        /// of the installation. Driven together so a body added later has an obvious place to go.
        /// </summary>
        [Test]
        public async Task TheDatalineInventories_EachReadTheirOwnResponse()
        {
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.getAllDatalineInputsAsync(A<inputMessageName12>._))
                .Returns(Task.FromResult(new outputMessageName12([new WSDatalineResource { resourceID = 1, datalineNumber = 11 }])));
            A.CallTo(() => soap.getAllDatalineOutputsAsync(A<inputMessageName13>._))
                .Returns(Task.FromResult(new outputMessageName13([new WSDatalineResource { resourceID = 2, datalineNumber = 22 }])));
            A.CallTo(() => soap.getExtraDatalineInputsAsync(A<inputMessageName10>._))
                .Returns(Task.FromResult(new outputMessageName10([new WSDatalineResource { resourceID = 3, datalineNumber = 33 }])));
            A.CallTo(() => soap.getExtraDatalineOutputsAsync(A<inputMessageName11>._))
                .Returns(Task.FromResult(new outputMessageName11([new WSDatalineResource { resourceID = 4, datalineNumber = 44 }])));
            ResourceInteractionService service = NewService(soap);

            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await service.GetAllDatalineInputs(),
                    Is.EqualTo(new[] { new DatalineResource { ResourceID = 1, DatalineNumber = 11 } }));
                Assert.That(await service.GetAllDatalineOutputs(),
                    Is.EqualTo(new[] { new DatalineResource { ResourceID = 2, DatalineNumber = 22 } }));
                Assert.That(await service.GetExtraDatalineInputs(),
                    Is.EqualTo(new[] { new DatalineResource { ResourceID = 3, DatalineNumber = 33 } }));
                Assert.That(await service.GetExtraDatalineOutputs(),
                    Is.EqualTo(new[] { new DatalineResource { ResourceID = 4, DatalineNumber = 44 } }));
            });
        }

        /// <summary>
        /// An entry the controller left empty is DROPPED from an inventory rather than surfaced as a null the
        /// caller has to defend against - these lists carry no positional meaning, unlike the OpenAPI value
        /// read, where an empty entry is refused because position is what binds a value to its resource.
        /// </summary>
        [Test]
        public async Task ADatalineInventory_DropsAnEntryTheControllerLeftEmpty()
        {
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.getAllDatalineInputsAsync(A<inputMessageName12>._))
                .Returns(Task.FromResult(new outputMessageName12(
                    [null!, new WSDatalineResource { resourceID = 7, datalineNumber = 77 }])));

            IReadOnlyList<DatalineResource> inputs = await NewService(soap).GetAllDatalineInputs();

            Assert.That(inputs, Is.EqualTo(new[] { new DatalineResource { ResourceID = 7, DatalineNumber = 77 } }));
        }

        // ---------------------------------------------------------------- the value reads

        [Test]
        public async Task GetRuntimeValue_ReadsTheEnvelopeTheControllerAnswered()
        {
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.getRuntimeValueAsync(A<inputMessageName14>._))
                .Returns(Task.FromResult(new outputMessageName14(Envelope(42))));

            ResourceValue value = await NewService(soap).GetRuntimeValue(42);

            Assert.Multiple(() =>
            {
                Assert.That(value.ResourceID, Is.EqualTo(42));
                Assert.That(value.Value.ValueKind, Is.EqualTo(ResourceValue.ValueKind.BOOL));
                Assert.That(value.Value.BoolValue, Is.True);
            });
        }

        /// <summary>
        /// A single-value read returns a NON-nullable value, so "the controller answered with nothing" cannot be
        /// expressed as a return - it is refused with a code instead of handed back as a hollow value the caller
        /// would read as false.
        /// </summary>
        [TestCaseSource(nameof(SingleValueReads))]
        public void ASingleValueRead_WithNoAnswer_IsRefusedRatherThanReturnedHollow(
            Func<ResourceInteractionService, Task<ResourceValue>> read)
        {
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.getRuntimeValueAsync(A<inputMessageName14>._))
                .Returns(Task.FromResult(new outputMessageName14(null)));
            A.CallTo(() => soap.getInitialValueAsync(A<inputMessageName15>._))
                .Returns(Task.FromResult(new outputMessageName15(null)));
            ResourceInteractionService service = NewService(soap);

            var thrown = Assert.CatchAsync<ErrorWithCodeException>(async () => await read(service));

            Assert.That(thrown!.Message, Does.Contain("42"), "and names the resource that could not be read");
        }

        /// <summary>The two reads that answer with a single value. Named by the CALL rather than by a string
        /// the case body has to re-interpret, so a case cannot silently exercise the other one.</summary>
        private static IEnumerable<TestCaseData> SingleValueReads()
        {
            yield return new TestCaseData(
                (Func<ResourceInteractionService, Task<ResourceValue>>)(s => s.GetRuntimeValue(42)))
                .SetName("{m}(runtime)");
            yield return new TestCaseData(
                (Func<ResourceInteractionService, Task<ResourceValue>>)(s => s.GetInitialValue(42)))
                .SetName("{m}(initial)");
        }

        [Test]
        public async Task TheBulkValueReads_MapEveryEnvelopeAndDropTheEmptyOnes()
        {
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.getRuntimeValuesAsync(A<inputMessageName16>._))
                .Returns(Task.FromResult(new outputMessageName16([Envelope(1), null!, Envelope(2, false)])));
            A.CallTo(() => soap.getInitialValuesAsync(A<inputMessageName17>._))
                .Returns(Task.FromResult(new outputMessageName17([Envelope(3), null!])));
            ResourceInteractionService service = NewService(soap);

            IReadOnlyList<ResourceValue> runtime = await service.GetRuntimeValues([1, 2]);
            IReadOnlyList<ResourceValue> initial = await service.GetInitialValues([3]);

            Assert.Multiple(() =>
            {
                Assert.That(runtime.Select(v => v.ResourceID), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(runtime.Select(v => v.Value.BoolValue), Is.EqualTo(new bool?[] { true, false }));
                Assert.That(initial.Select(v => v.ResourceID), Is.EqualTo(new[] { 3 }));
            });
        }

        [Test]
        public async Task GetResourceType_ReadsTheTypeStringVerbatim()
        {
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.getResourceTypeAsync(A<inputMessageName19>._))
                .Returns(Task.FromResult(new outputMessageName19("resource_scene")));

            Assert.That(await NewService(soap).GetResourceType(9), Is.EqualTo("resource_scene"));
        }

        /// <summary>
        /// Logged data carries its timestamp as epoch SECONDS - a second, separate date convention on this
        /// service, and the one place a wrong unit would put a whole history a thousand-fold out.
        /// </summary>
        [Test]
        public async Task GetLoggedData_ReadsItsTimestampsAsEpochSeconds()
        {
            var when = new DateTimeOffset(2026, 3, 4, 9, 30, 15, TimeSpan.Zero);
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.getLoggedDataAsync(A<inputMessageName20>._))
                .Returns(Task.FromResult(new outputMessageName20(
                    [new WSLoggedData { id = 5, value = "on", timestamp = when.ToUnixTimeSeconds() }])));

            IReadOnlyList<LoggedData> logged = await NewService(soap).GetLoggedData(9);

            Assert.That(logged, Is.EqualTo(new[] { new LoggedData { Id = 5, Value = "on", Timestamp = when } }));
        }

        private static Task<IReadOnlyList<EnumDefinition>> DefinitionsFrom(params WSEnumDefinition?[] reported)
        {
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.getEnumeratorDefinitionsAsync(A<inputMessageName9>._))
                .Returns(Task.FromResult(new outputMessageName9(reported!)));
            return NewService(soap).GetEnumeratorDefinitions();
        }

        [Test]
        public async Task GetEnumeratorDefinitions_MapsEachDefinitionAndDropsTheValuesTheWireLeftEmpty()
        {
            IReadOnlyList<EnumDefinition> definitions = await DefinitionsFrom(
                new WSEnumDefinition
                {
                    enumeratorDefinitionID = 3,
                    enumeratorValues = [new WSEnumValue { definitionTypeID = 3, enumValueID = 1, enumName = "Aften" }, null!],
                },
                null);

            Assert.Multiple(() =>
            {
                Assert.That(definitions, Has.Count.EqualTo(1), "the empty definition is dropped");
                Assert.That(definitions[0].EnumeratorDefinitionID, Is.EqualTo(3));
                Assert.That(definitions[0].Values,
                    Is.EqualTo(new[] { new EnumValue { DefinitionTypeID = 3, EnumValueID = 1, EnumName = "Aften" } }));
            });
        }

        /// <summary>A definition with no values at all is an EMPTY list, never a null the caller must guard.</summary>
        [Test]
        public async Task GetEnumeratorDefinitions_WithNoValues_ReportsAnEmptyListRatherThanNull()
        {
            IReadOnlyList<EnumDefinition> definitions = await DefinitionsFrom(
                new WSEnumDefinition { enumeratorDefinitionID = 4, enumeratorValues = null });

            Assert.That(definitions.Single().Values, Is.Empty);
        }

        [Test]
        public async Task TheScenePositionReads_MapTheirOwnResponses()
        {
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.getSceneGroupResourceIdAndPositionsAsync(A<inputMessageName1>._))
                .Returns(Task.FromResult(new outputMessageName1(
                [
                    new WSSceneResourceIdAndLocationURLs
                    {
                        sceneResourceId = 8,
                        scenePositionSeenFromProduct = "p",
                        scenePositionSeenFromFunctionBlock = "fb",
                    },
                    null!,
                ])));
            A.CallTo(() => soap.getScenePositionsForSceneValueResourceAsync(A<inputMessageName2>._))
                .Returns(Task.FromResult(new outputMessageName2(null)));
            ResourceInteractionService service = NewService(soap);

            IReadOnlyList<SceneResourceIdAndLocation> group = await service.GetSceneGroupResourceIdAndPositions(8);

            await Assert.MultipleAsync(async () =>
            {
                Assert.That(group, Has.Count.EqualTo(1), "an empty entry is dropped");
                Assert.That(group[0].SceneResourceId, Is.EqualTo(8));
                Assert.That(group[0].ScenePositionSeenFromProduct, Is.EqualTo("p"));
                Assert.That(group[0].ScenePositionSeenFromFunctionBlock, Is.EqualTo("fb"));
                Assert.That(await service.GetScenePositionsForSceneValueResource(8), Is.Null,
                    "the single-position read reports absence as null, which its signature allows");
            });
        }

        // ---------------------------------------------------------------- the writes

        /// <summary>
        /// A write to a real installation. The envelope has to carry the resource it addresses and the value it
        /// sets, and the acknowledgement has to be reported as the controller gave it - a write reported as
        /// successful on no answer is a switch a caller believes it moved.
        /// </summary>
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(null, false)]
        public async Task SetResourceValue_SendsTheValueAndReportsTheControllersAnswer(bool? answered, bool expected)
        {
            WSResourceValueEnvelope? sent = null;
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.setResourceValueAsync(A<inputMessageName18>._))
                .Invokes((inputMessageName18 m) => sent = m.setResourceValue1)
                .Returns(Task.FromResult(new outputMessageName18(answered)));

            bool result = await NewService(soap).SetResourceValue(Value(42, value: true));

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(sent!.resourceID, Is.EqualTo(42));
                Assert.That(sent.typeString, Is.EqualTo(TypeStrings.DatalineOutput));
                Assert.That(sent.value, Is.InstanceOf<WSBooleanValue>());
                Assert.That(((WSBooleanValue)sent.value).value, Is.True);
            });
        }

        [Test]
        public async Task SetResourceValues_SendsEveryValueInTheOrderGiven()
        {
            WSResourceValueEnvelope[]? sent = null;
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.setResourceValuesAsync(A<inputMessageName3>._))
                .Invokes((inputMessageName3 m) => sent = m.setResourceValues1)
                .Returns(Task.FromResult(new outputMessageName3(true)));

            bool result = await NewService(soap).SetResourceValues([Value(1, true), Value(2, false)]);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(sent!.Select(e => e.resourceID), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(sent!.Select(e => ((WSBooleanValue)e.value).value), Is.EqualTo(new[] { true, false }));
            });
        }

        // ---------------------------------------------------------------- the subscription lifecycle

        /// <summary>
        /// Enabling a subscription answers with the values as they stand, so a consumer starts from a known
        /// state rather than from whatever the first change happens to report. Both notification kinds are
        /// separate bodies over the same mapper.
        /// </summary>
        [Test]
        public async Task EnablingNotifications_SubscribesTheGivenResourcesAndAnswersWithTheirCurrentValues()
        {
            int[]? runtimeIds = null;
            int[]? initialIds = null;
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.enableRuntimeValueNotificationsAsync(A<inputMessageName4>._))
                .Invokes((inputMessageName4 m) => runtimeIds = m.enableRuntimeValueNotifications1)
                .Returns(Task.FromResult(new outputMessageName4([Envelope(1), null!])));
            A.CallTo(() => soap.enableInitialValueNotificationsAsync(A<inputMessageName6>._))
                .Invokes((inputMessageName6 m) => initialIds = m.enableInitialValueNotifications1)
                .Returns(Task.FromResult(new outputMessageName6([Envelope(2)])));
            ResourceInteractionService service = NewService(soap);

            IReadOnlyList<ResourceValue> runtime = await service.EnableRuntimeValueNotifications([1, 2]);
            IReadOnlyList<ResourceValue> initial = await service.EnableInitialValueNotifications([3]);

            Assert.Multiple(() =>
            {
                Assert.That(runtimeIds, Is.EqualTo(new[] { 1, 2 }));
                Assert.That(initialIds, Is.EqualTo(new[] { 3 }));
                Assert.That(runtime.Select(v => v.ResourceID), Is.EqualTo(new[] { 1 }), "an empty envelope is dropped");
                Assert.That(initial.Select(v => v.ResourceID), Is.EqualTo(new[] { 2 }));
            });
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(null, false)]
        public async Task DisablingNotifications_ReportsTheControllersAnswer(bool? answered, bool expected)
        {
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.disableRuntimeValueNotifactionsAsync(A<inputMessageName5>._))
                .Returns(Task.FromResult(new outputMessageName5(answered)));
            A.CallTo(() => soap.disableInitialValueNotifactionsAsync(A<inputMessageName7>._))
                .Returns(Task.FromResult(new outputMessageName7(answered)));
            ResourceInteractionService service = NewService(soap);

            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await service.DisableRuntimeValueNotifactions([1]), Is.EqualTo(expected));
                Assert.That(await service.DisableInitialValueNotifactions([1]), Is.EqualTo(expected));
            });
        }

        [Test]
        public async Task WaitForResourceValueChanges_SendsItsTimeoutAndMapsWhatCameBack()
        {
            int? timeout = null;
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.waitForResourceValueChangesAsync(A<inputMessageName8>._))
                .Invokes((inputMessageName8 m) => timeout = m.waitForResourceValueChanges1)
                .Returns(Task.FromResult(new outputMessageName8([Envelope(5), null!])));

            IReadOnlyList<ResourceValue> changes = await NewService(soap).WaitForResourceValueChanges(7);

            Assert.Multiple(() =>
            {
                Assert.That(timeout, Is.EqualTo(7), "the controller decides how long to hold the poll open");
                Assert.That(changes.Select(v => v.ResourceID), Is.EqualTo(new[] { 5 }));
            });
        }

        // ---------------------------------------------------------------- the change stream

        /// <summary>
        /// A SOAP layer whose long poll answers <paramref name="polls"/> in order and then CANCELS - the
        /// explicit signal that ends the loop, in place of a duration for the test to wait out.
        /// </summary>
        private static SoapContract PollingSoap(CancellationTokenSource stopAfterLastPoll,
            params WSResourceValueEnvelope[][] polls)
        {
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.enableRuntimeValueNotificationsAsync(A<inputMessageName4>._))
                .Returns(Task.FromResult(new outputMessageName4([])));
            A.CallTo(() => soap.disableRuntimeValueNotifactionsAsync(A<inputMessageName5>._))
                .Returns(Task.FromResult(new outputMessageName5(true)));

            int poll = 0;
            A.CallTo(() => soap.waitForResourceValueChangesAsync(A<inputMessageName8>._))
                .ReturnsLazily(() =>
                {
                    WSResourceValueEnvelope[] answer = polls[Math.Min(poll, polls.Length - 1)];
                    if (++poll >= polls.Length)
                    {
                        stopAfterLastPoll.Cancel();
                    }
                    return Task.FromResult(new outputMessageName8(answer));
                });
            return soap;
        }

        private static async Task<List<ResourceValue>> DrainAsync(
            ResourceInteractionService service, CancellationToken token, int? stopAfter = null)
        {
            List<ResourceValue> seen = [];
            try
            {
                await foreach (ResourceValue change in service.GetResourceValueChanges([1, 2], token))
                {
                    seen.Add(change);
                    if (stopAfter is { } limit && seen.Count >= limit)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // The signal that ends the loop; not a failure of the stream.
            }
            return seen;
        }

        /// <summary>
        /// The happy path of the loop: every value a poll reports reaches the consumer, in the order the
        /// controller reported it, across more than one poll - the detail a single-poll test cannot see.
        /// </summary>
        [Test]
        public async Task GetResourceValueChanges_YieldsEveryPollsChangesInOrder()
        {
            using var stop = new CancellationTokenSource();
            ResourceInteractionService service = NewService(PollingSoap(stop,
                [Envelope(1), Envelope(2)],
                [Envelope(3)]));

            List<ResourceValue> seen = await DrainAsync(service, stop.Token);

            Assert.That(seen.Select(v => v.ResourceID), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        /// <summary>
        /// The lifecycle the stream owes the controller: it subscribes before polling and UNSUBSCRIBES when the
        /// consumer stops - including when the consumer simply walks away from the loop rather than cancelling.
        /// A subscription left behind keeps the controller pushing to nobody.
        /// </summary>
        [Test]
        public async Task GetResourceValueChanges_UnsubscribesWhenTheConsumerStopsListening()
        {
            using var stop = new CancellationTokenSource();
            SoapContract soap = PollingSoap(stop, [Envelope(1), Envelope(2)]);

            List<ResourceValue> seen = await DrainAsync(NewService(soap), stop.Token, stopAfter: 1);

            Assert.Multiple(() =>
            {
                Assert.That(seen, Has.Count.EqualTo(1), "the consumer left after the first change");
                A.CallTo(() => soap.enableRuntimeValueNotificationsAsync(A<inputMessageName4>._))
                    .MustHaveHappenedOnceExactly();
                A.CallTo(() => soap.disableRuntimeValueNotifactionsAsync(A<inputMessageName5>._))
                    .MustHaveHappenedOnceExactly();
            });
        }

        /// <summary>
        /// A subscription that cannot be established is not a stream with no changes - it is a failure, and it
        /// has to surface rather than leaving the consumer waiting on a loop that never had a subscription.
        /// </summary>
        [Test]
        public void GetResourceValueChanges_WhenTheSubscriptionCannotBeEstablished_FailsRatherThanPolling()
        {
            var soap = A.Fake<SoapContract>();
            A.CallTo(() => soap.enableRuntimeValueNotificationsAsync(A<inputMessageName4>._))
                .Throws(new InvalidOperationException("the controller refused the subscription"));
            A.CallTo(() => soap.disableRuntimeValueNotifactionsAsync(A<inputMessageName5>._))
                .Returns(Task.FromResult(new outputMessageName5(true)));
            ResourceInteractionService service = NewService(soap);

            Assert.CatchAsync<InvalidOperationException>(async () =>
            {
                await foreach (ResourceValue _ in service.GetResourceValueChanges([1], CancellationToken.None))
                {
                }
            });

            A.CallTo(() => soap.waitForResourceValueChangesAsync(A<inputMessageName8>._)).MustNotHaveHappened();
        }

        /// <summary>
        /// The unsubscribe runs in a <c>finally</c> and is deliberately BEST EFFORT: a controller that refuses
        /// it must not turn the consumer's own cancellation into a different exception, which would hide why the
        /// stream ended.
        /// </summary>
        [Test]
        public async Task GetResourceValueChanges_WhenUnsubscribingFails_DoesNotMaskWhyTheStreamEnded()
        {
            using var stop = new CancellationTokenSource();
            SoapContract soap = PollingSoap(stop, [Envelope(1)]);
            A.CallTo(() => soap.disableRuntimeValueNotifactionsAsync(A<inputMessageName5>._))
                .Throws(new InvalidOperationException("the controller refused the unsubscribe"));

            List<ResourceValue> seen = await DrainAsync(NewService(soap), stop.Token, stopAfter: 1);

            Assert.That(seen.Select(v => v.ResourceID), Is.EqualTo(new[] { 1 }),
                "the consumer keeps what it read, and sees no exception from the cleanup");
        }
    }
}
