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
            ReportShapeDocument document = kind switch
            {
                ReportKind.Functions => FunctionsReportBuilder.Build(project, generatedAt),
                ReportKind.Installation => InstallationReportBuilder.Build(project, generatedAt),
                ReportKind.FunctionBlocks => FunctionBlockReportBuilder.Build(project, generatedAt),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown report kind."),
            };
            ReportShapeDocument selected = ReportModeFilter.Select(document, mode);
            return mimeType switch
            {
                ReportMimeTypes.PlainText => TextReportWriter.Write(selected, iconProvider),
                ReportMimeTypes.Html => HtmlReportWriter.Write(selected, iconProvider),
                _ => throw new ArgumentException(
                    $"Unsupported report mimetype '{mimeType}'. Supported: '{ReportMimeTypes.Html}', '{ReportMimeTypes.PlainText}'.",
                    nameof(mimeType)),
            };
        }
    }
}
