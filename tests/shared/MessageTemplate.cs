using System.Collections.Generic;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// Reads a user-facing message template the way the renderer does.
    /// </summary>
    /// <remarks>
    /// Shared rather than copied because two gates hold templates against their declared argument slots — the SDK
    /// catalogue's in <c>safe_project_tests</c> and OpenVisual's <c>app.openvisual.*</c> family's in
    /// <c>safe_visual_tests</c> — and they have to agree on what a placeholder IS. With a copy each, a syntax
    /// change (an escape, say) applied to one leaves the other reading the old syntax, and the failure is a gate
    /// that passes while a template renders a visible <c>{placeholder}</c> to a user.
    /// </remarks>
    internal static class MessageTemplate
    {
        /// <summary>The <c>{slot}</c> names a template uses, in order of appearance.</summary>
        /// <param name="template">The Danish message template to read.</param>
        public static IReadOnlyList<string> Placeholders(string template)
        {
            List<string> names = [];
            for (int open = template.IndexOf('{'); open >= 0; open = template.IndexOf('{', open + 1))
            {
                int close = template.IndexOf('}', open);
                if (close > open + 1)
                {
                    names.Add(template[(open + 1)..close]);
                }
            }

            return names;
        }
    }
}
