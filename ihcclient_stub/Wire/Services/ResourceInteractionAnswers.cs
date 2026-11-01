using System.Collections.Immutable;
using Ihc.Soap.Resourceinteraction;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The resource interaction service's answers, all over the one store in <see cref="StubResources"/>. A list
    /// answer that has nothing to say is an empty list, never an absent one, because the SDK reads its length
    /// unchecked.
    /// </summary>
    internal sealed class ResourceInteractionAnswers(StubResources scenario) : IWireAnswers
    {
        public string Service => "ResourceInteractionService";

        internal Task<outputMessageName16> getRuntimeValuesAsync(inputMessageName16 request) =>
            Task.FromResult(new outputMessageName16 { getRuntimeValues2 = ReadToWire(request.getRuntimeValues1, isRuntime: true) });

        internal Task<outputMessageName17> getInitialValuesAsync(inputMessageName17 request) =>
            Task.FromResult(new outputMessageName17 { getInitialValues2 = ReadToWire(request.getInitialValues1, isRuntime: false) });

        internal Task<outputMessageName3> setResourceValuesAsync(inputMessageName3 request)
        {
            ResourceValue[] values = [.. (request.setResourceValues1 ?? []).Select(ResourceValueWire.FromWire)];
            AnswerContext.RecordPayload(values);
            scenario.Write(values);
            return Task.FromResult(new outputMessageName3 { setResourceValues2 = true });
        }

        internal Task<outputMessageName14> getRuntimeValueAsync(inputMessageName14 request) =>
            Task.FromResult(new outputMessageName14 { getRuntimeValue2 = ReadToWire([request.getRuntimeValue1 ?? 0], isRuntime: true).Single() });

        internal Task<outputMessageName15> getInitialValueAsync(inputMessageName15 request) =>
            Task.FromResult(new outputMessageName15 { getInitialValue2 = ReadToWire([request.getInitialValue1 ?? 0], isRuntime: false).Single() });

        internal Task<outputMessageName18> setResourceValueAsync(inputMessageName18 request)
        {
            ResourceValue value = ResourceValueWire.FromWire(request.setResourceValue1);
            AnswerContext.RecordPayload(value);
            scenario.Write([value]);
            return Task.FromResult(new outputMessageName18 { setResourceValue2 = true });
        }

        internal Task<outputMessageName12> getAllDatalineInputsAsync(inputMessageName12 request) =>
            Task.FromResult(new outputMessageName12
            {
                getAllDatalineInputs1 = [.. scenario.DatalineInputs.Select(input => new WSDatalineResource
                {
                    datalineNumber = input.DatalineNumber,
                    resourceID = input.ResourceID,
                })],
            });

        internal Task<outputMessageName20> getLoggedDataAsync(inputMessageName20 request) =>
            Task.FromResult(new outputMessageName20
            {
                getLoggedData2 = [.. scenario.LoggedData.GetValueOrDefault(request.getLoggedData1 ?? 0, []).Select(logged => new WSLoggedData
                {
                    id = logged.Id,
                    value = logged.Value,
                    timestamp = logged.Timestamp.ToUnixTimeSeconds(),
                })],
            });

        internal Task<outputMessageName1> getSceneGroupResourceIdAndPositionsAsync(inputMessageName1 request) =>
            Task.FromResult(new outputMessageName1
            {
                getSceneGroupResourceIdAndPositions2 =
                    [.. scenario.SceneGroupPositions.GetValueOrDefault(request.getSceneGroupResourceIdAndPositions1 ?? 0, []).Select(PositionToWire)],
            });

        internal Task<outputMessageName2> getScenePositionsForSceneValueResourceAsync(inputMessageName2 request)
        {
            int id = request.getScenePositionsForSceneValueResource1 ?? 0;
            SceneResourceIdAndLocation position = scenario.ScenePositions.GetValueOrDefault(id)
                ?? new SceneResourceIdAndLocation { SceneResourceId = id, ScenePositionSeenFromFunctionBlock = "", ScenePositionSeenFromProduct = "" };
            return Task.FromResult(new outputMessageName2 { getScenePositionsForSceneValueResource2 = PositionToWire(position) });
        }

        /// <summary>Enabling notifications answers the current value of each resource, as a real controller does.</summary>
        internal Task<outputMessageName4> enableRuntimeValueNotificationsAsync(inputMessageName4 request) =>
            Task.FromResult(new outputMessageName4 { enableRuntimeValueNotifications2 = ReadToWire(request.enableRuntimeValueNotifications1, isRuntime: true) });

        internal Task<outputMessageName6> enableInitialValueNotificationsAsync(inputMessageName6 request) =>
            Task.FromResult(new outputMessageName6 { enableInitialValueNotifications2 = ReadToWire(request.enableInitialValueNotifications1, isRuntime: false) });

        internal static Task<outputMessageName5> disableRuntimeValueNotifactionsAsync(inputMessageName5 request) =>
            Task.FromResult(new outputMessageName5 { disableRuntimeValueNotifactions2 = true });

        internal static Task<outputMessageName7> disableInitialValueNotifactionsAsync(inputMessageName7 request) =>
            Task.FromResult(new outputMessageName7 { disableInitialValueNotifactions2 = true });

        /// <summary>The queued changes, held for by the <see cref="LongPoll"/> rule.</summary>
        internal async Task<outputMessageName8> waitForResourceValueChangesAsync(inputMessageName8 request)
        {
            ImmutableList<ResourceValue> changes =
                await LongPoll.TakeAll(scenario.Changes, request.waitForResourceValueChanges1).ConfigureAwait(false);
            return new outputMessageName8
            {
                waitForResourceValueChanges2 = [.. changes.Select(change => ResourceValueWire.ToWire(change, change.IsValueRuntime))],
            };
        }

        /// <summary>One value per requested id, in the order asked.</summary>
        private WSResourceValueEnvelope[] ReadToWire(int[]? ids, bool isRuntime) =>
            [.. scenario.Read(ids ?? []).Select(value => ResourceValueWire.ToWire(value, isRuntime))];

        private static WSSceneResourceIdAndLocationURLs PositionToWire(SceneResourceIdAndLocation position) => new()
        {
            sceneResourceId = position.SceneResourceId,
            scenePositionSeenFromFunctionBlock = position.ScenePositionSeenFromFunctionBlock,
            scenePositionSeenFromProduct = position.ScenePositionSeenFromProduct,
        };
    }
}
