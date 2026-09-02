using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Ihc;
using ihc_openvisual.Configuration;
using Ihc.Vis;
using Ihc.Vis.Projects;
using Microsoft.Extensions.Logging;

namespace ihc_openvisual.Services;

/// <summary>
/// T019 (M6): the reporting collaborator extracted from <see cref="ProjectWorkflow"/>. The SDK generates the
/// FINISHED report bytes (content AND formatting) through <see cref="ProjectAppService.GenerateReport(Project,
/// ReportKind, ReportMode, string, string, IReportIconProvider?)"/>; this collaborator only picks the format and
/// icon mapping for the requested flow (US-040/041) and routes the returned bytes — to a temp file the OS browser
/// opens for view/print, or to the target the user chose in the save dialog. Holds no document state: it reads the
/// current project through the passed getter, so <see cref="ProjectWorkflow"/> keeps the document lifecycle and
/// just delegates its report methods here.
/// </summary>
internal sealed class ProjectReportWorkflow(
    ProjectAppService service, IDialogService dialogs, ILogger logger, Func<Project?> getCurrent) : IDisposable
{
    /// <summary>This type's entry point into the instrumentation core.</summary>
    private readonly OperationTelemetry _telemetry =
        new(AppTelemetryRegistry.Surface, nameof(ProjectReportWorkflow));

    private string? _viewDirectory;

    /// <summary>Where <see cref="ViewInBrowserAsync"/> puts the page it hands to the OS — a directory of its own,
    /// because the file name below is deterministic and the app is deliberately multi-instance: two instances
    /// viewing the same report would otherwise write one path, the second overwriting the page the first is
    /// reading (or failing outright while that viewer holds the file). Minted by the OS rather than keyed on
    /// anything of ours, so uniqueness is guaranteed instead of argued; created on FIRST use, so merely
    /// constructing this collaborator (the previewer does) writes nothing.</summary>
    private string ViewDirectory =>
        _viewDirectory ??= Directory.CreateTempSubdirectory("ihc-openvisual-reports-").FullName;

    /// <summary>
    /// T015 (R12): generates the picked report in the picked format via the facade — HTML with the app's SVG
    /// icon provider, plain text with the SDK's default stand-ins — to a temp file and opens it in the OS
    /// default application for that format (the US-063 view/print flow; a browser for HTML, whatever the system
    /// associates with .txt otherwise — hence the picker's neutral [Vis] label). Generation or write failures
    /// surface through the standard message dialog; open failures are handled by
    /// <see cref="IDialogService.OpenExternalUrlAsync"/> itself.
    /// </summary>
    public async Task ViewInBrowserAsync(ReportKind kind, ReportMode mode, ReportFormat format)
    {
        using OperationScope scope = _telemetry.Start(nameof(ViewInBrowserAsync));
        try
        {
            if (getCurrent() is not { } project)
            {
                return;   // no project open — the registry gate normally prevents this
            }
            string path = Path.Combine(ViewDirectory, FileName(kind, mode, format));
            await service.GenerateReport(project, kind, mode, MimeTypeOf(format), path, IconsFor(format));
            // The handover to the OS is the last step of this workflow, so a handover that did not happen is a
            // failure of it — not a silent no-op that leaves the installer waiting for a window (UX review CORE-03).
            if (!await dialogs.OpenExternalUrlAsync(path))
            {
                // This arm showed the installer a problem and left the scope ending OK, so the one
                // outcome the boolean return exists to report was invisible to every telemetry query. Coded
                // rather than exception-shaped, because nothing threw: the OS simply opened nothing.
                await FailureReport.RefusedAsync(
                    scope, logger, dialogs, ReportFailedTitle, HostProblems.ReportNotOpenable(path),
                    "The OS opened no viewer for the {Kind} {Mode} report at {Path}", kind, mode, path);
            }
        }
        catch (Exception ex)
        {
            await FailureReport.FailedAsync(
                scope, logger, dialogs, ReportFailedTitle, HostProblems.ReportViewFailed(ex), ex,
                "Failed to generate the {Kind} {Mode} report for browser view", kind, mode);
        }
    }

    /// <summary>
    /// T016 (R12): [Gem som…] — asks for a target path, suggested in the format the picker's format dropdown
    /// chose, then generates the picked report via the facade to that file in that format:
    /// <see cref="ReportFormat.Text"/> with the default unicode stand-ins, <see cref="ReportFormat.Html"/> with
    /// the app's SVG icons. Generation and save failures surface through the message dialog.
    /// </summary>
    public async Task SaveAsAsync(ReportKind kind, ReportMode mode, ReportFormat format)
    {
        using OperationScope scope = _telemetry.Start(nameof(SaveAsAsync));
        try
        {
            if (getCurrent() is not { } project)
            {
                return;   // no project open — the registry gate normally prevents this
            }
            string? path = await dialogs.PickSaveReportAsync(FileName(kind, mode, format), format);
            if (path is null)
            {
                return;   // cancelled
            }
            await service.GenerateReport(project, kind, mode, MimeTypeOf(format), path, IconsFor(format));
        }
        catch (Exception ex)
        {
            await FailureReport.FailedAsync(
                scope, logger, dialogs, ReportFailedTitle, HostProblems.ReportSaveFailed(ex), ex,
                "Failed to save the {Kind} {Mode} report", kind, mode);
        }
    }

    // The ONE title over a report that could not be produced. Viewing and saving fail for the same reasons — the
    // facade generates the bytes either way — so both routes name the failure identically.
    private const string ReportFailedTitle = "Rapport mislykkedes";

    /// <summary>The facade mimetype a picked format generates as — the ONE place a <see cref="ReportFormat"/>
    /// becomes a string, immediately beside the <c>GenerateReport</c> call that takes one.</summary>
    private static string MimeTypeOf(ReportFormat format) =>
        format == ReportFormat.Text ? ReportMimeTypes.PlainText : ReportMimeTypes.Html;

    /// <summary>The file name a generated report gets, carrying the extension of the picked format so the
    /// temp page and the save dialog's suggestion both match what the facade writes.</summary>
    private static string FileName(ReportKind kind, ReportMode mode, ReportFormat format) =>
        $"{kind}-{mode}.{ReportMimeTypes.FileExtensionFor(MimeTypeOf(format))}".ToLowerInvariant();

    /// <summary>The app's SVG icon mapping for HTML output; the SDK's default unicode stand-ins for text.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
        Justification = "The declared type IS the seam. IReportIconProvider is the SDK contract the report " +
                        "writer consumes; returning the application's own SvgReportIconProvider would point " +
                        "the dependency at the concrete implementation, which ARCHITECTURE.md forbids.")]
    private static IReportIconProvider? IconsFor(ReportFormat format) =>
        format == ReportFormat.Text ? null : new SvgReportIconProvider();

    /// <summary>Removes this run's viewing directory on shutdown, so the per-run scoping does not turn into
    /// per-run litter. Nothing to do when no report was ever viewed — the directory is created on first use.
    /// Best-effort by design: a viewer still holding a page just leaves the directory behind.</summary>
    public void Dispose()
    {
        if (_viewDirectory is not { } dir)
            return;
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not remove the report viewing directory {Path}", dir);
        }
    }
}
