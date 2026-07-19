using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// fablerefac W3-8: the per-node-type <i>Properties</i> dialog flows, extracted from
/// <see cref="MainWindowViewModel"/> (C# 12 primary ctor). Each flow reads the element, opens its dialog through
/// <see cref="IDialogService"/>, and applies the result as a command via <paramref name="applyAndReport"/> — the
/// view-model's single outcome→status/dialog rule. The view-model keeps the node dispatch and calls these.
/// <para>Extracted in slices: this holds the locality / scene-container / scene-value / enum flows; the
/// product / pin / modem / dimmer cluster and the typed read views (which retire the dialog-DTO GetAttribute reads)
/// follow in later increments.</para>
/// </summary>
internal sealed class PropertiesDialogCoordinator(
    ProjectWorkflow session,
    IDialogService dialogs,
    Func<ProjectCommand, string, Task> applyAndReport)
{
    // Read element attributes through the SDK read surface; the element always belongs to the open project.
    private ElementView View(ProjectElement element) => session.Current!.View(element);

    /// <summary>Renames a locality or function block through the shared Name/Note dialog (US-007/US-019).</summary>
    public async Task OpenLocalityAsync(ElementId id, string currentName)
    {
        string currentNote = session.Current?.FindById(id)?.GetAttribute("note") ?? string.Empty;
        PropertiesResult? result = await dialogs.EditPropertiesAsync($"Edit {currentName} properties", currentName, currentNote);
        if (result is null)
            return;   // cancelled — the locality keeps its original name and note
        await applyAndReport(new RenameLocality(id, result.Name, result.Note), $"Renamed to {result.Name}.");
    }

    // The product's scene container (US-024): its fixed name, its note, and a row per membership naming the
    // scenario, the function block driving it and that block's locality — the same triple the membership's link row
    // shows as a path, split into columns.
    public async Task OpenSceneContainerAsync(ElementId scenesId, ProjectElement scenes)
    {
        var rows = new List<SceneContainerRow>();
        foreach (ProjectElement member in scenes.ChildrenOrEmpty())
        {
            if (!IsSceneMember(member.Tag))
                continue;
            IReadOnlyList<string> parts = LinkOppositeParts(member);
            (string value, string ramp) = SceneMemberValue(member);
            rows.Add(new SceneContainerRow(
                SceneName: parts.Count > 2 ? parts[2] : string.Empty,
                FunctionBlock: parts.Count > 1 ? parts[1] : string.Empty,
                Locality: parts.Count > 0 ? parts[0] : string.Empty,
                Value: value, RampTime: ramp));
        }
        string name = scenes.GetAttribute("name") ?? "Scenarier";
        SceneContainerResult? result = await dialogs.EditSceneContainerAsync(
            new SceneContainerInput(name, scenes.GetAttribute("note") ?? string.Empty, rows));
        if (result is null)
            return;
        await applyAndReport(new UpdateSceneContainer(scenesId, result.Note), $"'{name}' updated.");
    }

    public async Task OpenSceneValueAsync(ElementId memberId, ProjectElement member)
    {
        if (!SceneValue.TryParse(member, out SceneValue sv))
            return;
        bool isDimmer = sv.Kind == SceneValueKind.Dimmer;
        int ms = (int)sv.RampTime.TotalMilliseconds;
        var input = new SceneValueInput("Scene value", isDimmer, sv.On, sv.LevelPercent, ms / 60000, ms / 1000 % 60);

        SceneValueResult? result = await dialogs.EditSceneValueAsync(input);
        if (result is null)
            return;
        await applyAndReport(new UpdateSceneValue(memberId, result), "Scene value updated.");
    }

    public async Task OpenEnumAsync(ElementId enumVariableId)
    {
        if (ReadEnumInfo(enumVariableId) is not { } info)
            return;
        EnumDefinitionResult? result = await dialogs.EditEnumDefinitionAsync(
            new EnumDefinitionInput($"Edit {info.Name}", info.Name, info.States, IsNew: false));
        if (result is null)
            return;
        if (session.BuildUpdateEnumStates(enumVariableId, result.States) is { } command)
            await applyAndReport(command, $"Enumerator '{info.Name}' updated.");
    }

    // Reads an enum variable's type name and ordered state names for the Edit dialog (US-030); null if not an enum.
    private (string Name, List<string> States)? ReadEnumInfo(ElementId enumVariableId)
    {
        if (session.Current is not { } project || project.FindById(enumVariableId) is not { Tag: "resource_enum" } variable
            || !ElementId.TryParse(variable.GetAttribute("typedef"), out ElementId defId)
            || project.FindById(defId) is not { } def)
        {
            return null;
        }
        var states = def.ChildrenOrEmpty().Where(c => c.Tag == "enum_value")
            .Select(c => c.GetAttribute("name") ?? string.Empty).ToList();
        return (def.GetAttribute("name") ?? string.Empty, states);
    }

    // The value-carrying rows inside a product's scenes container — its memberships of the scenarios FBs drive.
    private static bool IsSceneMember(string tag) => tag is "scene_relay" or "scene_dimmer" or "scene_shutter";

    // A scene membership's stored value and, for a dimmer, its ramp time — the two columns the scene-container
    // dialog shows separately.
    private static (string Value, string RampTime) SceneMemberValue(ProjectElement member)
    {
        if (!SceneValue.TryParse(member, out SceneValue sv))
            return (string.Empty, string.Empty);
        return sv.Kind switch
        {
            SceneValueKind.Relay => (sv.On ? "ON" : "OFF", string.Empty),
            SceneValueKind.Dimmer => ($"{sv.LevelPercent}%", $"{sv.RampTime.TotalSeconds:0.#}s"),
            SceneValueKind.Shutter => (sv.ShutterUp ? "up" : "down", string.Empty),
            _ => (string.Empty, string.Empty),
        };
    }

    // A product's tree label carries its placement descriptor "name (position) " (F-003) — reproduced so the
    // scene-container dialog names a product exactly as the Installation pane does.
    private static string ProductLabel(string name, string? position) =>
        string.IsNullOrEmpty(position) ? name : $"{name} ({position}) ";

    // The opposite end's path parts, outermost first: [locality, product-or-block, pin]. Empty when unresolvable.
    private IReadOnlyList<string> LinkOppositeParts(ProjectElement linkRow)
    {
        if (session.Current is not { } project
            || !ElementId.TryParse(linkRow.GetAttribute("link"), out ElementId partnerId)
            || project.FindParent(partnerId) is not { } oppositePin)
        {
            return Array.Empty<string>();
        }
        var parts = new List<string>();
        ProjectElement? current = oppositePin;
        bool leaf = true;
        while (current is not null)
        {
            bool significant = leaf || current.Tag is "group" or "functionblock" || ProductClassifier.IsProduct(current.Tag);
            if (significant && View(current).Name is { Length: > 0 } partName)
                parts.Insert(0, ProductClassifier.IsProduct(current.Tag)
                    ? ProductLabel(partName, View(current).Position)
                    : partName);
            current = current.Id is { } cid ? project.FindParent(cid) : null;
            leaf = false;
        }
        return parts;
    }
}
