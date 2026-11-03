using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Soap.Controller;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The four segment operations serve a segmented view of the same stored project the one-call operations
    /// serve, read and written through the real <see cref="ControllerService"/>. A download is served as a controller
    /// serves it: slices of one gzip stream of the project, <see cref="StubController.SegmentationSize"/> bytes each,
    /// which decompress only when joined.
    /// </summary>
    [TestFixture]
    public class StubProjectSegmentTests
    {
        private const int Size = 16;

        /// <summary>Several segments' worth, ending part-way into the last, with CRLF and ISO-8859-1 letters.</summary>
        private static readonly ProjectFile Project = new("segmented.vis",
            "<utcs_project>\r\n  <name>Køkken æøå</name>\n  <room>Stue</room>\r\n</utcs_project>\r\n");

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task StoreThenGet_RoundTripsByteForByte_InEveryCombination(bool storeSegmented, bool getSegmented)
        {
            using var sdk = Start();

            if (storeSegmented)
            {
                await StoreSegmented(sdk, Project);
            }
            else
            {
                Assert.That(await sdk.Controller.StoreProject(Project), Is.True);
            }
            string fetched = getSegmented ? await GetSegmented(sdk) : (await sdk.Controller.GetProject())!.Data;

            Assert.That(ProjectFile.Encoding.GetBytes(fetched), Is.EqualTo(ProjectFile.Encoding.GetBytes(Project.Data)));
        }

        /// <summary>
        /// The count is the gzip stream's size over the segment size, rounded up, and every slice but the last is
        /// full. The stream's size is measured through the SDK, as the one segment of a size it fits in; a segment
        /// size equal to it is the exact multiple that must not leave an empty tail.
        /// </summary>
        [Test]
        public async Task TheSlicesCutTheGzipStream_FullUpToTheLast_WithNoEmptyTail()
        {
            var stub = new ControllerStub();
            stub.Controller.Project = Project;
            stub.Controller.SegmentationSize = 1_000_000;
            using var sdk = StubbedSdk.Start(stub);
            byte[] whole = (await sdk.Controller.GetIHCProjectSegment(0, 1, 0)).Data;

            foreach (int size in new[] { whole.Length, whole.Length - 1, Size })
            {
                stub.Controller.SegmentationSize = size;
                byte[][] slices = await DownloadSlices(sdk);

                Assert.That(slices, Has.Length.EqualTo((whole.Length + size - 1) / size), $"size {size}");
                Assert.That(slices[..^1].Select(slice => slice.Length), Is.All.EqualTo(size), $"size {size}");
                Assert.That(slices[^1].Length, Is.InRange(1, size), $"size {size}: the last slice is never empty");
                Assert.That(slices.SelectMany(slice => slice), Is.EqualTo(whole), $"size {size}");
            }
        }

        /// <summary>
        /// As a 3.3.44 controller serves a download: only the first slice starts with the gzip magic 1F 8B, so a
        /// later one is no gzip stream of its own, and every segment is named <c>unknown.bin</c>, whatever the
        /// project is called.
        /// </summary>
        [Test]
        public async Task OnlyTheFirstSegment_StartsWithTheGzipMagic_AndEveryOneIsNamedUnknownBin()
        {
            var stub = new ControllerStub();
            stub.Controller.Project = Project;
            stub.Controller.SegmentationSize = Size;
            using var sdk = StubbedSdk.Start(stub);
            var controllerWire = new ControllerAnswers(stub.Controller);

            byte[][] slices = await DownloadSlices(sdk);
            string[] names = new string[slices.Length];
            for (int index = 0; index < slices.Length; index++)
            {
                outputMessageName15 served = await controllerWire.getIHCProjectSegmentAsync(new inputMessageName15 { getIHCProjectSegment1 = index });
                names[index] = served.getIHCProjectSegment4.filename;
            }

            Assert.That(slices.Select(ProjectGzipText.StartsWithGzipMagic),
                Is.EqualTo(slices.Select((_, index) => index == 0)));
            Assert.That(names, Is.All.EqualTo("unknown.bin"));
        }

        /// <summary>
        /// Index 0 starts a new upload, and the project is replaced only once its last slice arrives: before then the
        /// slices received are part of one gzip stream that does not decompress, which a controller unpacks after the
        /// whole upload.
        /// </summary>
        [Test]
        public async Task IndexZero_RestartsTheUpload_AndOnlyTheLastSliceReplacesTheProject()
        {
            var replacement = new ProjectFile("replacement.vis", "<replacement/>--several-slices-of-gzip");
            ProjectSegment[] slices = Slices(replacement);
            using var sdk = Start();
            await StoreSegmented(sdk, Project);

            Assert.That(await sdk.Controller.StoreIHCProjectSegment(slices[0], 0, slices.Length), Is.True);
            Assert.That(sdk.Stub.Controller.Project, Is.EqualTo(Project), "one slice of several replaces nothing");
            for (int index = 1; index < slices.Length; index++)
            {
                Assert.That(await sdk.Controller.StoreIHCProjectSegment(slices[index], index, slices.Length), Is.True);
            }

            Assert.That(slices, Has.Length.GreaterThan(1));
            Assert.That(sdk.Stub.Controller.Project, Is.EqualTo(replacement));
        }

        /// <summary>Two slices are received, so the next index is 2: a repeat and a skip are both out of order.</summary>
        [TestCase(1)]
        [TestCase(3)]
        public async Task AnOutOfOrderIndex_AnswersFalse_AndChangesNothing(int index)
        {
            ProjectSegment[] slices = Slices(Project);
            using var sdk = Start();
            ProjectFile before = sdk.Stub.Controller.Project!;
            Assert.That(await sdk.Controller.StoreIHCProjectSegment(slices[0], 0, slices.Length), Is.True);
            Assert.That(await sdk.Controller.StoreIHCProjectSegment(slices[1], 1, slices.Length), Is.True);

            bool stored = await sdk.Controller.StoreIHCProjectSegment(slices[2], index, slices.Length);

            Assert.That(slices, Has.Length.GreaterThan(3));
            Assert.That(stored, Is.False);
            Assert.That(sdk.Stub.Controller.Project, Is.EqualTo(before));
        }

        /// <summary>The third part is the upload's segment count, the same in every call: a slice announcing another
        /// count belongs to no upload in progress.</summary>
        [Test]
        public async Task ASliceWithAnotherSegmentCount_AnswersFalse()
        {
            ProjectSegment[] slices = Slices(Project);
            using var sdk = Start();
            Assert.That(await sdk.Controller.StoreIHCProjectSegment(slices[0], 0, slices.Length), Is.True);

            Assert.That(await sdk.Controller.StoreIHCProjectSegment(slices[1], 1, slices.Length + 1), Is.False);
        }

        /// <summary>Slices that join into no gzip stream are refused at the last one, as a controller that cannot load
        /// the project refuses it, and the stored project stays.</summary>
        [Test]
        public async Task SlicesThatJoinIntoNoGzipStream_AnswerFalseToTheLast_AndLeaveTheProject()
        {
            using var sdk = Start();
            ProjectFile before = sdk.Stub.Controller.Project!;

            Assert.That(await sdk.Controller.StoreIHCProjectSegment(new ProjectSegment { Data = [1, 2, 3] }, 0, 2), Is.True);
            Assert.That(await sdk.Controller.StoreIHCProjectSegment(new ProjectSegment { Data = [4, 5, 6] }, 1, 2), Is.False);
            Assert.That(sdk.Stub.Controller.Project, Is.EqualTo(before));
        }

        /// <summary>A declined store is declined however the project is sent: a segment changes nothing either.</summary>
        [Test]
        public async Task WhileStoresAreDeclined_ASegmentAnswersFalse_AndLeavesTheProject()
        {
            ProjectSegment[] slices = Slices(Project);
            using var sdk = Start();
            ProjectFile before = sdk.Stub.Controller.Project!;
            sdk.Stub.Controller.StoreProjectAccepted = false;

            Assert.That(await sdk.Controller.StoreIHCProjectSegment(slices[0], 0, slices.Length), Is.False);
            Assert.That(sdk.Stub.Controller.Project, Is.EqualTo(before));
        }

        [Test]
        public async Task ASegmentedUpload_CannotContinueAfterTheProjectWasReplacedAnotherWay()
        {
            ProjectSegment[] slices = Slices(Project);
            using var sdk = Start();
            await sdk.Controller.StoreIHCProjectSegment(slices[0], 0, slices.Length);
            await sdk.Controller.StoreProject(Project);

            Assert.That(await sdk.Controller.StoreIHCProjectSegment(slices[1], 1, slices.Length), Is.False);
            Assert.That(sdk.Stub.Controller.Project!.Data, Is.EqualTo(Project.Data));
        }

        [Test]
        public async Task TheDefaultProject_ReadsTheSameSegmentedAsInOneCall()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.Controller.GetIHCProjectSegmentationSize(), Is.EqualTo(7500), "a 3.3.44 controller's size");
            Assert.That(await GetSegmented(sdk), Is.EqualTo((await sdk.Controller.GetProject())!.Data));
        }

        /// <summary>The whole-project download through segments reads the text the one-call download reads, under
        /// the name the stub gives every segment.</summary>
        [Test]
        public async Task GetProjectInSegments_ReadsTheDefaultProject_AsGetProjectDoes()
        {
            using var sdk = StubbedSdk.Start();

            ProjectFile? inSegments = await sdk.Controller.GetProjectInSegments();

            Assert.That(inSegments?.Data, Is.EqualTo((await sdk.Controller.GetProject())!.Data));
            Assert.That(inSegments?.Filename, Is.EqualTo("unknown.bin"));
        }

        /// <summary>Across many slices the project still comes back byte for byte, every segment asked for once, in
        /// order, after the project's identity and the count.</summary>
        [Test]
        public async Task GetProjectInSegments_JoinsManySlices_ByteForByte()
        {
            var stub = new ControllerStub();
            stub.Controller.Project = Project;
            stub.Controller.SegmentationSize = Size;
            using var sdk = StubbedSdk.Start(stub);
            int count = stub.Controller.NumberOfSegments();

            ProjectFile? fetched = await sdk.Controller.GetProjectInSegments();

            Assert.That(count, Is.GreaterThan(3));
            Assert.That(ProjectFile.Encoding.GetBytes(fetched!.Data), Is.EqualTo(ProjectFile.Encoding.GetBytes(Project.Data)));
            Assert.That(sdk.Stub.Journal.All.Select(call => call.SoapAction),
                Is.EqualTo(new[] { "getProjectInfo", "getIHCProjectNumberOfSegments" }.Concat(Enumerable.Repeat("getIHCProjectSegment", count))));
        }

        /// <summary>A project stored in many segments inside project change mode reads back byte for byte through the
        /// one-call download, with every slice sent in order between the enter and the exit.</summary>
        [Test]
        public async Task StoreProjectInSegments_ThenGetProject_RoundTripsByteForByte()
        {
            using var sdk = Start();

            bool stored = await sdk.Controller.StoreProjectInSegments(Project);
            ProjectFile? fetched = await sdk.Controller.GetProject();

            Assert.That(stored, Is.True);
            Assert.That(ProjectFile.Encoding.GetBytes(fetched!.Data), Is.EqualTo(ProjectFile.Encoding.GetBytes(Project.Data)));
            string[] actions = [.. sdk.Stub.Journal.All.Select(call => call.SoapAction)];
            int enter = Array.IndexOf(actions, "enterProjectChangeMode");
            int exit = Array.IndexOf(actions, "exitProjectChangeMode");
            Assert.That(actions[enter..exit].Count(action => action == "storeIHCProjectSegment"), Is.GreaterThan(3).And.EqualTo(
                actions.Count(action => action == "storeIHCProjectSegment")), "every slice inside the mode");
            Assert.That(sdk.Stub.Controller.State, Is.EqualTo(ControllerState.Ready), "the upload left change mode");
        }

        /// <summary>A controller that declines the slices declines the store: the upload stops, reads false, and the
        /// project stays.</summary>
        [Test]
        public async Task StoreProjectInSegments_WhileStoresAreDeclined_ReadsFalse_AndLeavesTheProject()
        {
            using var sdk = Start();
            ProjectFile before = sdk.Stub.Controller.Project!;
            sdk.Stub.Controller.StoreProjectAccepted = false;

            Assert.That(await sdk.Controller.StoreProjectInSegments(Project), Is.False);
            Assert.That(sdk.Stub.Controller.Project, Is.EqualTo(before));
            Assert.That(sdk.Stub.Journal.All.Count(call => call.SoapAction == "storeIHCProjectSegment"), Is.EqualTo(1), "nothing after the declined slice");
        }

        [Test]
        public async Task AControllerWithNoProject_HasNoSegments()
        {
            var stub = new ControllerStub();
            stub.Controller.Project = null;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Controller.GetIHCProjectNumberOfSegments(), Is.Zero);
            Assert.That((await sdk.Controller.GetIHCProjectSegment(0, 1, 0)).Data, Is.Empty);
            Assert.That(await sdk.Controller.GetProjectInSegments(), Is.Null);
            ProjectSegment[] slices = Slices(Project);
            Assert.That(await sdk.Controller.StoreIHCProjectSegment(slices[1], 1, slices.Length), Is.False);
        }

        [TestCase(-1)]
        [TestCase(1000)]
        [TestCase(int.MaxValue)]
        public async Task ASegmentOutsideTheProject_ReadsAsNone(int index)
        {
            using var sdk = Start();
            await StoreSegmented(sdk, Project);

            Assert.That((await sdk.Controller.GetIHCProjectSegment(index, 1, 0)).Data, Is.Empty);
        }

        [Test]
        public async Task TheSegmentationSize_RoundTripsThroughTheRealWrapper()
        {
            using var sdk = Start();

            Assert.That(await sdk.Controller.GetIHCProjectSegmentationSize(), Is.EqualTo(Size));
        }

        private static StubbedSdk Start()
        {
            var stub = new ControllerStub();
            stub.Controller.SegmentationSize = Size;
            return StubbedSdk.Start(stub);
        }

        /// <summary>
        /// The slices IHC Visual sends: the project's ISO-8859-1 text gzipped once, cut every <see cref="Size"/> bytes.
        /// </summary>
        private static ProjectSegment[] Slices(ProjectFile project) =>
            [.. ProjectGzipText.Compress(project.Data).Chunk(Size).Select(slice => new ProjectSegment { Data = slice, Filename = project.Filename })];

        /// <summary>An upload of every slice, in order, each carrying the segment count.</summary>
        private static async Task StoreSegmented(StubbedSdk sdk, ProjectFile project)
        {
            ProjectSegment[] slices = Slices(project);
            for (int index = 0; index < slices.Length; index++)
            {
                Assert.That(await sdk.Controller.StoreIHCProjectSegment(slices[index], index, slices.Length), Is.True, $"segment {index}");
            }
        }

        /// <summary>The stored project, downloaded segment by segment and decoded the way a vendor client decodes it.</summary>
        private static async Task<string> GetSegmented(StubbedSdk sdk) => ProjectGzipText.Decompress(await DownloadSlices(sdk));

        private static async Task<byte[][]> DownloadSlices(StubbedSdk sdk)
        {
            int count = await sdk.Controller.GetIHCProjectNumberOfSegments();
            var slices = new byte[count][];
            for (int index = 0; index < count; index++)
            {
                slices[index] = (await sdk.Controller.GetIHCProjectSegment(index, 1, 0)).Data;
            }
            return slices;
        }
    }
}
