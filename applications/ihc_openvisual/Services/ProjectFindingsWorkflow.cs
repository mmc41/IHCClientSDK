using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Ihc;
using ihc_openvisual.Configuration;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;
using Microsoft.Extensions.Logging;

namespace ihc_openvisual.Services;

/// <summary>
/// What the Problemer panel asks to have exported: the findings it is showing, the label for the order they are
/// in, and which tiers were included.
///
/// <para>All of them are things only the panel knows, and none is recoverable from the findings afterwards.
/// In particular <see cref="Severities"/> is NOT derivable from <see cref="Findings"/>: a list with no Info
/// rows and a list that excluded the Info tier are the same list, and only this says which happened.</para>
///
/// <para><see cref="ErrorTiers"/> is the same argument one level down. The panel splits Error findings into
/// two tiers — <c>Fatale fejl</c> for a finding whose rule also refuses an operation, <c>Fejl</c> for the rest
/// — and both are <see cref="ValidationSeverity.Error"/>, so <see cref="Severities"/> cannot say which of the
/// two a filtered list came from.</para>
///
/// <para>Declared beside its CONSUMER rather than its producer, as <c>ValidationRequest</c> is declared beside
/// <c>ValidationWorker</c>. A request type owned by the view-model would make this layer's signatures depend on
/// the panel, which is the wrong direction: the workflow is reachable, and testable, without a panel.</para>
/// </summary>
/// <param name="Findings">The visible rows' findings, filtered and ordered exactly as shown.</param>
/// <param name="Order">A label for that order, in the form <c>host:&lt;column&gt;</c> or <c>host:&lt;column&gt; desc</c>.</param>
/// <param name="Severities">The severities that were included, in enum order.</param>
/// <param name="ErrorTiers">How the panel's two Error filters stood — Fatale fejl and Fejl.</param>
public sealed record FindingsExportRequest(
    EquatableArray<ValidationFinding> Findings,
    string Order,
    EquatableArray<ValidationSeverity> Severities,
    ErrorTierFilter ErrorTiers);

/// <summary>
/// US-085: routes the Problemer panel's export request to a file the user chooses.
///
/// <para><b>A sibling of <see cref="ProjectReportWorkflow"/> rather than a method on it.</b> That one owns a
/// temporary viewing directory and an icon provider, and disposes the directory on shutdown; this needs neither,
/// and inheriting both would have made an export that never opens a browser responsible for cleaning one up.</para>
///
/// <para><b>The split of responsibility is the point.</b> The panel decides WHAT is exported — which findings,
/// in which order, under which tiers — because those are facts only it knows. This decides WHERE it goes, and
/// knows nothing about filters or sorting. Neither one knows the file format: that is the SDK's, reached through
/// the facade.</para>
/// </summary>
/// <param name="getDocumentName">
/// What the open document is called. A <see cref="Project"/> carries no path, no filename and no provenance, so
/// the source name a findings file records is something only the host can supply — and this is the delegate that
/// supplies it, rather than the workflow reaching into the session for it.
/// </param>
internal sealed class ProjectFindingsWorkflow(
    ProjectAppService service, IDialogService dialogs, ILogger logger,
    Func<Project?> getCurrent, Func<string> getDocumentName)
{
    /// <summary>This type's entry point into the instrumentation core.</summary>
    private readonly OperationTelemetry _telemetry =
        new(AppTelemetryRegistry.Surface, nameof(ProjectFindingsWorkflow));

    /// <summary>The ONE title over a findings list that could not be written.</summary>
    internal const string ExportFailedTitle = "Eksport mislykkedes";

    /// <summary>
    /// Asks for a destination and writes the panel's list there.
    /// <para>
    /// The request travels whole into <c>ExportFindings</c>'s caller-supplied-sequence overload, so the file
    /// holds exactly the findings the panel handed over, in that order — nothing here re-sorts, re-filters or
    /// re-validates, and a second validation run is exactly how the file and the screen would come to disagree.
    /// </para>
    /// </summary>
    public async Task ExportAsync(FindingsExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using OperationScope scope = _telemetry.Start(nameof(ExportAsync));
        try
        {
            if (getCurrent() is not { } project)
            {
                return;   // no project open — the panel's state gate normally prevents this
            }

            string? path = await dialogs.PickSaveFindingsAsync(SuggestedFileName());
            if (path is null)
            {
                return;   // cancelled: nothing written, and nothing reported — the user said no
            }

            await service.ExportFindings(project, request.Findings.AsImmutableArray(), path,
                FindingExportOptions.Default with
                {
                    SourceName = getDocumentName(),
                    Order = request.Order,
                    Severities = request.Severities,
                    ErrorTiers = request.ErrorTiers,
                });
        }
        catch (Exception ex)
        {
            await FailureReport.FailedAsync(
                scope, logger, dialogs, ExportFailedTitle, HostProblems.FindingsExportFailed(ex), ex,
                "Failed to export the findings list");
        }
    }

    /// <summary>
    /// What the save dialog suggests: the open document's name with its extension swapped, so a findings file
    /// lands beside the project it is about and is recognisable as belonging to it.
    /// </summary>
    private string SuggestedFileName()
    {
        string document = getDocumentName();
        string stem = System.IO.Path.GetFileNameWithoutExtension(document);
        return $"{(stem.Length > 0 ? stem : document)}-fejlliste.{FindingExportFormat.FileExtension}";
    }
}
