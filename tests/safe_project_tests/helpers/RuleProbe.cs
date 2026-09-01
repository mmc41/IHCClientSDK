using System.Linq;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Runs the whole-project engine face over a project and picks one rule's findings out of the result — the
    /// arrange every rule fixture in <c>problems/</c> opens with, so a per-rule test reads as the tree it builds
    /// and the row it expects rather than as the plumbing that gets there.
    /// </summary>
    /// <remarks>
    /// Import it with <c>using static Ihc.Vis.Tests.RuleProbe;</c>: a fixture's own member of the same name would
    /// hide these, so a fixture that needs a different run keeps it here as an overload rather than beside its
    /// tests. <see cref="Count(Project, string, ILibraryBlockSource?)"/> is the one such case today — a rule that
    /// declares the library context needs a profile the facade does not expose, so it goes through
    /// <see cref="ProjectVerification"/> directly, as the capacity fixture's controller-limit runs also do.
    /// </remarks>
    internal static class RuleProbe
    {
        public static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        public static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        public static string Message(Project project, string ruleId) =>
            Validate(project).Findings.First(f => f.RuleId == ruleId).Message;

        public static ProjectValidationFinding Single(Project project, string ruleId) =>
            Validate(project).Findings.Single(f => f.RuleId == ruleId);

        public static int Count(Project project, string ruleId, ILibraryBlockSource? library) =>
            ProjectVerification.Run(project, ValidationProfile.Categorized with { Library = library })
                .Findings.Count(f => f.RuleId == ruleId);

        public static string Message(Project project, string ruleId, ILibraryBlockSource? library) =>
            ProjectVerification.Run(project, ValidationProfile.Categorized with { Library = library })
                .Findings.First(f => f.RuleId == ruleId).Message;
    }
}
