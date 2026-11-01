using System.Collections.Immutable;
using Ihc.Soap.Openapi;
using Interaction = Ihc.Soap.Resourceinteraction;

namespace Ihc.Stubbing
{
    /// <summary>
    /// OpenAPI's answers, over the state the other services answer from: one password rule, one stored project and
    /// one resource store. A value written through one API therefore reads back through every other.
    /// </summary>
    internal sealed class OpenApiAnswers(StubAuthentication authentication, StubController controller, StubResources resources) : IWireAnswers
    {
        public string Service => "OpenAPIService";

        /// <summary>The authentication service's login rule. This answer is a bare flag, with no account-invalid
        /// reason, so a refusal reaches the caller as the SDK's unknown login error.</summary>
        internal Task<outputMessageName13> authenticateAsync(inputMessageName13 request) =>
            Task.FromResult(new outputMessageName13
            {
                authenticate3 = AuthenticationAnswers.Login(authentication, request.authenticate1, request.authenticate2),
            });

        internal Task<outputMessageName16> isIHCProjectAvailableAsync(inputMessageName16 request) =>
            Task.FromResult(new outputMessageName16 { isIHCProjectAvailable1 = controller.Project is not null });

        /// <summary>The project info the controller service answers, in this service's terms.</summary>
        internal Task<outputMessageName14> getProjectInfoAsync(inputMessageName14 request) =>
            Task.FromResult(new outputMessageName14
            {
                getProjectInfo1 = GeneratedTwin.Translate<WSProjectInfo>(ControllerAnswers.ProjectInfoToWire(controller.StoredProjectInfo())),
            });

        internal Task<outputMessageName18> getIHCProjectSegmentationSizeAsync(inputMessageName18 request) =>
            Task.FromResult(new outputMessageName18 { getIHCProjectSegmentationSize1 = controller.SegmentationSize });

        internal Task<outputMessageName19> getIHCProjectNumberOfSegmentsAsync(inputMessageName19 request) =>
            Task.FromResult(new outputMessageName19 { getIHCProjectNumberOfSegments1 = controller.NumberOfSegments() });

        /// <summary>The segment the controller service answers, gzip and all: the SDK hands these bytes to its caller
        /// as they arrive.</summary>
        internal Task<outputMessageName17> getIHCProjectSegmentAsync(inputMessageName17 request) =>
            Task.FromResult(new outputMessageName17
            {
                getIHCProjectSegment4 = GeneratedTwin.Translate<WSFile>(ControllerAnswers.ProjectToWire(controller.Segment(request.getIHCProjectSegment1 ?? -1))),
            });

        /// <summary>One bare value per requested id, in request order: this wire names no resource in a value, so the
        /// SDK pairs each with the id asked for at its position.</summary>
        internal Task<outputMessageName6> getValuesAsync(inputMessageName6 request) =>
            Task.FromResult(new outputMessageName6
            {
                getValues2 =
                [
                    .. resources.Read(request.getValues1 ?? []).Select(value =>
                        GeneratedTwin.Translate<WSResourceValue>(ResourceValueWire.ValueToWire(value.Value))!),
                ],
            });

        internal Task<outputMessageName7> setValuesAsync(inputMessageName7 request)
        {
            ImmutableArray<ResourceValue> written = resources.WriteBare((request.setValues1 ?? []).Select(change =>
                (change.m_resourceID, ResourceValueWire.ValueFromWire(GeneratedTwin.Translate<Interaction.WSResourceValue>(change.m_value)))));
            AnswerContext.RecordPayload(written);
            return Task.FromResult(new outputMessageName7 { setValues2 = true });
        }

        /// <summary>
        /// The changes queued for this API, held for by the <see cref="LongPoll"/> rule, so a change stream over the
        /// stub waits between reads rather than spinning. The program runs while the controller is ready. The stub
        /// keeps no count of subscriptions, so it reports none.
        /// </summary>
        internal async Task<outputMessageName5> waitForEventsAsync(inputMessageName5 request)
        {
            ImmutableList<ResourceValue> changes = await LongPoll.TakeAll(resources.Events, request.waitForEvents1).ConfigureAwait(false);
            return new outputMessageName5
            {
                waitForEvents2 = new WSEventPackage
                {
                    resourceValueEvents =
                    [
                        .. changes.Select(change => new WSResourceValueEvent
                        {
                            m_resourceID = change.ResourceID,
                            m_value = GeneratedTwin.Translate<WSResourceValue>(ResourceValueWire.ValueToWire(change.Value))!,
                        }),
                    ],
                    controllerExecutionRunning = controller.State == ControllerState.Ready,
                    subscriptionAmount = 0,
                },
            };
        }
    }
}
