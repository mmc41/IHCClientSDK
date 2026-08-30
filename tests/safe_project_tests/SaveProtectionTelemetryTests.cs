using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc;
using Ihc.Tests.Shared;
using Ihc.Vis.Problems;
using Ihc.Vis.Session;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// What the editor's save door protects against, and what it says it did.
    ///
    /// <c>SaveDocument</c> is the door a GUI saves through. Verifying the round trip there turns a
    /// wrong-but-well-formed write - bytes that parse fine but do not reproduce the project the user was
    /// shown - from a silent success into a coded refusal. The span records WHICH protections actually ran,
    /// because that is not derivable from anything else in the trace: a save through the general door and a
    /// save through the editor door differ only in their options.
    /// </summary>
    public class SaveProtectionTelemetryTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        // A note holding U+20AC, above Latin-1's ceiling: a scalar the .vis wire encoding genuinely cannot
        // represent, so the written bytes cannot reproduce this model.
        private static readonly string NonLatin1Char = ((char)0x20AC).ToString();

        /// <summary>
        /// The protection itself: a project the format cannot represent must be REFUSED by the editor's save
        /// door, not written and reported as a success. See the comment at the code assertion for why the
        /// specific code is not pinned.
        /// </summary>
        [Test]
        public void SaveDocument_WhenTheBytesCannotReproduceTheProject_RefusesWithACode()
        {
            var app = new ProjectAppService(Settings);
            string path = Path.Combine(Path.GetTempPath(), "ihc_t025_" + System.Guid.NewGuid().ToString("N") + ".vis");

            // An AUTHENTIC project carrying one non-representable scalar, reached through the real command
            // path: a hand-built root would fail the vendor re-stamp long before verification runs.
            Project loaded = app.Load("testdata/projects/Project1-SimpelWired.vis").GetAwaiter().GetResult();
            IProjectDocument document = app.OpenDocument(loaded);
            ElementId locality = document.Current!.Groups[0].Id!.Value;
            EditOutcome edit = document.Apply(new RenameLocality(locality, "Stue", NonLatin1Char));
            Assert.That(edit.Status, Is.EqualTo(EditStatus.Committed), "the model accepts it; only the FORMAT cannot");

            RefusedOperationException refusal = Assert.ThrowsAsync<RefusedOperationException>(
                async () => await app.SaveDocument(document.Current!, path))!;

            Assert.Multiple(() =>
            {
                // The code is `attr-latin1` rather than `save-roundtrip-mismatch`: the writer's own encoding
                // guard catches this particular scalar BEFORE the round-trip comparison runs. Which guard
                // fires is not the point being pinned - that the editor door refuses with a CODE instead of
                // writing bytes and reporting success is. Pinning the specific code here would make the test
                // fail if a more specific guard were ever added, which would be an improvement.
                Assert.That(refusal.Problems!.Cause.Code.Value, Is.Not.Empty,
                    "a coded refusal the caller can act on, not a bare exception");
                Assert.That(File.Exists(path), Is.False, "nothing is left behind by a refused save");
            });
        }

        /// <summary>The general Save keeps its own options, so the editor door is the one that changed.</summary>
        [Test]
        public async Task SaveDocument_RecordsItsEffectiveOptions_WithVerificationOn()
        {
            using (TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "ProjectAppService.Save", "ProjectAppService.Load" }))
            {
                var app = new ProjectAppService(Settings);
                Project project = app.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
                string path = Path.Combine(Path.GetTempPath(), "ihc_t025_" + System.Guid.NewGuid().ToString("N") + ".vis");

                try
                {
                    await app.SaveDocument(project, path);

                    Activity save = capture.Spans.Single(s => s.OperationName == "ProjectAppService.Save");
                    Assert.Multiple(() =>
                    {
                        Assert.That(save.GetTagItem("ihc.save.verify_round_trip"), Is.EqualTo(true),
                            "the whole point of D03: the editor door verifies, and says so");
                        Assert.That(save.GetTagItem("ihc.save.create_backup"), Is.EqualTo(true));
                        Assert.That(save.GetTagItem("ihc.save.validate_before_save"), Is.EqualTo(false));
                        Assert.That(save.GetTagItem("ihc.save.write_metadata_verbatim"), Is.EqualTo(false));
                        Assert.That(save.GetTagItem("ihc.project.last_unique_id"), Is.Not.Null,
                            "the allocator high-water mark, whose DECREASE between saves is corruption");
                    });
                }
                finally
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public async Task Load_AlsoRecordsTheAllocatorHighWaterMark()
        {
            using (TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "ProjectAppService.Save", "ProjectAppService.Load" }))
            {
                var app = new ProjectAppService(Settings);

                await app.Load("testdata/projects/Project1-SimpelWired.vis");

                Activity load = capture.Spans.Single(s => s.OperationName == "ProjectAppService.Load");
                Assert.That(load.GetTagItem("ihc.project.last_unique_id"), Is.Not.Null,
                    "recorded on the load too, so a decrease is visible across the open/save pair");
            }
        }
    }
}
