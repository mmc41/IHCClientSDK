using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc;
using Ihc.Tests.Shared;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The opt-in file digest.
    ///
    /// A SHA-256 of the exact bytes is a stable FINGERPRINT of one customer project: it carries no content
    /// and cannot be reversed, but two sessions that touched byte-identical files emit the same value, so
    /// records naming no file become linkable across the backend. That is what makes it useful for diagnosis
    /// and what makes it opt-in. The byte COUNT is unconditional, because a size reveals nothing comparable.
    /// </summary>
    public class FileDigestTelemetryTests
    {
        private const string DigestTag = "ihc.project.content.sha256";
        private const string SizeTag = "ihc.project.file_size";

        private static IhcSettings SettingsWith(bool logSensitiveData) =>
            TestSetup.Settings with { LogSensitiveData = logSensitiveData };

        private static async Task<Activity> SaveCapturingSpan(bool logSensitiveData)
        {
            using (TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "ProjectAppService.Save", "ProjectAppService.Load" }))
            {
                var app = new ProjectAppService(SettingsWith(logSensitiveData));
                Project project = app.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
                string path = Path.Combine(Path.GetTempPath(), "ihc_t026_" + System.Guid.NewGuid().ToString("N") + ".vis");
                try
                {
                    await app.SaveDocument(project, path);
                    return capture.Spans.Single(s => s.OperationName == "ProjectAppService.Save");
                }
                finally
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>The shipped default. A digest appearing here would be a privacy defect.</summary>
        [Test]
        public async Task WithSensitiveLoggingOff_NoDigestIsRecorded_ButTheSizeStillIs()
        {
            Activity save = await SaveCapturingSpan(logSensitiveData: false);

            Assert.Multiple(() =>
            {
                Assert.That(save.GetTagItem(DigestTag), Is.Null,
                    "the fingerprint is opt-in; under default settings it must not leave the process");
                Assert.That(save.GetTagItem(SizeTag), Is.Not.Null,
                    "the byte count is unconditional - a size links nothing");
            });
        }

        [Test]
        public async Task WithSensitiveLoggingOn_TheDigestIsRecorded()
        {
            Activity save = await SaveCapturingSpan(logSensitiveData: true);

            Assert.Multiple(() =>
            {
                Assert.That(save.GetTagItem(DigestTag)?.ToString(), Has.Length.EqualTo(64),
                    "a SHA-256 rendered as lowercase hex");
                Assert.That(save.GetTagItem(SizeTag), Is.Not.Null);
            });
        }

        [Test]
        public async Task TheLoadSideBehavesTheSameWay()
        {
            using (TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "ProjectAppService.Save", "ProjectAppService.Load" }))
            {
                await new ProjectAppService(SettingsWith(false)).Load("testdata/projects/Project1-SimpelWired.vis");
                Activity load = capture.Spans.Single(s => s.OperationName == "ProjectAppService.Load");

                Assert.Multiple(() =>
                {
                    Assert.That(load.GetTagItem(DigestTag), Is.Null);
                    Assert.That(load.GetTagItem(SizeTag), Is.Not.Null, "the size of what was actually read");
                });
            }
        }

        /// <summary>
        /// The property that makes it both useful and sensitive: identical bytes give an identical value.
        /// Asserting it here is what documents the linkage as intended rather than incidental.
        /// </summary>
        [Test]
        public async Task TheDigestIsStableForIdenticalBytes()
        {
            using (TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "ProjectAppService.Save", "ProjectAppService.Load" }))
            {
                var app = new ProjectAppService(SettingsWith(true));
                await app.Load("testdata/projects/Project1-SimpelWired.vis");
                await app.Load("testdata/projects/Project1-SimpelWired.vis");

                string?[] digests = capture.Spans.Where(s => s.OperationName == "ProjectAppService.Load")
                    .Select(s => s.GetTagItem(DigestTag)?.ToString()).ToArray();

                Assert.That(digests, Has.Length.EqualTo(2));
                Assert.That(digests[0], Is.EqualTo(digests[1]),
                    "the same file fingerprints the same - which is the linkage the flag gates");
            }
        }
    }
}
