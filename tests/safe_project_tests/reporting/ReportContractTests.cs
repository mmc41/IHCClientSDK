using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Ihc.Vis.Reporting;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T014 (AC8/AC9): the facade and icon-contract edges. An unsupported mimetype is rejected with a
    /// clear error; the stream and file overloads produce byte-identical output; the icon fallback rules
    /// (unknown key, null/empty provider result, HTML-only provider generating text) never fail generation
    /// and land on the default unicode stand-ins; and the provider is consulted for BOTH formats.
    /// </summary>
    public class ReportContractTests
    {
        /// <summary>The pinned report clock (S10) — also what makes two generation calls byte-comparable.</summary>
        private sealed class ReportClock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private static ProjectAppService App() => new(TestSetup.Settings, new BuiltInCatalog(), new ReportClock());

        private static Project Load() =>
            App().Load(new MemoryStream(TestData.ReadBytes(Path.Combine("projects", "project5-Dokumentation.vis"))))
                .GetAwaiter().GetResult();

        private static async Task<byte[]> Generate(Project project, string mimeType, IReportIconProvider? icons)
        {
            using var output = new MemoryStream();
            await App().GenerateReport(project, ReportKind.FunctionBlocks, ReportMode.Full, mimeType, output, icons);
            return output.ToArray();
        }

        [Test]
        public void UnsupportedMimetype_IsRejected_WithAClearError()
        {
            var exception = Assert.ThrowsAsync<ArgumentException>(() =>
                App().GenerateReport(Load(), ReportKind.Functions, ReportMode.Standard, "application/pdf", new MemoryStream()));

            Assert.That(exception!.Message,
                Does.Contain("application/pdf").And.Contain(ReportMimeTypes.Html).And.Contain(ReportMimeTypes.PlainText),
                "the error names the offending mimetype and the supported ones");
        }

        [Test]
        public async Task StreamAndFileOverloads_ProduceIdenticalBytes()
        {
            Project project = Load();
            byte[] streamed = await Generate(project, ReportMimeTypes.PlainText, icons: null);
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "t014-overload-identity.txt");
            try
            {
                await App().GenerateReport(project, ReportKind.FunctionBlocks, ReportMode.Full,
                    ReportMimeTypes.PlainText, path);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(streamed),
                    "the file convenience overload writes exactly the stream overload's bytes");
            }
            finally
            {
                File.Delete(path);
            }
        }

        // A provider that only ever answers null/empty — generation must fall back to the default
        // stand-ins for every key and produce exactly the default output.
        private sealed class EmptyProvider : IReportIconProvider
        {
            public string? GetFragment(string mimeType, string iconKey) => string.Empty;
            public string? GetDefinitionsBlock(string mimeType, IReadOnlyCollection<string> iconKeys) => null;
        }

        [Test]
        public async Task NullOrEmptyProviderResults_FallBackToTheDefaultStandIns()
        {
            Project project = Load();
            byte[] withDefaults = await Generate(project, ReportMimeTypes.PlainText, icons: null);
            byte[] withEmptyProvider = await Generate(project, ReportMimeTypes.PlainText, new EmptyProvider());

            Assert.Multiple(() =>
            {
                Assert.That(withEmptyProvider, Is.EqualTo(withDefaults),
                    "a provider answering null/empty for every key is indistinguishable from the default");
                Assert.That(DefaultReportIcons.StandInFor("no-such-key"), Is.EqualTo("·"),
                    "an unknown icon key resolves to the neutral stand-in — generation never fails over icons");
            });
        }

        // Records which formats it was consulted for; customizes ONLY the text format.
        private sealed class RecordingTextProvider : IReportIconProvider
        {
            public HashSet<string> SeenMimeTypes { get; } = new(StringComparer.Ordinal);

            public string? GetFragment(string mimeType, string iconKey)
            {
                SeenMimeTypes.Add(mimeType);
                return mimeType == ReportMimeTypes.PlainText && iconKey == "pin-in" ? "@@" : null;
            }

            public string? GetDefinitionsBlock(string mimeType, IReadOnlyCollection<string> iconKeys) => null;
        }

        [Test]
        public async Task Provider_IsConsultedForBothFormats_AndHtmlOnlyCustomizationFallsBackForText()
        {
            Project project = Load();
            var recorder = new RecordingTextProvider();

            byte[] text = await Generate(project, ReportMimeTypes.PlainText, recorder);
            await Generate(project, ReportMimeTypes.Html, recorder);

            string rendered = System.Text.Encoding.UTF8.GetString(text);
            Assert.Multiple(() =>
            {
                Assert.That(recorder.SeenMimeTypes,
                    Does.Contain(ReportMimeTypes.PlainText).And.Contain(ReportMimeTypes.Html),
                    "the provider is consulted for both formats");
                Assert.That(rendered, Does.Contain("@@ Kip"),
                    "a text-format customization reaches the text output");
                Assert.That(rendered, Does.Contain("← Udgang"),
                    "keys the provider declines fall back to the default stand-ins");
            });
        }
    }
}
