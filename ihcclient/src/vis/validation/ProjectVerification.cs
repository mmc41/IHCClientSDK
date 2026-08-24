#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The two shipped verification entry points, on the engine.
    /// <para>
    /// Both are ONE run of the registered rules under a profile, which is the whole point of the profile axis:
    /// the structural checklist and the full categorized verification differ in WHICH rules run, not in which
    /// pipeline runs them. Documentation findings are advisory and only ever add warnings, so
    /// <c>IsValid</c> means the same thing in both.
    /// </para>
    /// <para>
    /// The engine's own finding carries a problem and structured locations; <see cref="ProjectValidationResult"/>
    /// carries the flatter shape its existing callers read. The conversion happens HERE, at the boundary, rather
    /// than inside the engine — a caller that wants the locations asks
    /// <see cref="IWholeProjectValidator.Validate"/> directly and gets them.
    /// </para>
    /// </summary>
    public static class ProjectVerification
    {
        /// <summary>The pre-serialize structural checklist.</summary>
        /// <param name="project">The project to verify.</param>
        public static ProjectValidationResult Structural(Project project) =>
            Run(project, ValidationProfile.ProjectOnly);

        /// <summary>The full categorized verification: the structural checklist plus the documentation checks.</summary>
        /// <param name="project">The project to verify.</param>
        public static ProjectValidationResult Categorized(Project project) =>
            Run(project, ValidationProfile.Categorized);

        /// <summary>One run under an explicit profile — the door for a controller-capability verification.</summary>
        /// <param name="project">The project to verify.</param>
        /// <param name="profile">Which rules run, and at what severity.</param>
        public static ProjectValidationResult Run(Project project, ValidationProfile profile)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(profile);
            return ProjectValidationResult.FromFindings(
                [.. ProjectRules.Validator.Validate(project, profile).Select(Flatten)]);
        }

        /// <summary>
        /// The engine finding, in the shape existing callers read. The structured locations do not survive the
        /// conversion and are not meant to: the locator they collapse to is exactly what the flat shape has always
        /// carried, and the full form stays available one call further in.
        /// </summary>
        private static ProjectValidationFinding Flatten(ValidationFinding finding) =>
            new(finding.Severity, finding.Code.Value, finding.Primary?.Locator, finding.Problem.Message)
            {
                Category = finding.Category,
            };
    }
}
