using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Ihc;
using Ihc.Tests.Shared;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The phases inside a load and a save.
    ///
    /// Reading the bytes, normalizing, indexing and diffing all happen inside ONE <c>Load</c> span, so a load
    /// that got slower says nothing about WHICH part did. Each phase now reports its own span with the size or
    /// count that explains its cost - and one of those counts doubles as an early corruption warning.
    /// </summary>
    public class LoadPhaseTelemetryTests
    {
        private const string Fixture = "testdata/projects/Project1-SimpelWired.vis";

        [Test]
        public async Task ALoadEmitsItsPhaseSpansUnderTheLoadSpan_EachCarryingItsSizeOrCount()
        {
            using (TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName))
            {
                await new ProjectAppService(TestSetup.Settings).Load(Fixture);

                Activity load = capture.Spans.Single(s => s.OperationName == "ProjectAppService.Load");
                Activity read = capture.Spans.Single(s => s.OperationName == "ProjectReader.Read");

                Assert.Multiple(() =>
                {
                    Assert.That(read.Parent, Is.SameAs(load), "the read is a phase OF the load, not a sibling");
                    Assert.That(read.GetTagItem("ihc.project.file_size"), Is.Not.Null.And.Not.EqualTo(0),
                        "the bytes going in - the size that explains the read's cost");
                    Assert.That(read.StartTimeUtc, Is.GreaterThanOrEqualTo(load.StartTimeUtc),
                        "a child cannot start before its parent");
                });
            }
        }

        [Test]
        public async Task ASaveEmitsTheSerializePhase_CarryingTheBytesOut()
        {
            using (TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName))
            {
                var app = new ProjectAppService(TestSetup.Settings);
                Project project = await app.Load(Fixture);
                using var stream = new System.IO.MemoryStream();
                await app.Save(project, stream, ProjectSaveOptions.PreserveExistingMetadata);

                Activity serialize = capture.Spans.Last(s => s.OperationName == "ProjectSerializer.Serialize");
                Activity save = capture.Spans.Last(s => s.OperationName == "ProjectAppService.Save");

                Assert.That(serialize.Parent, Is.SameAs(save), "the serialize is a phase OF the save");
            }
        }

        [Test]
        public void IndexingAndDiffingReportTheirSizes()
        {
            using (TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName))
            {
                var app = new ProjectAppService(TestSetup.Settings);
                Project project = app.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
                IProjectDocument document = app.OpenDocument(project);
                document.Apply(new Ihc.Vis.Session.AddLocality("Ny lokalitet"));

                Activity index = capture.Spans.Last(s => s.OperationName == "ProjectIndex.Build");
                Activity diff = capture.Spans.Last(s => s.OperationName == "ProjectChangeSet.Diff");

                Assert.Multiple(() =>
                {
                    Assert.That((int)index.GetTagItem("ihc.project.element_count")!, Is.GreaterThan(0),
                        "the element count is what makes a slow index explicable");
                    Assert.That(diff.GetTagItem("ihc.diff.added_count"), Is.EqualTo(1),
                        "one locality added, and the diff says so");
                    Assert.That(diff.GetTagItem("ihc.diff.removed_count"), Is.EqualTo(0));
                    Assert.That(diff.GetTagItem("ihc.diff.changed_count"), Is.Not.Null);
                    Assert.That(diff.GetTagItem("ihc.diff.child_list_changed_count"), Is.Not.Null);
                });
            }
        }

        /// <summary>
        /// What open-time normalization does to an authentic vendor file, measured rather than assumed.
        ///
        /// It is a pure RE-HOIST: the built-in catalog enum definitions are removed and re-added with freshly
        /// allocated ids. So it never changes NOTHING - re-minting ids is what it does, on every open, by
        /// design. What IS invariant is the shape: added equals removed, and nothing is edited in place. An
        /// unbalanced re-hoist or an in-place change on a file the vendor just wrote would each be anomalous,
        /// and neither is visible in a total.
        /// </summary>
        [Test]
        public async Task NormalizingAnAuthenticVendorFile_IsABalancedReHoistThatChangesNothingInPlace()
        {
            using (TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName))
            {
                var app = new ProjectAppService(TestSetup.Settings);
                Project loaded = await app.Load(Fixture);

                app.NormalizeOnOpen(loaded);

                Activity normalize = capture.Spans.Last(s => s.OperationName == "ProjectAppService.NormalizeOnOpen");
                int added = (int)normalize.GetTagItem("ihc.normalize.added_count")!;
                int removed = (int)normalize.GetTagItem("ihc.normalize.removed_count")!;

                Assert.Multiple(() =>
                {
                    Assert.That(added, Is.GreaterThan(0),
                        "normalization re-hoists the catalog enums with fresh ids on EVERY open");
                    Assert.That(removed, Is.EqualTo(added),
                        "a re-hoist is balanced - every definition removed comes back with a new id");
                    Assert.That(normalize.GetTagItem("ihc.normalize.changed_count"), Is.EqualTo(0),
                        "nothing is edited in place; an in-place change on a vendor file would be anomalous");
                });
            }
        }
    }
}
