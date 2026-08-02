#nullable enable
using System.Collections.Immutable;

using Ihc.Vis.Model;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// Accumulates <see cref="ProjectValidationFinding"/>s for one check run. Owns <see cref="Locate"/> — the
    /// single rule for how a finding names its subject (the element's <c>_0x</c> id token when it has one,
    /// else its tag) — so every check family points a GUI at an element the same way.
    /// </summary>
    internal sealed class FindingCollector
    {
        private readonly ImmutableArray<ProjectValidationFinding>.Builder items =
            ImmutableArray.CreateBuilder<ProjectValidationFinding>();

        public void Error(string ruleId, ProjectElement? element, string message) =>
            Add(ValidationSeverity.Error, ruleId, element, message);

        public void Warning(string ruleId, ProjectElement? element, string message) =>
            Add(ValidationSeverity.Warning, ruleId, element, message);

        public ImmutableArray<ProjectValidationFinding> ToImmutable() => items.ToImmutable();

        /// <summary>How a finding names its subject: the element's <c>_0x</c> id token, else its tag.</summary>
        public static string? Locate(ProjectElement? element) =>
            element is null ? null : element.GetAttribute("id") ?? element.Tag;

        private void Add(ValidationSeverity severity, string ruleId, ProjectElement? element, string message) =>
            items.Add(new ProjectValidationFinding(severity, ruleId, Locate(element), message));
    }
}
