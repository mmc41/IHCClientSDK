#nullable enable
using System;

using Ihc.Vis.Projects;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// The report generation pipeline entry (spec D1): builder (per <see cref="ReportKind"/>) →
    /// <see cref="ReportModeFilter"/> → format writer (per mimetype). The facade owns the public door,
    /// null-guards and tracing; this orchestrator owns kind/format dispatch and the unsupported-mimetype
    /// contract (R3: unknown mimetype → clear error).
    /// </summary>
    internal static class ReportGenerator
    {
        public static byte[] Generate(Project project, ReportKind kind, ReportMode mode, string mimeType,
            IReportIconProvider? iconProvider, DateTimeOffset generatedAt)
        {
            // Reports are built rarely and read carefully, so what was asked for matters as much as how long
            // it took: the same project renders very differently as a full HTML functions report and a
            // standard plain-text one, and the output size is the only signal that says which. Through the
            // core, so the R3 contract below — an unknown kind or mimetype throws — is recorded as the
            // failure it is rather than as a report that was generated.
            return Telemetry.Run(nameof(Generate), scope =>
            {
                scope.Activity?.SetTag(SdkTelemetryRegistry.Attributes.ReportKind, kind.ToString());
                scope.Activity?.SetTag(SdkTelemetryRegistry.Attributes.ReportMode, mode.ToString());
                scope.Activity?.SetTag(SdkTelemetryRegistry.Attributes.ReportMime, mimeType);

                ReportShapeDocument document = kind switch
                {
                    ReportKind.Functions => FunctionsReportBuilder.Build(project, generatedAt),
                    ReportKind.Installation => InstallationReportBuilder.Build(project, generatedAt),
                    ReportKind.FunctionBlocks => FunctionBlockReportBuilder.Build(project, generatedAt),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown report kind."),
                };
                ReportShapeDocument selected = ReportModeFilter.Select(document, mode);
                byte[] rendered = mimeType switch
                {
                    ReportMimeTypes.PlainText => TextReportWriter.Write(selected, iconProvider),
                    ReportMimeTypes.Html => HtmlReportWriter.Write(selected, iconProvider),
                    _ => throw new ArgumentException(
                        $"Unsupported report mimetype '{mimeType}'. Supported: '{ReportMimeTypes.Html}', '{ReportMimeTypes.PlainText}'.",
                        nameof(mimeType)),
                };

                scope.Activity?.SetTag(SdkTelemetryRegistry.Attributes.ReportBytes, rendered.Length);
                return rendered;
            });
        }

        /// <summary>This generator's entry point into the instrumentation core.</summary>
        private static readonly OperationTelemetry Telemetry =
            new OperationTelemetry(SdkTelemetryRegistry.Surface, nameof(ReportGenerator));
    }
}
