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

    /// <summary>The NAME of the state an enum variable currently starts in: a <c>resource_enum</c>'s
    /// <c>inivalue</c> is an IDREF to one of its type's <c>enum_value</c> elements, so the label only exists after
    /// following it (F-004/F-50). Null for any other type, an unresolvable reference, or an unnamed state.
    /// <para>Shared deliberately: the tree ROW renders this value and the variable DIALOG pre-selects it, and two
    /// copies of the follow-the-IDREF rule are two chances for the row and the editor to disagree about which
    /// state a variable is in.</para></summary>
    public static string? EnumStateName(this Project project, ProjectElement variable) =>
        variable.Kind == ElementKind.EnumResource
        && ElementId.TryParse(project.View(variable).Effective("inivalue"), out ElementId valueId)
        && project.FindById(valueId) is { } state
        && project.View(state).Name is { Length: > 0 } name
            ? name
            : null;
}
