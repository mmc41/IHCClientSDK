#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ihc.Projects
{
    /// <summary>
    /// Debug-tier interior observation points: the cheap core invariants every edit session guarantees —
    /// unique id tokens/counters and <c>last_unique_id</c> at or above the highest counter — asserted at the
    /// session-commit boundary, so a corrupting bug is blamed at the mutation batch that introduced it rather
    /// than surfacing as a validation error long after. Reciprocity of link/scene halves is deliberately NOT a
    /// session invariant (<see cref="LinkCopyPolicy.KeepExternal"/> legitimately produces one-sided halves for
    /// the validator to flag), and nothing asserts on the passive load→save path: the open-world contract keeps
    /// quirky-but-loadable files byte-exact round-trippable. The always-on guards live where consequences are
    /// irreversible (<c>Edit()</c> entry, <c>UploadTo</c>, the opt-in
    /// <see cref="ProjectSaveOptions.ValidateBeforeSave"/>/<see cref="ProjectSaveOptions.VerifyRoundTrip"/>).
    /// All checks are pure reads over the immutable tree.
    /// </summary>
    internal static class ProjectContracts
    {
        [Conditional("DEBUG")]
        public static void AssertCore(Project project, string stage)
        {
            string? why = CoreViolation(project);
            Debug.Assert(why is null, $"[{stage}] project core invariant violated: {why}");
        }

        internal static string? CoreViolation(Project project)
        {
            var tokens = new HashSet<string>(StringComparer.Ordinal);
            var counters = new HashSet<int>();
            long maxCounter = 0;
            string? violation = null;
            void Walk(ProjectElement element)
            {
                if (violation is not null)
                {
                    return;
                }
                if (element.GetAttribute("id") is { } token)
                {
                    if (!tokens.Add(token))
                    {
                        violation = $"duplicate id token '{token}'";
                        return;
                    }
                    if (ElementId.TryParse(token, out ElementId id))
                    {
                        if (!counters.Add(id.Counter))
                        {
                            violation = $"duplicate id counter in '{token}'";
                            return;
                        }
                        if (id.Counter > maxCounter)
                        {
                            maxCounter = id.Counter;
                        }
                    }
                }
                if (!element.Children.IsDefaultOrEmpty)
                {
                    foreach (ProjectElement child in element.Children)
                    {
                        Walk(child);
                    }
                }
            }
            Walk(project.Root);
            if (violation is null
                && HexToken.ParseValueOrDefault(project.LastUniqueId, long.MaxValue) < maxCounter)
            {
                violation = $"last_unique_id '{project.LastUniqueId}' is below the highest counter (0x{maxCounter:x})";
            }
            return violation;
        }
    }
}
