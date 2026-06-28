#nullable enable
namespace Ihc.Projects
{
    /// <summary>
    /// The metadata supplied when creating a new project — the write path for the fields exposed read-only
    /// on <see cref="Project"/> (<see cref="Project.Programmer"/>, <see cref="Project.InstallerName"/>,
    /// <see cref="Project.InstallerCountry"/>). Named <c>ProjectDetails</c> rather than <c>ProjectInfo</c> so
    /// it never clashes with the controller-side <see cref="Ihc.ProjectInfo"/> (a different concept: a cheap
    /// controller-reported summary), even when a caller imports both <c>Ihc</c> and <c>Ihc.Projects</c>.
    /// </summary>
    /// <remarks>
    /// Stage-1 deliberate limitation: this exposes only the fields populated in the testdata. The wider
    /// <c>project_info</c>/<c>installer_info</c>/<c>customer_info</c> DTD field set can be added as positional-safe
    /// <c>init</c> properties in Stage 2 without breaking callers.
    /// </remarks>
    public sealed record ProjectDetails(string Programmer, string InstallerName, string InstallerCountry)
    {
        public override string ToString() =>
            $"ProjectDetails(Programmer={Programmer}, InstallerName={InstallerName}, InstallerCountry={InstallerCountry})";
    }
}
