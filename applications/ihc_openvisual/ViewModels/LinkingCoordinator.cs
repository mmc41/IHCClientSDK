using System;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// T017 (M6): the pin/scene linking engine extracted from <see cref="MainWindowViewModel"/> — the two-step
/// "Link from here"/"Link to here" gesture (US-022/US-023), the scenario-link value flow (US-024) and the F4
/// jump to a link's opposite end (US-025). Mirrors <see cref="ProgramAuthoringCoordinator"/>'s delegate-ctor
/// shape: it owns no Avalonia types and reaches the view-model only through the passed delegates
/// (apply/run/status), the pending-source getter/setter (which stays an <c>[ObservableProperty]</c> on the
/// view-model, so it is XAML-bound) and a reveal callback for the tree-navigation view-state — so it is headlessly
/// testable. The view-model keeps the thin entry points (<c>LinkPins</c> plus the <c>[RelayCommand]</c>s),
/// delegating their bodies here.
/// </summary>
internal sealed class LinkingCoordinator(
    ProjectWorkflow session,
    IDialogService dialogs,
    Func<string, Func<Task>, Task> runAsync,
    Func<ProjectCommand, string, Task> applyAndReport,
    Action<string> setStatus,
    Func<TreeNodeViewModel?> getPendingSource,
    Action<TreeNodeViewModel?> setPendingSource,
    Action<ElementId> revealOpposite)
{
    /// <summary>Links two pins (US-022/US-023): the <paramref name="source"/> pin is linked onto the
    /// <paramref name="target"/> pin (the target gets the "link from" half). Both must be pins.</summary>
    public Task LinkPinsAsync(TreeNodeViewModel? source, TreeNodeViewModel? target) =>
        runAsync("LinkPins", async () =>
        {
            if (source?.ElementId is not { } fromId || target?.ElementId is not { } toId
                || !source.IsPin || !target.IsPin || session.Current is not { } project)
                return;
            await applyAndReport(session.Commands.LinkPins(project, fromId, toId), $"Linked {source.DisplayName} to {target.DisplayName}.");
        });

    /// <summary>Arms a link from the given pin (US-022) — the next <i>Link to here</i> completes it.</summary>
    public void StartLink(TreeNodeViewModel? node)
    {
        if (node is { IsPin: true })
        {
            setPendingSource(node);
            setStatus($"Linking from {node.DisplayName} — choose 'Link to here' on the other pin.");
        }
    }

    /// <summary>Completes a link onto the given pin or scenes container (US-022/US-024), pairing it with the armed
    /// pending source. A scene output onto a scenes container makes a scenario link (opens the value dialog);
    /// otherwise a follow-link between two pins.</summary>
    public Task LinkToHereAsync(TreeNodeViewModel? node) =>
        runAsync("LinkToHere", async () =>
        {
            if (node is not { } || (!node.IsPin && !node.IsSceneTarget))
                return;
            if (getPendingSource() is not { } source || ReferenceEquals(source, node))
            {
                setStatus("Choose 'Link from here' on the source pin first.");
                return;
            }
            setPendingSource(null);

            if (node.IsSceneTarget && source.ElementId is { } srcId && node.ElementId is { } scenesId
                && session.Current?.FindById(srcId)?.IsSceneResource == true)
            {
                await CompleteSceneLinkAsync(srcId, scenesId);
                return;
            }
            await LinkPinsAsync(source, node);
        });

    /// <summary>Navigates from a link row to the pin at the opposite end of the link (US-025, F4) — the reveal
    /// callback selects it in whichever pane holds it.</summary>
    public void NavigateLinkOpposite(TreeNodeViewModel? node)
    {
        if (node is not { IsLinkRow: true } || node.ElementId is not { } linkId || session.Current is not { } project
            || project.FindById(linkId) is not { } linkRow
            || !ElementId.TryParse(project.View(linkRow).Effective("link"), out ElementId partnerId)
            || project.FindParent(partnerId) is not { Id: { } oppositeId })
        {
            return;
        }
        revealOpposite(oppositeId);
    }

    private async Task CompleteSceneLinkAsync(ElementId sceneOutputId, ElementId scenesId)
    {
        if (session.Current is not { } project || project.FindById(scenesId) is null)
            return;
        // The scene value variant (sliver #11) is the SDK's decision — used to shape the dialog and stamp the command.
        bool isDimmer = session.Commands.IsSceneWirelessDimming(project, scenesId);
        var input = new SceneValueInput("Scene value", isDimmer, On: true, LevelPercent: isDimmer ? 100 : 0, RampMinutes: 0, RampSeconds: 0);

        SceneValueResult? result = await dialogs.EditSceneValueAsync(input);
        if (result is null)
            return;
        await applyAndReport(session.Commands.LinkScene(project, sceneOutputId, scenesId, result), "Scene link created.");
    }
}
