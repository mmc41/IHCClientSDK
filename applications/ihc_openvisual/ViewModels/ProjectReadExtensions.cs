using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// T022: the one shared read-surface convenience the tree/label/properties projections use, replacing the per-class
/// <c>NameOr</c> copies (and the coordinator ctor delegates that threaded one instance around). The raw element view
/// itself is the SDK's <c>project.View(element)</c> (Ihc.Vis) — this only adds the name-or-fallback projection over it.
/// </summary>
internal static class ProjectReadExtensions
{
    /// <summary>The element's effective name, or <paramref name="fallback"/> when it is empty — preserving the old
    /// <c>GetAttribute("name") ?? fallback</c> (a canonicalized project omits an empty name, so it reads back as "").</summary>
    public static string NameOr(this Project project, ProjectElement element, string fallback) =>
        project.View(element).Name is { Length: > 0 } name ? name : fallback;
}
