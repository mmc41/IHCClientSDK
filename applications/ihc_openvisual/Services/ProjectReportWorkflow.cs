using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Projects;
using Ihc.Vis.Reporting;
using Microsoft.Extensions.Logging;

namespace ihc_openvisual.Services;

/// <summary>
/// T019 (M6): the reporting collaborator extracted from <see cref="ProjectWorkflow"/> — builds the render-ready
/// installation / end-user / function-block report models for the open project (US-040/041) over the stateless
/// <see cref="ProjectAppService"/>, and writes a rendered HTML page to a temp file for the browser to open. Holds
/// no document state: it reads the current project through the passed getter, so <see cref="ProjectWorkflow"/>
/// keeps the document lifecycle and just delegates its report methods here.
/// </summary>
internal sealed class ProjectReportWorkflow(
    ProjectAppService service, IDialogService dialogs, ILogger logger, Func<Project?> getCurrent)
{
    /// <summary>The render-ready installation report model for the open project (US-040), or null if none.</summary>
    public InstallationReport? Installation() =>
        getCurrent() is { } project ? service.GenerateInstallationReport(project) : null;

    /// <summary>The render-ready end-user report model for the open project (US-040), or null if none.</summary>
    public EndUserReport? EndUser() =>
        getCurrent() is { } project ? service.GenerateEndUserReport(project) : null;

    /// <summary>The render-ready function-block documentation report model for the open project (US-041), or null.</summary>
    public FunctionBlockReport? FunctionBlock() =>
        getCurrent() is { } project ? service.GenerateFunctionBlockReport(project) : null;

    /// <summary>Writes a rendered report HTML page to a temp file (US-040) and returns its path for the browser to
    /// open; null on failure. The file is a self-contained static page — no controller contact.</summary>
    public async Task<string?> WriteHtmlAsync(string fileStem, string html)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(ProjectReportWorkflow)}.{nameof(WriteHtmlAsync)}");
        try
        {
            string dir = Path.Combine(Path.GetTempPath(), "ihc-openvisual-reports");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileStem + ".html");
            await File.WriteAllTextAsync(path, html, System.Text.Encoding.UTF8);
            return path;
        }
        catch (Exception ex)
        {
            ActivityExtensions.SetError(activity, ex);
            logger.LogError(ex, "Failed to write report HTML {Stem}", fileStem);
            await dialogs.ShowMessageAsync("Report failed", ex.Message);
            return null;
        }
    }
}
