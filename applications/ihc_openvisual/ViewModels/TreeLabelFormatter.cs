using System;
using System.Collections.Generic;
using Ihc.Vis;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// T021: the shared, Avalonia-free presentation helpers the tree projector, the properties-dialog coordinator and
/// the reconciler render through, so a product's placement label, a scene member's value/ramp, and a link/scene
/// row's opposite-end path are each formatted in exactly ONE place. Previously <see cref="ProductLabel"/>,
/// <see cref="SceneMemberValue"/> and the opposite-end walk were copied verbatim across the projector and the
/// coordinator, and the same partner walk was open-coded a third time by the reconciler's dependency mirror.
/// </summary>
internal static class TreeLabelFormatter
{
    // A product's tree label carries its placement descriptor: "name (position) " — trailing space included — and
    // the bare name when position is absent (F-003). The trailing space is the vendor's and is reproduced so a
    // label-mode diff against IHC Visual stays exact.
    public static string ProductLabel(string name, string? position) =>
        string.IsNullOrEmpty(position) ? name : $"{name} ({position}) ";

    // A scene membership's stored value and, for a dimmer, its ramp time — the two columns the scene-container
    // dialog shows separately; empty for an unparseable / other-kind member.
    public static (string Value, string RampTime) SceneMemberValue(ProjectElement member)
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

    // The chain of elements from a link/scene row's OPPOSITE pin up to the root, innermost first: the partner pin,
    // then each of its ancestors. Empty when the row's `link` is unresolvable. This is the one partner walk that
    // the opposite-end label parts (names) and the reconciler's dependency edges (ids) both project from.
    public static IReadOnlyList<ProjectElement> LinkPartnerChain(Project project, ProjectElement linkRow)
    {
        if (!ElementId.TryParse(project.View(linkRow).Effective("link"), out ElementId partnerId)
            || project.FindParent(partnerId) is not { } oppositePin)
        {
            return Array.Empty<ProjectElement>();
        }
        var chain = new List<ProjectElement>();
        for (ProjectElement? current = oppositePin; current is not null;
             current = current.Id is { } cid ? project.FindParent(cid) : null)
        {
            chain.Add(current);
        }
        return chain;
    }

    // The opposite end's path parts, outermost first: [locality, product-or-block, pin]. Empty when unresolvable. A
    // product part carries its ProductLabel placement descriptor; any other significant ancestor its bare name.
    public static IReadOnlyList<string> LinkOppositeParts(Project project, ProjectElement linkRow)
    {
        var parts = new List<string>();
        bool leaf = true;
        foreach (ProjectElement current in LinkPartnerChain(project, linkRow))
        {
            bool significant = leaf || current.IsLocalityGroup || current.Kind is ElementKind.FunctionBlock || ProductClassifier.IsProduct(current.Tag);
            if (significant && project.View(current).Name is { Length: > 0 } partName)
                parts.Insert(0, ProductClassifier.IsProduct(current.Tag)
                    ? ProductLabel(partName, project.View(current).Position)
                    : partName);
            leaf = false;
        }
        return parts;
    }

    // The opposite end rendered as a single "a / b / c" path, or "(unresolved)" when nothing resolves.
    public static string LinkOppositePath(Project project, ProjectElement linkRow) =>
        LinkOppositeParts(project, linkRow) is { Count: > 0 } parts ? string.Join(" / ", parts) : "(unresolved)";
}
