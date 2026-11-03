using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Soap.Controller;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// OpenAPI's login and project operations answer from the stub's shared state, read through the real
    /// <see cref="OpenAPIService"/>: the one password rule and the one stored project.
    /// </summary>
    [TestFixture]
    public class StubOpenApiTests
    {
        private const int Size = 16;

        private static readonly ProjectFile Uploaded = new("uploaded.vis",
            "<utcs_project>\r\n  <name>Køkken æøå</name>\n</utcs_project>\r\n");

        /// <summary>
        /// A change on the controller is an event OpenAPI's wait reports, as the resource-interaction wait reports it
        /// too: each API's wait reads its own queue, so neither takes the other's changes.
        /// </summary>
        [Test]
        public async Task AChangeOnTheController_IsReportedByTheNextEventWait_AndByTheOtherApisWait()
        {
            var stub = new ControllerStub();
            using var sdk = StubbedSdk.Start(stub);

            stub.Resources.Change(ResourceValue.CreateBooleanRuntimeInput(23386, true));

            EventPackage events = await sdk.OpenApi.WaitForEvents(1);
            Assert.That(events.ResourceValueEvents.Select(change => (change.ResourceID, change.Value.BoolValue)),
                Is.EqualTo(new[] { (23386, (bool?)true) }));
            Assert.That(events.ControllerExecutionRunning, Is.True, "the controller is running its program");
            Assert.That((await sdk.Resources.WaitForResourceValueChanges(1)).Select(change => change.ResourceID),
                Is.EqualTo(new[] { 23386 }), "the other API's wait still reports the change");
        }

        /// <summary>A read of no resources answered in the controller's empty-list form, one nil <c>arrayItem</c>,
        /// is an empty list through the whole SDK.</summary>
        [Test]
        public async Task AValueReadOfNoResources_AnsweredByOneNilItem_IsEmpty()
        {
            var stub = new ControllerStub();
            stub.OverrideRaw("OpenAPIService", "getValues", (_, _) => StubExchanges.ControllerReply("getValues2", StubExchanges.OneNilItem));
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.OpenApi.GetValues([]), Is.Empty);
        }

        /// <summary>
        /// A controller with no dataline resource of a kind answers its id list as one nil <c>arrayItem</c>, the form
        /// a 3.3.44 controller gives every empty list. Through the whole SDK both id lists read as empty.
        /// </summary>
        [TestCase("getDatalineInputIDs", "getDatalineInputIDs1", false)]
        [TestCase("getDatalineOutputIDs", "getDatalineOutputIDs1", true)]
        public async Task ADatalineIdListOfOneNilItem_ReadsAsEmpty(string action, string element, bool outputs)
        {
            var stub = new ControllerStub();
            stub.OverrideRaw("OpenAPIService", action, (_, _) => StubExchanges.ControllerReply(element, StubExchanges.OneNilItem));
            using var sdk = StubbedSdk.Start(stub);

            IReadOnlyList<int> ids = outputs ? await sdk.OpenApi.GetDatalineOutputIDs() : await sdk.OpenApi.GetDatalineInputIDs();

            Assert.That(ids, Is.Empty);
        }

        /// <summary>
        /// An event package whose event list is the controller's empty-list form, one nil <c>arrayItem</c>, reads
        /// through the whole SDK as no events, its flags as sent.
        /// </summary>
        [Test]
        public async Task AnEventListOfOneNilItem_ReadsAsNoEvents()
        {
            var stub = new ControllerStub();
            stub.OverrideRaw("OpenAPIService", "waitForEvents", (_, _) => StubExchanges.ControllerReply("waitForEvents2",
                $"<ns1:resourceValueEvents>{StubExchanges.OneNilItem}</ns1:resourceValueEvents>" +
                "<ns1:controllerExecutionRunning>true</ns1:controllerExecutionRunning><ns1:subscriptionAmount>1</ns1:subscriptionAmount>"));
            using var sdk = StubbedSdk.Start(stub);

            EventPackage events = await sdk.OpenApi.WaitForEvents(1);

            Assert.That((events.ResourceValueEvents.Length, events.ControllerExecutionRunning, events.SubscriptionAmount), Is.EqualTo((0, true, 1)));
        }

        /// <summary>An idle event wait holds for the client's timeout capped at one second, as every long poll does,
        /// so a change stream over the stub waits instead of spinning.</summary>
        [Test]
        public async Task AnIdleEventWait_HoldsForAtMostOneSecond_ThenAnswersNoEvents()
        {
            using var sdk = StubbedSdk.Start();

            var clock = Stopwatch.StartNew();
            EventPackage events = await sdk.OpenApi.WaitForEvents(15);
            TimeSpan held = clock.Elapsed;

            Assert.That(events.ResourceValueEvents, Is.Empty);
            Assert.That(held, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900)).And.LessThan(TimeSpan.FromSeconds(10)));
        }

        /// <summary>OpenAPI's change stream, read through the real wrapper, yields a value written through OpenAPI.</summary>
        [Test]
        public async Task OpenApisChangeStream_YieldsAValueWrittenThroughIt()
        {
            using var sdk = StubbedSdk.Start();
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            IAsyncEnumerator<ResourceValue> changes = sdk.OpenApi.GetResourceValueChanges([23386], limit.Token).GetAsyncEnumerator(limit.Token);
            try
            {
                ValueTask<bool> next = changes.MoveNextAsync();
                Assert.That(await sdk.OpenApi.SetValues([ResourceValue.CreateBooleanRuntimeInput(23386, true)]), Is.True);

                Assert.That(await next, Is.True, "the stream yields the change before its limit");
                Assert.That((changes.Current.ResourceID, changes.Current.Value.BoolValue), Is.EqualTo((23386, (bool?)true)));
            }
            finally
            {
                await changes.DisposeAsync();
            }
        }

        [Test]
        public async Task ALogin_WithAnyNonBlankPassword_IsAccepted_AndIssuesASession()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.OpenApi.Authenticate("installer", "anything");

            StubCall login = sdk.Stub.Journal.Calls<IOpenAPIService>(nameof(IOpenAPIService.Authenticate)).Single();
            Assert.That(login.SetCookie, Does.StartWith("JSESSIONID=stub-"));
            Assert.That(login.Payload, Is.EqualTo("installer"), "the journal holds the user name, never the password");
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(ControllerStub.IllegalPassword)]
        public void ABlankOrTheIllegalPassword_IsRefused_AsTheUnknownLoginError(string password)
        {
            using var sdk = StubbedSdk.Start();

            var refusal = Assert.ThrowsAsync<ErrorWithCodeException>(() => sdk.OpenApi.Authenticate("installer", password));

            Assert.That(refusal!.ErrorCode, Is.EqualTo(Errors.LOGIN_UNKNOWN_ERROR));
            Assert.That(sdk.Stub.Journal.Calls<IOpenAPIService>(nameof(IOpenAPIService.Authenticate)).Single().SetCookie, Is.Null);
        }

        /// <summary>A project stored through the controller service is the one OpenAPI describes and serves.</summary>
        [Test]
        public async Task AProjectStoredThroughTheControllerService_IsWhatOpenApiServes()
        {
            var stub = new ControllerStub();
            stub.Controller.SegmentationSize = Size;
            using var sdk = StubbedSdk.Start(stub);

            await sdk.Controller.StoreProject(Uploaded);

            Assert.That(await sdk.OpenApi.IsIHCProjectAvailable(), Is.True);
            Assert.That(await sdk.OpenApi.GetProjectInfo(), Is.EqualTo(await sdk.Controller.GetProjectInfo()));
            Assert.That(await sdk.OpenApi.GetIHCProjectSegmentationSize(), Is.EqualTo(Size));
            int count = await sdk.OpenApi.GetIHCProjectNumberOfSegments();
            Assert.That(count, Is.EqualTo(await sdk.Controller.GetIHCProjectNumberOfSegments()).And.GreaterThan(1));
            var slices = new byte[count][];
            for (int index = 0; index < count; index++)
            {
                slices[index] = (await sdk.OpenApi.GetIHCProjectSegment(index, 1, 0)).Data;
                Assert.That(slices[index], Is.EqualTo((await sdk.Controller.GetIHCProjectSegment(index, 1, 0)).Data), $"segment {index}");
            }
            Assert.That(ProjectFile.Encoding.GetBytes(ProjectGzipText.Decompress(slices)), Is.EqualTo(ProjectFile.Encoding.GetBytes(Uploaded.Data)),
                "the segments both services serve join and decode to the stored project");
        }

        /// <summary>OpenAPI hands the segment's bytes to its caller as they arrive, so they must be exactly the bytes
        /// the controller service puts on its wire for the same segment.</summary>
        [Test]
        public async Task AnOpenApiSegment_IsTheSameGzipTheControllerServiceServes()
        {
            var stub = new ControllerStub();
            stub.Controller.SegmentationSize = Size;
            stub.Controller.Project = Uploaded;
            using var sdk = StubbedSdk.Start(stub);
            var controllerWire = new ControllerAnswers(stub.Controller);

            for (int index = 0; index < stub.Controller.NumberOfSegments(); index++)
            {
                outputMessageName15 served = await controllerWire.getIHCProjectSegmentAsync(new inputMessageName15 { getIHCProjectSegment1 = index });

                Assert.That((await sdk.OpenApi.GetIHCProjectSegment(index, 1, 0)).Data, Is.EqualTo(served.getIHCProjectSegment4.data), $"segment {index}");
            }
        }

        [Test]
        public async Task AControllerWithNoProject_HasNoneThroughOpenApi()
        {
            var stub = new ControllerStub();
            stub.Controller.Project = null;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.OpenApi.IsIHCProjectAvailable(), Is.False);
            Assert.That(await sdk.OpenApi.GetProjectInfo(), Is.Null);
            Assert.That(await sdk.OpenApi.GetIHCProjectNumberOfSegments(), Is.Zero);
        }
    }
}
