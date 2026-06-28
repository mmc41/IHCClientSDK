#nullable enable
namespace Ihc.Vis.Model
{
    /// <summary>
    /// The <c>project_info</c> metadata supplied when creating a new project. Named <c>VisProjectInfo</c>
    /// (not <c>ProjectInfo</c>) to avoid a simple-name clash with the SDK's controller-side
    /// <see cref="Ihc.ProjectInfo"/>, which would otherwise shadow it for consumers under <c>Ihc.*</c>.
    /// </summary>
    /// <remarks>
    /// Stage-1 deliberate limitation: this exposes only the field(s) populated in the testdata and is
    /// settable only at <c>CreateNew</c> — the wider <c>project_info</c>/<c>installer_info</c>/
    /// <c>customer_info</c> DTD field set, and an edit-metadata-on-a-loaded-project path on
    /// <c>VisProjectEditor</c>, are deferred to Stage 2 (mechanical to add).
    /// </remarks>
    public sealed record VisProjectInfo(string Programmer);

    /// <summary>
    /// The <c>installer_info</c> metadata supplied when creating a new project.
    /// </summary>
    public sealed record InstallerInfo(string Name, string Country);
}
