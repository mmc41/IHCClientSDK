using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Ihc;
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
    ProjectAppService service, IDialogService dialogs, ILogger logger, Func<Project?> getCurrent)
{
    /// <summary>
    /// T015 (R12): generates the picked report via the facade — HTML with the app's SVG icon provider —
    /// to a temp file and opens it in the OS default browser (the US-063 view/print flow). Generation or
    /// write failures surface through the standard message dialog; browser-open failures are handled by
    /// <see cref="IDialogService.OpenExternalUrlAsync"/> itself.
    /// </summary>
    public async Task ViewInBrowserAsync(ReportKind kind, ReportMode mode)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectReportWorkflow)}.{nameof(ViewInBrowserAsync)}");
        try
        {
            if (getCurrent() is not { } project)
            {
                return;   // no project open — the registry gate normally prevents this
            }
            string dir = Path.Combine(Path.GetTempPath(), "ihc-openvisual-reports");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"{kind}-{mode}.html".ToLowerInvariant());
            await service.GenerateReport(project, kind, mode, ReportMimeTypes.Html, path, new SvgReportIconProvider());
            await dialogs.OpenExternalUrlAsync(path);
        }
        catch (Exception ex)
        {
            ActivityExtensions.SetError(activity, ex);
            logger.LogError(ex, "Failed to generate the {Kind} {Mode} report for browser view", kind, mode);
            await dialogs.ShowMessageAsync("Report failed", ex.Message);
        }
    }

    /// <summary>
    /// T016 (R12): [Gem som…] — asks for a target path (.html/.txt), then generates the picked report via
    /// the facade to that file: text/plain with the default unicode stand-ins for a .txt target, HTML with
    /// the app's SVG icons otherwise. Generation and save failures surface through the message dialog.
    /// </summary>
    public async Task SaveAsAsync(ReportKind kind, ReportMode mode)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectReportWorkflow)}.{nameof(SaveAsAsync)}");
        try
        {
            if (getCurrent() is not { } project)
            {
                return;   // no project open — the registry gate normally prevents this
            }
            string? path = await dialogs.PickSaveReportAsync($"{kind}-{mode}.html".ToLowerInvariant());
            if (path is null)
            {
                return;   // cancelled
            }
            bool asText = path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
            await service.GenerateReport(project, kind, mode,
                asText ? ReportMimeTypes.PlainText : ReportMimeTypes.Html,
                path,
                asText ? null : new SvgReportIconProvider());
        }
        catch (Exception ex)
        {
            ActivityExtensions.SetError(activity, ex);
            logger.LogError(ex, "Failed to save the {Kind} {Mode} report", kind, mode);
            await dialogs.ShowMessageAsync("Report failed", ex.Message);
        }
    }

}
