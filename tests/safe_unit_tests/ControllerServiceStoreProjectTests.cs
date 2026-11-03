using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc;
using Ihc.Soap.Controller;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Tests
{
    /// <summary>
    /// <see cref="ControllerService.StoreProject"/> semantics against a faked SOAP layer: a transport failure
    /// mid-upload must surface as the original exception (never replaced by the change-mode cleanup's own
    /// failure), non-Latin-1 project text must fail before the controller is put into change mode instead of
    /// being silently stored as '?' replacements, and the state waits follow the controller's long poll.
    /// </summary>
    /// <remarks>
    /// The default harness reads <c>ready</c> from every <c>getState</c>, so each wait for
    /// <see cref="ControllerState.Initialize"/> goes on to a long poll, which it answers from a sequence.
    /// </remarks>
    [TestFixture]
    public class ControllerServiceStoreProjectTests
    {
        private static ControllerService NewService(Ihc.Soap.Controller.ControllerService soap, TimeProvider? clock = null)
        {
            var settings = new IhcSettings { Endpoint = "http://unit.test.local" };
            var auth = A.Fake<IAuthenticationService>();
            A.CallTo(() => auth.IhcSettings).Returns(settings);
            return new ControllerService(auth, soap, clock);
        }

        private static Ihc.Soap.Controller.ControllerService HappySoap()
        {
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            A.CallTo(() => soap.getStateAsync(A<inputMessageName1>._))
                .Returns(Task.FromResult(new outputMessageName1(new WSControllerState { state = "text.ctrl.state.ready" })));
            A.CallTo(() => soap.isSDCardReadyAsync(A<inputMessageName9>._))
                .Returns(Task.FromResult(new outputMessageName9(true)));
            A.CallTo(() => soap.isIHCProjectAvailableAsync(A<inputMessageName14>._))
                .Returns(Task.FromResult(new outputMessageName14(true)));
            A.CallTo(() => soap.enterProjectChangeModeAsync(A<inputMessageName12>._))
                .Returns(Task.FromResult(new outputMessageName12(true)));
            A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._))
                .Returns(Task.FromResult(new outputMessageName13(true)));
            A.CallTo(() => soap.storeIHCProjectAsync(A<inputMessageName4>._))
                .Returns(Task.FromResult(new outputMessageName4(true)));
            A.CallTo(() => soap.getIHCProjectSegmentationSizeAsync(A<inputMessageName17>._))
                .Returns(Task.FromResult(new outputMessageName17(7_500)));
            A.CallTo(() => soap.storeIHCProjectSegmentAsync(A<inputMessageName16>._))
                .Returns(Task.FromResult(new outputMessageName16(true)));
            A.CallTo(() => soap.waitForControllerStateChangeAsync(A<inputMessageName19>._))
                .ReturnsNextFromSequence(
                    Task.FromResult(new outputMessageName19(new WSControllerState { state = "text.ctrl.state.initialize" })),
                    Task.FromResult(new outputMessageName19(new WSControllerState { state = "text.ctrl.state.initialize" })),
                    Task.FromResult(new outputMessageName19(new WSControllerState { state = "text.ctrl.state.ready" })));
            return soap;
        }

        [Test]
        public async Task StoreProject_HappyPath_ReturnsTrue_AndExitsChangeMode()
        {
            var soap = HappySoap();
            var service = NewService(soap);

            bool stored = await service.StoreProject(new ProjectFile("Project.ihc", "<utcs_project/>"));

            Assert.That(stored, Is.True);
            A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void StoreProject_TransportFailure_SurfacesTheTransportException_NotACleanupError()
        {
            var soap = HappySoap();
            A.CallTo(() => soap.storeIHCProjectAsync(A<inputMessageName4>._))
                .ThrowsAsync(new HttpRequestException("network dropped mid-upload"));
            A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._))
                .ThrowsAsync(new HttpRequestException("still down"));
            var service = NewService(soap);

            Assert.That(async () => await service.StoreProject(new ProjectFile("Project.ihc", "<utcs_project/>")),
                Throws.TypeOf<HttpRequestException>().With.Message.Contains("mid-upload"),
                "the change-mode cleanup failure must never replace the root-cause upload exception");
        }

        /// <summary>
        /// The vendor leaves project change mode after ANY failed send step, and the first state poll after
        /// entering the mode is already one: a poll that fails must not skip the exit.
        /// </summary>
        [Test]
        public void StoreProject_FirstStatePollThrows_StillExitsChangeMode_AndSurfacesThePollFailure()
        {
            var soap = HappySoap();
            A.CallTo(() => soap.waitForControllerStateChangeAsync(A<inputMessageName19>._))
                .ThrowsAsync(new HttpRequestException("first poll dropped")).Once()
                .Then.Returns(StateReply("text.ctrl.state.ready"));

            AssertStoreFailsAndLeavesChangeModeUnstored(soap,
                Throws.TypeOf<HttpRequestException>().With.Message.Contains("first poll dropped"));
        }

        /// <summary>
        /// Polls that never answer <see cref="ControllerState.Initialize"/> -- replies naming no state, read as
        /// <see cref="ControllerState.Unknown"/>, or another state -- fail the store at the deadline: it does not
        /// proceed, the mode it entered is still left, and a cleanup that also fails never replaces that failure.
        /// </summary>
        [TestCase(null, false, TestName = "StoreProject_StatePollsAnswerUnknown_StillExitsChangeMode_AndSurfacesTheStateFailure")]
        [TestCase("text.ctrl.state.ready", true, TestName = "StoreProject_StatePollsAnswerAnotherState_StillExitsChangeMode_AndKeepsThePrimaryFailure")]
        public void StoreProject_StatePollsThatNeverAnswerInitialize_FailTheStoreAndStillExitChangeMode(
            string? answeredState, bool exitFailsToo)
        {
            var soap = HappySoap();
            var clock = new FakeTimeProvider();
            A.CallTo(() => soap.waitForControllerStateChangeAsync(A<inputMessageName19>._))
                .ReturnsLazily((inputMessageName19 request) => HeldReply(clock, request, answeredState));
            if (exitFailsToo)
                A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._))
                    .ThrowsAsync(new HttpRequestException("exit failed too"));

            AssertStoreFailsAndLeavesChangeModeUnstored(soap,
                Throws.InvalidOperationException.With.Message.Contains("did not enter init state"), clock);
        }

        /// <summary>
        /// The long poll answers as soon as the controller's state differs from the one sent, so a wait whose state
        /// has already been reached needs no poll at all: here every state read already names the state the store
        /// waits for, and nothing is held.
        /// </summary>
        [Test]
        public async Task StoreProject_AWaitAlreadySatisfied_IsSkipped()
        {
            var soap = HappySoap();
            string current = "text.ctrl.state.ready";
            A.CallTo(() => soap.getStateAsync(A<inputMessageName1>._))
                .ReturnsLazily(() => Task.FromResult(new outputMessageName1(new WSControllerState { state = current })));
            A.CallTo(() => soap.enterProjectChangeModeAsync(A<inputMessageName12>._))
                .ReturnsLazily(() => { current = "text.ctrl.state.initialize"; return Task.FromResult(new outputMessageName12(true)); });
            A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._))
                .ReturnsLazily(() => { current = "text.ctrl.state.ready"; return Task.FromResult(new outputMessageName13(true)); });

            bool stored = await NewService(soap).StoreProject(new ProjectFile("Project.ihc", "<utcs_project/>"));

            Assert.That(stored, Is.True);
            A.CallTo(() => soap.waitForControllerStateChangeAsync(A<inputMessageName19>._)).MustNotHaveHappened();
        }

        /// <summary>
        /// Each poll carries the state the SDK last OBSERVED, never the one it expects: sent the expected state, the
        /// controller would hold the poll for as long as that state lasts. A reply naming no state is observed as
        /// <see cref="ControllerState.Unknown"/> and goes out again as nil, as IHC Administrator sends it.
        /// </summary>
        [Test]
        public async Task StoreProject_AStaleState_IsRePolledWithTheLastObservedState()
        {
            var soap = HappySoap();
            var sent = new List<string?>();
            A.CallTo(() => soap.waitForControllerStateChangeAsync(A<inputMessageName19>._))
                .Invokes((inputMessageName19 request) => sent.Add(request.waitForControllerStateChange1?.state))
                .ReturnsNextFromSequence(
                    StateReply(null),
                    StateReply("text.ctrl.state.initialize"),
                    StateReply("text.ctrl.state.initialize"));

            bool stored = await NewService(soap).StoreProject(new ProjectFile("Project.ihc", "<utcs_project/>"));

            Assert.That(stored, Is.True);
            Assert.That(sent, Is.EqualTo(new string?[] { "text.ctrl.state.ready", null, "text.ctrl.state.ready" }),
                "after the enter: ready read, then the nil observed; after the store: ready read again");
        }

        /// <summary>
        /// A state that is never reached ends at the deadline, no shorter than 10 s per wait, and each poll holds
        /// only what is left of it. The cleanup's own wait for <see cref="ControllerState.Ready"/> is satisfied by its
        /// state read, so the whole deadline is the first wait's.
        /// </summary>
        [Test]
        public void StoreProject_TheDeadline_EndsAWaitThatNeverReachesItsState()
        {
            var soap = HappySoap();
            var clock = new FakeTimeProvider();
            DateTimeOffset start = clock.GetUtcNow();
            var holds = new List<int?>();
            A.CallTo(() => soap.waitForControllerStateChangeAsync(A<inputMessageName19>._))
                .ReturnsLazily((inputMessageName19 request) =>
                {
                    holds.Add(request.waitForControllerStateChange2);
                    clock.Advance(TimeSpan.FromSeconds(3));
                    return StateReply("text.ctrl.state.ready");
                });

            AssertStoreFailsAndLeavesChangeModeUnstored(soap,
                Throws.InvalidOperationException.With.Message.Contains("did not enter init state"), clock);
            Assert.That(holds, Is.EqualTo(new int?[] { 10, 7, 4, 1 }), "each poll holds at most what is left of 10 s");
            Assert.That(clock.GetUtcNow() - start, Is.EqualTo(TimeSpan.FromSeconds(12)));
        }

        /// <summary>A state-wait reply naming <paramref name="wireState"/>, or no state at all when it is null.</summary>
        private static Task<outputMessageName19> StateReply(string? wireState) =>
            Task.FromResult(new outputMessageName19(wireState is null ? null : new WSControllerState { state = wireState }));

        /// <summary>A poll the controller holds for the full time it was asked to, then answers unchanged.</summary>
        private static Task<outputMessageName19> HeldReply(FakeTimeProvider clock, inputMessageName19 request, string? wireState)
        {
            clock.Advance(TimeSpan.FromSeconds(request.waitForControllerStateChange2 ?? 0));
            return StateReply(wireState);
        }

        private static void AssertStoreFailsAndLeavesChangeModeUnstored(Ihc.Soap.Controller.ControllerService soap,
            NUnit.Framework.Constraints.IResolveConstraint expectedFailure, TimeProvider? clock = null)
        {
            var service = NewService(soap, clock);

            Assert.That(async () => await service.StoreProject(new ProjectFile("Project.ihc", "<utcs_project/>")), expectedFailure);
            A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => soap.storeIHCProjectAsync(A<inputMessageName4>._)).MustNotHaveHappened();
        }

        [Test]
        public void StoreProject_NonLatin1Data_FailsBeforeEnteringChangeMode()
        {
            var soap = HappySoap();
            var service = NewService(soap);
            var project = new ProjectFile("Project.ihc", "<utcs_project note=\"pris 20 €\"/>");

            Assert.That(async () => await service.StoreProject(project),
                Throws.InvalidOperationException.With.Message.Contains("U+20AC"),
                "an out-of-repertoire character must fail loudly, never be stored as '?'");
            A.CallTo(() => soap.enterProjectChangeModeAsync(A<inputMessageName12>._)).MustNotHaveHappened();
        }

        /// <summary>
        /// The acknowledgement element is <c>Nullable&lt;bool&gt;</c> on the wire, and an absent one used to
        /// fold to the same clean <c>false</c> as an explicit refusal - after the project bytes had already
        /// been sent. "The controller declined the store" and "the controller's answer did not say" are
        /// different facts, and only the first is one a caller can act on.
        /// </summary>
        [Test]
        public void StoreProject_AbsentAcknowledgement_IsRefusedRatherThanReportedAsADeclinedStore()
        {
            var soap = HappySoap();
            A.CallTo(() => soap.storeIHCProjectAsync(A<inputMessageName4>._))
                .Returns(Task.FromResult(new outputMessageName4(null)));
            var service = NewService(soap);

            Assert.That(async () => await service.StoreProject(new ProjectFile("Project.ihc", "<utcs_project/>")),
                Throws.InvalidOperationException.With.Message.Contains("acknowledge"));
        }

        /// <summary>An explicit <c>false</c> still means what it always meant: the controller declined.</summary>
        [Test]
        public async Task StoreProject_ExplicitlyDeclined_StillReturnsFalse()
        {
            var soap = HappySoap();
            A.CallTo(() => soap.storeIHCProjectAsync(A<inputMessageName4>._))
                .Returns(Task.FromResult(new outputMessageName4(false)));
            var service = NewService(soap);

            Assert.That(await service.StoreProject(new ProjectFile("Project.ihc", "<utcs_project/>")), Is.False);
        }

        // ---------------------------------------------------------------- the upload in segments

        /// <summary>A project text whose gzip spans a few hundred bytes, so small segment sizes cut it into many.</summary>
        private static readonly ProjectFile Segmented = new("house.vis",
            "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<utcs_project>\r\n" +
            string.Concat(Enumerable.Range(0, 40).Select(i => $"  <room name=\"Rum {i} æøå\" id=\"_0x{i * 7919:x}\"/>\r\n")) +
            "</utcs_project>\r\n");

        /// <summary>The slices an upload sent, and the segment count each carried.</summary>
        private static List<(byte[] Data, int? Index, int? Count, string Filename)> RecordSlices(Ihc.Soap.Controller.ControllerService soap)
        {
            var sent = new List<(byte[] Data, int? Index, int? Count, string Filename)>();
            A.CallTo(() => soap.storeIHCProjectSegmentAsync(A<inputMessageName16>._))
                .Invokes((inputMessageName16 request) => sent.Add((request.storeIHCProjectSegment1.data, request.storeIHCProjectSegment2,
                    request.storeIHCProjectSegment3, request.storeIHCProjectSegment1.filename)))
                .Returns(Task.FromResult(new outputMessageName16(true)));
            return sent;
        }

        private static void AnswerSegmentationSize(Ihc.Soap.Controller.ControllerService soap, int size) =>
            A.CallTo(() => soap.getIHCProjectSegmentationSizeAsync(A<inputMessageName17>._))
                .Returns(Task.FromResult(new outputMessageName17(size)));

        /// <summary>
        /// The project is gzipped once and its stream cut at the controller's segmentation size: every slice full but
        /// the last, none empty, each carrying the slice count, only the first starting with the gzip magic, and all
        /// of them joined the whole stream. The stream's length is measured through the SDK as the one slice of a size
        /// it fits in; then a size equal to it, a size one byte smaller and every size dividing it exactly are tried,
        /// since an exact multiple is where IHC Visual's own arithmetic sends an empty last slice.
        /// </summary>
        [Test]
        public async Task StoreProjectInSegments_CutsOneGzipStream_IntoFullSlices_WithNoEmptyTail()
        {
            byte[] whole = (await UploadWithSegmentationSize(1_000_000)).Single().Data;
            int[] sizes = [whole.Length, whole.Length - 1, .. Enumerable.Range(2, whole.Length - 2)
                .Where(divisor => whole.Length % divisor == 0).Select(divisor => whole.Length / divisor)];

            foreach (int size in sizes)
            {
                var sent = await UploadWithSegmentationSize(size);

                int count = (whole.Length + size - 1) / size;
                Assert.That(sent.Select(slice => slice.Index), Is.EqualTo(Enumerable.Range(0, count).Select(i => (int?)i)), $"size {size}");
                Assert.That(sent.Select(slice => slice.Count), Is.All.EqualTo(count), $"size {size}");
                Assert.That(sent.SkipLast(1).Select(slice => slice.Data.Length), Is.All.EqualTo(size), $"size {size}");
                Assert.That(sent[^1].Data.Length, Is.InRange(1, size), $"size {size}");
                Assert.That(sent.SelectMany(slice => slice.Data), Is.EqualTo(whole), $"size {size}");
                Assert.That(sent.Select(slice => ProjectGzipText.StartsWithGzipMagic(slice.Data)),
                    Is.EqualTo(sent.Select((_, index) => index == 0)), $"size {size}");
            }
            Assert.That(whole.Length, Is.GreaterThan(100), "the gzip spans enough bytes to cut");

            Assert.That(ProjectGzipText.Decompress([whole]), Is.EqualTo(Segmented.Data), "the one stream is the project's ISO-8859-1 text");
        }

        private static async Task<List<(byte[] Data, int? Index, int? Count, string Filename)>> UploadWithSegmentationSize(int size)
        {
            var soap = HappySoap();
            AnswerSegmentationSize(soap, size);
            var sent = RecordSlices(soap);

            Assert.That(await NewService(soap).StoreProjectInSegments(Segmented), Is.True, $"size {size}");
            Assert.That(sent.Select(slice => slice.Filename), Is.All.EqualTo(Segmented.Filename));
            return sent;
        }

        /// <summary>The upload runs inside project change mode: entered before the segmentation size is read and left
        /// after the last slice, with nothing sent whole.</summary>
        [Test]
        public async Task StoreProjectInSegments_SendsTheSlicesInsideProjectChangeMode()
        {
            var soap = HappySoap();
            AnswerSegmentationSize(soap, 64);

            Assert.That(await NewService(soap).StoreProjectInSegments(Segmented), Is.True);

            A.CallTo(() => soap.enterProjectChangeModeAsync(A<inputMessageName12>._)).MustHaveHappenedOnceExactly()
                .Then(A.CallTo(() => soap.getIHCProjectSegmentationSizeAsync(A<inputMessageName17>._)).MustHaveHappenedOnceExactly())
                .Then(A.CallTo(() => soap.storeIHCProjectSegmentAsync(A<inputMessageName16>._)).MustHaveHappened())
                .Then(A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._)).MustHaveHappenedOnceExactly());
            A.CallTo(() => soap.storeIHCProjectAsync(A<inputMessageName4>._)).MustNotHaveHappened();
        }

        /// <summary>A slice that fails ends the upload: nothing after it is sent, the mode it entered is still left, and
        /// the slice's own failure is the one raised.</summary>
        [Test]
        public void StoreProjectInSegments_AFailedSlice_StopsTheUpload_ExitsChangeMode_AndSurfacesItsFailure()
        {
            var soap = HappySoap();
            AnswerSegmentationSize(soap, 64);
            var sent = RecordSlices(soap);
            A.CallTo(() => soap.storeIHCProjectSegmentAsync(A<inputMessageName16>.That.Matches(request => request.storeIHCProjectSegment2 == 1)))
                .ThrowsAsync(new HttpRequestException("slice 1 dropped"));

            Assert.That(async () => await NewService(soap).StoreProjectInSegments(Segmented),
                Throws.TypeOf<HttpRequestException>().With.Message.Contains("slice 1 dropped"));
            Assert.That(sent.Select(slice => slice.Index), Is.EqualTo(new int?[] { 0 }), "nothing after the failed slice");
            A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._)).MustHaveHappenedOnceExactly();
        }

        /// <summary>A declined slice ends the upload as IHC Visual ends it, at once and with nothing more sent, and
        /// reads as a declined store; the mode is still left.</summary>
        [Test]
        public async Task StoreProjectInSegments_ADeclinedSlice_StopsTheUpload_AndReturnsFalse()
        {
            var soap = HappySoap();
            AnswerSegmentationSize(soap, 64);
            A.CallTo(() => soap.storeIHCProjectSegmentAsync(A<inputMessageName16>.That.Matches(request => request.storeIHCProjectSegment2 == 1)))
                .Returns(Task.FromResult(new outputMessageName16(false)));

            Assert.That(await NewService(soap).StoreProjectInSegments(Segmented), Is.False);
            A.CallTo(() => soap.storeIHCProjectSegmentAsync(A<inputMessageName16>._)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => soap.storeIHCProjectSegmentAsync(A<inputMessageName16>.That.Matches(request => request.storeIHCProjectSegment2 > 1)))
                .MustNotHaveHappened();
            A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._)).MustHaveHappenedOnceExactly();
        }

        /// <summary>A slice the controller does not acknowledge leaves the upload's outcome unknown, which a declined
        /// store is not: it throws, after leaving the mode, rather than read as false.</summary>
        [Test]
        public void StoreProjectInSegments_AnUnacknowledgedSlice_Throws_AndExitsChangeMode()
        {
            var soap = HappySoap();
            AnswerSegmentationSize(soap, 64);
            A.CallTo(() => soap.storeIHCProjectSegmentAsync(A<inputMessageName16>._))
                .Returns(Task.FromResult(new outputMessageName16(null)));

            Assert.That(async () => await NewService(soap).StoreProjectInSegments(Segmented),
                Throws.InvalidOperationException.With.Message.Contains("did not acknowledge segment 0"));
            A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._)).MustHaveHappenedOnceExactly();
        }

        /// <summary>A segmentation size that cuts nothing is refused inside the mode, which is then left; IHC Visual
        /// would divide by it.</summary>
        [TestCase(0)]
        [TestCase(-1)]
        public void StoreProjectInSegments_ANonPositiveSegmentationSize_IsRefused_AndExitsChangeMode(int size)
        {
            var soap = HappySoap();
            AnswerSegmentationSize(soap, size);

            Assert.That(async () => await NewService(soap).StoreProjectInSegments(Segmented),
                Throws.InvalidOperationException.With.Message.Contains("segmentation size"));
            A.CallTo(() => soap.storeIHCProjectSegmentAsync(A<inputMessageName16>._)).MustNotHaveHappened();
            A.CallTo(() => soap.exitProjectChangeModeAsync(A<inputMessageName13>._)).MustHaveHappenedOnceExactly();
        }

        /// <summary>The checks StoreProject makes before entering the mode apply alike: text outside ISO-8859-1 is
        /// refused before anything changes on the controller.</summary>
        [Test]
        public void StoreProjectInSegments_NonLatin1Data_FailsBeforeEnteringChangeMode()
        {
            var soap = HappySoap();

            Assert.That(async () => await NewService(soap).StoreProjectInSegments(new ProjectFile("Project.ihc", "<utcs_project note=\"pris 20 €\"/>")),
                Throws.InvalidOperationException.With.Message.Contains("U+20AC"));
            A.CallTo(() => soap.enterProjectChangeModeAsync(A<inputMessageName12>._)).MustNotHaveHappened();
        }
    }
}
