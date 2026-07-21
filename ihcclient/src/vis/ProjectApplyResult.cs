#nullable enable
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace Ihc.Vis
{
    /// <summary>
    /// The result of running a command through <see cref="ProjectAppService.Apply(Project, ProjectCommand)"/>: the
    /// resulting immutable <see cref="Project"/> snapshot paired with the command's <see cref="EditOutcome"/>. The
    /// facade runs each command on a throwaway document session and returns the snapshot here because
    /// <see cref="EditOutcome"/> deliberately carries only the status / label / change-set, never the project.
    /// <para>
    /// <b>Project-snapshot contract (D03):</b> on an <see cref="EditStatus.Committed"/> outcome <see cref="Project"/>
    /// is the CHANGED project (the edit is visible in it); on any non-committing outcome
    /// (<see cref="EditStatus.NoChange"/> / <see cref="EditStatus.Refused"/> / <see cref="EditStatus.Failed"/>) it is
    /// the ORIGINAL input project — reference-identical and never null. A caller therefore always has a valid
    /// snapshot and commits only when <see cref="Outcome"/>'s status is <see cref="EditStatus.Committed"/>.
    /// </para>
    /// </summary>
    public sealed record ProjectApplyResult(Project Project, EditOutcome Outcome);

    /// <summary>
    /// The value-producing peer of <see cref="ProjectApplyResult"/> for
    /// <see cref="ProjectAppService.Apply{T}(Project, ProjectCommand{T})"/>: the resulting <see cref="Project"/>
    /// snapshot (same D03 contract) paired with the typed <see cref="EditOutcome{T}"/>, whose
    /// <see cref="EditOutcome{T}.Value"/> preserves the command-produced value on a committed outcome (and is
    /// <c>default</c> otherwise).
    /// </summary>
    public sealed record ProjectApplyResult<T>(Project Project, EditOutcome<T> Outcome);
}
