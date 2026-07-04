#nullable enable
using System;
using System.Linq;

namespace Ihc.Projects
{
    /// <summary>
    /// Thrown when a project fails the pre-persist validation checklist (e.g. before a controller upload).
    /// Carries the full structured <see cref="Result"/> so a GUI can present every finding at once instead of
    /// re-running <c>Validate</c>.
    /// </summary>
    public sealed class ProjectValidationException : InvalidOperationException
    {
        /// <summary>The complete validation outcome that caused the throw.</summary>
        public ProjectValidationResult Result { get; }

        public ProjectValidationException(ProjectValidationResult result)
            : base(BuildMessage(result ?? throw new ArgumentNullException(nameof(result))))
        {
            Result = result;
        }

        private static string BuildMessage(ProjectValidationResult result)
        {
            int count = result.Errors.IsDefaultOrEmpty ? 0 : result.Errors.Length;
            string preview = count == 0
                ? string.Empty
                : ": " + string.Join(" | ", result.Errors.Take(5)) + (count > 5 ? $" | … ({count - 5} more)" : string.Empty);
            return $"The project failed validation with {count} error(s){preview}";
        }
    }
}
