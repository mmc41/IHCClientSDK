using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The four segment operations serve a segmented view of the same stored project the one-call operations
    /// serve, each segment gzip-encoded on its own, read and written through the real <see cref="ControllerService"/>.
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

        [Test]
        public async Task AProjectAnExactMultipleOfTheSegmentSize_SplitsWithNoEmptyTail()
        {
            var exact = new ProjectFile("exact.vis", new string('x', Size * 3));
            using var sdk = Start();

            await StoreSegmented(sdk, exact);

            Assert.That(await sdk.Controller.GetIHCProjectNumberOfSegments(), Is.EqualTo(3));
            Assert.That(await GetSegmented(sdk), Is.EqualTo(exact.Data));
        }

        [Test]
        public async Task IndexZero_RestartsTheUpload_AndReplacesTheProjectAtOnce()
        {
            // Two segments: a full one and a shorter tail.
            var replacement = new ProjectFile("replacement.vis", "<replacement/>--second-segment");
            using var sdk = Start();
            await StoreSegmented(sdk, Project);

            bool restarted = await sdk.Controller.StoreIHCProjectSegment(Chunks(replacement)[0], 0, 1);

            Assert.That(restarted, Is.True);
            Assert.That(sdk.Stub.Controller.Project!.Data, Is.EqualTo(Chunks(replacement)[0].Data));
            Assert.That(await sdk.Controller.StoreIHCProjectSegment(Chunks(replacement)[1], 1, 1), Is.True);
            Assert.That(sdk.Stub.Controller.Project!.Data, Is.EqualTo(replacement.Data));
        }

        /// <summary>Two segments are stored, so the next index is 2: a repeat and a skip are both out of order.</summary>
        [TestCase(1)]
        [TestCase(3)]
        public async Task AnOutOfOrderIndex_AnswersFalse_AndChangesNothing(int index)
        {
            using var sdk = Start();
            Assert.That(await sdk.Controller.StoreIHCProjectSegment(Chunks(Project)[0], 0, 1), Is.True);
            Assert.That(await sdk.Controller.StoreIHCProjectSegment(Chunks(Project)[1], 1, 1), Is.True);
            string before = sdk.Stub.Controller.Project!.Data;

            bool stored = await sdk.Controller.StoreIHCProjectSegment(Chunks(Project)[2], index, 1);

            Assert.That(stored, Is.False);
            Assert.That(sdk.Stub.Controller.Project!.Data, Is.EqualTo(before));
        }

        /// <summary>A declined store is declined however the project is sent: a segment changes nothing either.</summary>
        [Test]
        public async Task WhileStoresAreDeclined_ASegmentAnswersFalse_AndLeavesTheProject()
        {
            using var sdk = Start();
            ProjectFile before = sdk.Stub.Controller.Project!;
            sdk.Stub.Controller.StoreProjectAccepted = false;

            Assert.That(await sdk.Controller.StoreIHCProjectSegment(Chunks(Project)[0], 0, 1), Is.False);
            Assert.That(sdk.Stub.Controller.Project, Is.EqualTo(before));
        }

        [Test]
        public async Task ASegmentedUpload_CannotContinueAfterTheProjectWasReplacedAnotherWay()
        {
            using var sdk = Start();
            await sdk.Controller.StoreIHCProjectSegment(Chunks(Project)[0], 0, 1);
            await sdk.Controller.StoreProject(Project);

            Assert.That(await sdk.Controller.StoreIHCProjectSegment(Chunks(Project)[1], 1, 1), Is.False);
            Assert.That(sdk.Stub.Controller.Project!.Data, Is.EqualTo(Project.Data));
        }

        [Test]
        public async Task TheDefaultProject_ReadsTheSameSegmentedAsInOneCall()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.Controller.GetIHCProjectSegmentationSize(), Is.EqualTo(8192));
            Assert.That(await GetSegmented(sdk), Is.EqualTo((await sdk.Controller.GetProject())!.Data));
        }

        [Test]
        public async Task AControllerWithNoProject_HasNoSegments()
        {
            var stub = new ControllerStub();
            stub.Controller.Project = null;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Controller.GetIHCProjectNumberOfSegments(), Is.Zero);
            Assert.That(await sdk.Controller.GetIHCProjectSegment(0, 1, 0), Is.Null);
            Assert.That(await sdk.Controller.StoreIHCProjectSegment(Chunks(Project)[1], 1, 1), Is.False);
        }

        [TestCase(-1)]
        [TestCase(5)]
        public async Task ASegmentOutsideTheProject_ReadsAsNone(int index)
        {
            using var sdk = Start();
            await StoreSegmented(sdk, Project);

            Assert.That(await sdk.Controller.GetIHCProjectSegment(index, 1, 0), Is.Null);
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

        private static ProjectFile[] Chunks(ProjectFile project) =>
            [.. project.Data.Chunk(Size).Select(chunk => project with { Data = new string(chunk) })];

        private static async Task StoreSegmented(StubbedSdk sdk, ProjectFile project)
        {
            ProjectFile[] chunks = Chunks(project);
            for (int index = 0; index < chunks.Length; index++)
            {
                Assert.That(await sdk.Controller.StoreIHCProjectSegment(chunks[index], index, 1), Is.True, $"segment {index}");
            }
        }

        private static async Task<string> GetSegmented(StubbedSdk sdk)
        {
            var data = new StringBuilder();
            int count = await sdk.Controller.GetIHCProjectNumberOfSegments();
            for (int index = 0; index < count; index++)
            {
                data.Append((await sdk.Controller.GetIHCProjectSegment(index, 1, 0))!.Data);
            }
            return data.ToString();
        }
    }
}
