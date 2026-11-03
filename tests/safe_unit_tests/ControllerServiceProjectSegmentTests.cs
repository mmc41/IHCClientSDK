using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using FakeItEasy;
using Ihc.Soap.Controller;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <see cref="ControllerService.GetIHCProjectSegment"/> and <see cref="ControllerService.StoreIHCProjectSegment"/>
    /// against a faked SOAP layer, with the project carried the way a controller carries it: one gzip stream of the
    /// project, cut into slices.
    /// </summary>
    /// <remarks>
    /// The slices here are synthetic, but cut as captured: a 3.3.44 controller served its gzip of the stored project
    /// in 7,500-byte slices, and only the first slice of a download began with the gzip magic 1F 8B. A segment is
    /// therefore no gzip stream of its own, and only the joined slices decompress.
    /// </remarks>
    [TestFixture]
    public class ControllerServiceProjectSegmentTests
    {
        private const int SliceSize = 7_500;

        /// <summary>Text that compresses poorly, so its gzip spans several slices; a fixed sequence, so every run is
        /// the same.</summary>
        private static readonly string ProjectText = BuildProjectText();

        private static readonly byte[] Gzip = ProjectGzipText.Compress(ProjectText);

        private static readonly byte[][] Slices = [.. Gzip.Chunk(SliceSize)];

        [Test]
        public void TheSyntheticDownload_IsCutAsCaptured()
        {
            Assert.That(Slices, Has.Length.GreaterThanOrEqualTo(3));
            Assert.That(Slices.Select(ProjectGzipText.StartsWithGzipMagic), Is.EqualTo(Slices.Select((_, index) => index == 0)),
                "only the first slice carries the gzip magic");
        }

        /// <summary>Each segment is handed to the caller as the bytes the controller sent, not decoded on its own.</summary>
        [Test]
        public async Task EverySegment_IsTheSliceTheControllerSent_Unchanged()
        {
            var service = ServiceServingSlices();

            for (int index = 0; index < Slices.Length; index++)
            {
                ProjectSegment segment = await service.GetIHCProjectSegment(index, 1, 0);

                Assert.That(segment.Data, Is.EqualTo(Slices[index]), $"segment {index}");
            }
        }

        /// <summary>A segment after the first starts mid-stream, with no gzip header: it must read, not fail.</summary>
        [Test]
        public async Task TheSegmentsAfterTheFirst_ReadWithoutFailing_AndJoinToTheProject()
        {
            var service = ServiceServingSlices();
            var served = new List<byte[]>();

            for (int index = 0; index < Slices.Length; index++)
            {
                served.Add((await service.GetIHCProjectSegment(index, 1, 0)).Data);
            }

            Assert.That(ProjectGzipText.Decompress(served), Is.EqualTo(ProjectText));
        }

        /// <summary>The identity pair goes out in the wire's order: the index, then the stored project's id1 and id2.</summary>
        [Test]
        public async Task TheIndexAndTheProjectIdentity_GoOutInWireOrder()
        {
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            inputMessageName15? sent = null;
            A.CallTo(() => soap.getIHCProjectSegmentAsync(A<inputMessageName15>._))
                .Invokes((inputMessageName15 request) => sent = request)
                .Returns(Task.FromResult(new outputMessageName15(new WSFile { filename = "unknown.bin", data = Slices[1] })));

            await new ControllerService(FakeSession.Over(), soap).GetIHCProjectSegment(1, 16842752, 16842753);

            Assert.That((sent?.getIHCProjectSegment1, sent?.getIHCProjectSegment2, sent?.getIHCProjectSegment3),
                Is.EqualTo(((int?)1, (int?)16842752, (int?)16842753)));
        }

        /// <summary>A reply with no file reads as a segment with no data, as the OpenAPI segment download reads it.</summary>
        [Test]
        public async Task AReplyWithNoFile_IsASegmentWithNoData()
        {
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            A.CallTo(() => soap.getIHCProjectSegmentAsync(A<inputMessageName15>._))
                .Returns(Task.FromResult(new outputMessageName15(null)));

            ProjectSegment segment = await new ControllerService(FakeSession.Over(), soap).GetIHCProjectSegment(0, 1, 0);

            Assert.That(segment.Data, Is.Empty);
        }

        // ---------------------------------------------------------------- the upload

        /// <summary>
        /// An upload sends the same kind of slices back: each one's bytes unchanged, its index, and the segment count
        /// in every call. IHC Visual was captured sending 27 in all 27 calls of a 27-slice upload, only the first slice
        /// starting with the gzip magic; the slices here are synthetic, cut into 27.
        /// </summary>
        [Test]
        public async Task AnUpload_SendsEverySliceUnchanged_WithItsIndex_AndTheSegmentCountInEveryCall()
        {
            byte[][] slices = [.. Gzip.Chunk((Gzip.Length + 26) / 27)];
            var (service, sent) = ServiceStoringSlices();

            for (int index = 0; index < slices.Length; index++)
            {
                Assert.That(await service.StoreIHCProjectSegment(new ProjectSegment { Data = slices[index] }, index, slices.Length), Is.True);
            }

            Assert.That(slices, Has.Length.EqualTo(27));
            Assert.That(sent.Select(request => request.storeIHCProjectSegment1.data), Is.EqualTo(slices));
            Assert.That(sent.Select(request => request.storeIHCProjectSegment2), Is.EqualTo(Enumerable.Range(0, 27).Select(i => (int?)i)));
            Assert.That(sent.Select(request => request.storeIHCProjectSegment3), Is.All.EqualTo(27));
            Assert.That(sent.Select(request => ProjectGzipText.StartsWithGzipMagic(request.storeIHCProjectSegment1.data)),
                Is.EqualTo(slices.Select((_, index) => index == 0)));
        }

        /// <summary>On the wire a slice is the base64 of its bytes and nothing else: no gzip of its own around it.</summary>
        [Test]
        public async Task AnUploadedSlice_IsTheBase64OfItsBytesOnTheWire()
        {
            var (service, sent) = ServiceStoringSlices();

            await service.StoreIHCProjectSegment(new ProjectSegment { Data = Slices[1], Filename = "house.vis" }, 1, Slices.Length);

            XElement file = XDocument.Parse(SoapRequestText.Of(sent.Single())).Descendants(Utcs + "storeIHCProjectSegment1").Single();
            Assert.That((string?)file.Element(Utcs + "data"), Is.EqualTo(Convert.ToBase64String(Slices[1])));
            Assert.That((string?)file.Element(Utcs + "filename"), Is.EqualTo("house.vis"));
        }

        /// <summary>
        /// The wire requires a file name, which a segment need not carry. IHC Visual sends its working file's path,
        /// <c>...\temp.vis</c>, in every segment; without a name of its own a segment goes out under that file's name,
        /// with no local path.
        /// </summary>
        [Test]
        public async Task ASliceWithNoName_GoesOutUnderTheVendorsWorkingFileName()
        {
            var (service, sent) = ServiceStoringSlices();

            await service.StoreIHCProjectSegment(new ProjectSegment { Data = Slices[0] }, 0, Slices.Length);

            Assert.That(sent.Single().storeIHCProjectSegment1.filename, Is.EqualTo("temp.vis"));
        }

        // ---------------------------------------------------------------- the whole-project download

        /// <summary>
        /// The whole project, through its segments: every slice fetched with the stored project's identity, joined in
        /// index order and decompressed once, as ServiceView reads a download, into the ISO-8859-1 text
        /// <see cref="IControllerService.GetProject"/> returns. Its name is the one the controller gives the segments.
        /// </summary>
        [Test]
        public async Task GetProjectInSegments_JoinsTheSlicesAndDecompressesThemOnce()
        {
            var (service, soap) = ServiceHoldingAProject(Slices.Length);

            ProjectFile? project = await service.GetProjectInSegments();

            Assert.That(project?.Data, Is.EqualTo(ProjectText));
            Assert.That(project?.Filename, Is.EqualTo("unknown.bin"));
            A.CallTo(() => soap.getIHCProjectSegmentAsync(A<inputMessageName15>.That.Matches(request =>
                    request.getIHCProjectSegment2 == ProjectId1 && request.getIHCProjectSegment3 == ProjectId2)))
                .MustHaveHappened(Slices.Length, Times.Exactly);
        }

        /// <summary>A controller that reports no segment has nothing to download, as one reporting no project.</summary>
        [Test]
        public async Task GetProjectInSegments_WithNoSegments_IsNull_AndFetchesNone()
        {
            var (service, soap) = ServiceHoldingAProject(0);

            Assert.That(await service.GetProjectInSegments(), Is.Null);
            A.CallTo(() => soap.getIHCProjectSegmentAsync(A<inputMessageName15>._)).MustNotHaveHappened();
        }

        [Test]
        public async Task GetProjectInSegments_WithNoStoredProject_IsNull_AndFetchesNone()
        {
            var (service, soap) = ServiceHoldingAProject(Slices.Length);
            A.CallTo(() => soap.getProjectInfoAsync(A<inputMessageName8>._)).Returns(Task.FromResult(new outputMessageName8(null)));

            Assert.That(await service.GetProjectInSegments(), Is.Null);
            A.CallTo(() => soap.getIHCProjectSegmentAsync(A<inputMessageName15>._)).MustNotHaveHappened();
        }

        /// <summary>A slice the controller does not send cannot be skipped: the joined stream would decompress to
        /// garbage or not at all, so the download fails and names the slice.</summary>
        [Test]
        public void GetProjectInSegments_WithAMissingSlice_FailsAndNamesIt()
        {
            var (service, soap) = ServiceHoldingAProject(Slices.Length);
            A.CallTo(() => soap.getIHCProjectSegmentAsync(A<inputMessageName15>.That.Matches(request => request.getIHCProjectSegment1 == 1)))
                .Returns(Task.FromResult(new outputMessageName15(null)));

            Assert.That(async () => await service.GetProjectInSegments(),
                Throws.InvalidOperationException.With.Message.Contains($"segment 1 of {Slices.Length}"));
        }

        /// <summary>Slices that join into no gzip stream fail as such, not as a bare decoder error out of the SDK.</summary>
        [Test]
        public void GetProjectInSegments_WhenTheJoinedSlicesAreNoGzipStream_FailsSayingSo()
        {
            var (service, soap) = ServiceHoldingAProject(2);
            A.CallTo(() => soap.getIHCProjectSegmentAsync(A<inputMessageName15>._))
                .Returns(Task.FromResult(new outputMessageName15(new WSFile { filename = "unknown.bin", data = [1, 2, 3] })));

            Assert.That(async () => await service.GetProjectInSegments(),
                Throws.InvalidOperationException.With.Message.Contains("do not decompress as one gzip stream")
                    .And.InnerException.TypeOf<InvalidDataException>());
        }

        /// <summary>
        /// A stream cut short still decodes, to a prefix of the project, because the framework's gzip stops where its
        /// input does. So a download whose joined slices lost their tail fails, whether the cut fell in the compressed
        /// data or only in the gzip trailer after it.
        /// </summary>
        [TestCase(SliceSize + 1, TestName = "GetProjectInSegments_WhenTheStreamIsCutShort_FailsSayingSo(in the compressed data)")]
        [TestCase(GzipTrailerLength, TestName = "GetProjectInSegments_WhenTheStreamIsCutShort_FailsSayingSo(only its trailer)")]
        public void GetProjectInSegments_WhenTheStreamIsCutShort_FailsSayingSo(int dropped)
        {
            byte[][] shortened = [.. Gzip[..^dropped].Chunk(SliceSize)];
            var (service, _) = ServiceHoldingAProject(shortened.Length, shortened);

            Assert.That(async () => await service.GetProjectInSegments(),
                Throws.InvalidOperationException.With.Message.Contains("do not decompress as one gzip stream")
                    .And.InnerException.TypeOf<InvalidDataException>());
        }

        /// <summary>The single-call download decodes the same way, so a stream cut short fails there too.</summary>
        [Test]
        public void GetProject_WhenTheStreamIsCutShort_Fails()
        {
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            A.CallTo(() => soap.getIHCProjectAsync(A<inputMessageName3>._)).Returns(Task.FromResult(new outputMessageName3(
                new WSFile { filename = "house.vis", data = Gzip[..(Gzip.Length / 2)] })));

            Assert.That(async () => await new ControllerService(FakeSession.Over(), soap).GetProject(), Throws.TypeOf<InvalidDataException>());
        }

        /// <summary>RFC 1952: a gzip member ends in CRC-32 and the decoded length, four bytes each.</summary>
        private const int GzipTrailerLength = 8;

        private const int ProjectId1 = 16842752;
        private const int ProjectId2 = 16842753;

        /// <summary>A controller reporting <paramref name="segmentCount"/> segments and serving <paramref name="served"/>,
        /// by default the slices of <see cref="ProjectText"/>.</summary>
        private static (ControllerService Service, Ihc.Soap.Controller.ControllerService Soap) ServiceHoldingAProject(int segmentCount,
            byte[][]? served = null)
        {
            served ??= Slices;
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            A.CallTo(() => soap.getProjectInfoAsync(A<inputMessageName8>._)).Returns(Task.FromResult(new outputMessageName8(
                new WSProjectInfo { projectMajorRevision = ProjectId1, projectMinorRevision = ProjectId2 })));
            A.CallTo(() => soap.getIHCProjectNumberOfSegmentsAsync(A<inputMessageName18>._))
                .Returns(Task.FromResult(new outputMessageName18(segmentCount)));
            A.CallTo(() => soap.getIHCProjectSegmentAsync(A<inputMessageName15>._))
                .ReturnsLazily((inputMessageName15 request) => Task.FromResult(new outputMessageName15(
                    new WSFile { filename = "unknown.bin", data = served[request.getIHCProjectSegment1!.Value] })));
            return (new ControllerService(FakeSession.Over(), soap), soap);
        }

        private static readonly XNamespace Utcs = "utcs";

        private static (ControllerService Service, List<inputMessageName16> Sent) ServiceStoringSlices()
        {
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            var sent = new List<inputMessageName16>();
            A.CallTo(() => soap.storeIHCProjectSegmentAsync(A<inputMessageName16>._))
                .Invokes((inputMessageName16 request) => sent.Add(request))
                .Returns(Task.FromResult(new outputMessageName16(true)));
            return (new ControllerService(FakeSession.Over(), soap), sent);
        }

        private static ControllerService ServiceServingSlices() => ServiceHoldingAProject(Slices.Length).Service;

        private static string BuildProjectText()
        {
            // A linear congruential sequence: letters with no pattern gzip can shrink much, and no Random to seed.
            uint state = 20251018;
            var text = new StringBuilder("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<utcs_project>\r\n");
            while (text.Length < 40_000)
            {
                text.Append("  <name>");
                for (int i = 0; i < 40; i++)
                {
                    state = state * 1103515245 + 12345;
                    text.Append((char)('a' + state / 65536 % 26));
                }
                text.Append(" æøå</name>\r\n");
            }
            return text.Append("</utcs_project>\r\n").ToString();
        }
    }
}
