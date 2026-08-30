#nullable enable
using System;
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
    /// than inside the engine — a caller that wants the locations asks <see cref="RunStructured(Ihc.Vis.Projects.Project, Ihc.Vis.Validation.ValidationProfile)"/>, which is the
    /// same run without the flattening and without naming the executor port.
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

        /// <summary>
        /// One run under an explicit profile, keeping the engine's OWN finding shape: the problem, the primary
        /// site and every RELATED site, none of it flattened.
        /// <para>
        /// It exists so a caller that wants the structured form does not have to name
        /// <see cref="IWholeProjectValidator"/> to get it. A GUI is forbidden to construct or run an executor
        /// (L5), so before this door the rich finding was reachable only from inside the SDK, and the related
        /// sites a grouped rule reports — the OTHER elements sharing a duplicate id, the rest of an
        /// under-populated module — were dropped at the boundary and could not be shown or navigated to.
        /// </para>
        /// </summary>
        /// <param name="project">The project to verify.</param>
        /// <param name="profile">Which rules run, and at what severity.</param>
        public static EquatableArray<ValidationFinding> RunStructured(Project project, ValidationProfile profile) =>
            RunStructured(project, profile, ProjectRules.Validator);

        /// <summary>
        /// The same door over a CALLER-SUPPLIED executor, so a host that configures the engine (per-rule
        /// timing, say) can use its own instance without the shared static becoming configurable for everyone.
        /// </summary>
        /// <param name="project">The project to verify.</param>
        /// <param name="profile">Which rules run, and at what severity.</param>
        /// <param name="validator">The executor to run.</param>
        public static EquatableArray<ValidationFinding> RunStructured(
            Project project, ValidationProfile profile, IWholeProjectValidator validator)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(validator);
            return validator.Validate(project, profile);
        }

        /// <summary>One run under an explicit profile — the door for a controller-capability verification.</summary>
        /// <param name="project">The project to verify.</param>
        /// <param name="profile">Which rules run, and at what severity.</param>
        public static ProjectValidationResult Run(Project project, ValidationProfile profile) =>
            Run(project, profile, ProjectRules.Validator);

        /// <summary>The same door over a caller-supplied executor; see the structured overload.</summary>
        /// <param name="project">The project to verify.</param>
        /// <param name="profile">Which rules run, and at what severity.</param>
        /// <param name="validator">The executor to run.</param>
        public static ProjectValidationResult Run(
            Project project, ValidationProfile profile, IWholeProjectValidator validator)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(validator);
            return ProjectValidationResult.FromFindings(
                [.. validator.Validate(project, profile).Select(Flatten)]);
        }

        /// <summary>
        /// The engine finding, in the shape existing callers read. The structured locations do not survive the
        /// conversion and are not meant to: the locator they collapse to is exactly what the flat shape has always
        /// carried, and the full form stays available one call further in.
        /// <para>
        /// The DIAGNOSTIC does survive, and the distinction is the point: a location is a shape this type does not
        /// have, while the English sentence is a field it does. Dropping it here left
        /// <see cref="ProjectValidationFinding.Diagnostic"/> null for everything the engine produced — so an
        /// upload refusal listed its items in Danish alone, with no text naming which attribute or which tag, on
        /// the one path a developer reads.
        /// </para>
        /// </summary>
        private static ProjectValidationFinding Flatten(ValidationFinding finding) =>
            new(finding.Severity, finding.Code.Value, finding.Primary?.Locator, finding.Problem.Message)
            {
                Category = finding.Category,
                Diagnostic = finding.Problem.Diagnostic,
            };
    }
}
