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
    // Reports whether the command COMMITTED — the two-step gesture needs the answer: a source pin that was armed
    // and then refused must stay armed, or the installer has to re-run "Link from here" before every retry.
    Func<ProjectCommand, string, Task<bool>> applyAndReport,
    Action<string> setStatus,
    Func<TreeNodeViewModel?> getPendingSource,
    Action<TreeNodeViewModel?> setPendingSource,
    Action<ElementId> revealOpposite)
{
    /// <summary>Links two pins (US-022/US-023): the <paramref name="source"/> pin is linked onto the
    /// <paramref name="target"/> pin (the target gets the "link from" half). Both must be pins.</summary>
    public Task LinkPinsAsync(TreeNodeViewModel? source, TreeNodeViewModel? target) =>
        runAsync("LinkPins", () => TryLinkPinsAsync(source, target));

    // The same link, reporting whether it was actually created. Not wrapped in runAsync: LinkToHereAsync already
    // runs inside one, and the two-step gesture needs this answer to decide whether the armed source is consumed.
    private async Task<bool> TryLinkPinsAsync(TreeNodeViewModel? source, TreeNodeViewModel? target)
    {
        bool linked = false;
        if (source?.ElementId is { } fromId && target?.ElementId is { } toId
            && source.IsPin && target.IsPin && session.Current is { } project)
        {
            linked = await applyAndReport(session.Commands.LinkPins(project, fromId, toId),
                $"Linkede {source.DisplayName} til {target.DisplayName}.");
        }
        return linked;
    }

    /// <summary>Arms a link from the given pin (US-022) — the next <i>Link to here</i> completes it.</summary>
    public void StartLink(TreeNodeViewModel? node)
    {
        if (node is { IsPin: true })
        {
            setPendingSource(node);
            setStatus($"Linker fra {node.DisplayName} — vælg 'Link til her' på den anden klemme.");
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
                setStatus("Vælg først 'Link fra her' på kilde-klemmen.");
                return;
            }
            // The armed source is consumed by a link that was actually CREATED, never merely attempted: clearing it
            // up front meant a refused pairing (or a cancelled scene-value dialog) silently disarmed the gesture, so
            // the installer had to walk back to the source pin and re-arm before every retry.
            bool linked = node.IsSceneTarget && source.ElementId is { } srcId && node.ElementId is { } scenesId
                          && session.Current?.FindById(srcId)?.IsSceneResource == true
                ? await CompleteSceneLinkAsync(srcId, scenesId)
                : await TryLinkPinsAsync(source, node);
            if (linked)
            {
                setPendingSource(null);
            }
        });

    /// <summary>Navigates from a link row to the pin at the opposite end of the link (US-025, F4) — the reveal
    /// callback selects it in whichever pane holds it.</summary>
    public void NavigateLinkOpposite(TreeNodeViewModel? node)
    {
        if (node is not { IsLinkRow: true } || node.ElementId is not { } linkId || session.Current is not { } project
            || project.FindById(linkId) is not { } linkRow
            || !ElementId.TryParse(project.View(linkRow).Effective("link"), out ElementId partnerId)
            || project.FindById(partnerId) is null)
        {
            return;
        }
        // The other HALF of the wire, not the pin that owns it (uxparity S-25): the vendor leaves the caret on a
        // link row, which is itself F4-able and Delete-able, so the wire stays the thing being worked with.
        revealOpposite(partnerId);
    }

    // Returns whether the scenario link was created (a cancelled value dialog leaves the source armed to retry).
    private async Task<bool> CompleteSceneLinkAsync(ElementId sceneOutputId, ElementId scenesId)
    {
        if (session.Current is not { } project || project.FindById(scenesId) is null)
            return false;
        // The scene value variant (sliver #11) is the SDK's decision — used to shape the dialog and stamp the command.
        bool isDimmer = session.Commands.IsSceneWirelessDimming(project, scenesId);
        // F-49: titled by the MEMBER's type, exactly as the edit-time dialog is — the original raises
        // "Relæ scenarie egenskaber" / "Lysdæmper scenarie egenskaber" at LINK time too (measured live
        // 2026-08-11: linking a block's scene pin to a Lampeudtag opened the relay caption). Two call sites
        // raise this dialog and only one had been corrected; a shared helper now keeps them from drifting.
        var input = new SceneValueInput(SceneValueTitles.For(isDimmer), isDimmer, On: true,
            LevelPercent: isDimmer ? 100 : 0, RampMinutes: 0, RampSeconds: 0,
            Level: SceneValue.LevelConstraint, RampPart: SceneValue.RampPartConstraint);

        SceneValueResult? result = await dialogs.EditSceneValueAsync(input);
        return result is not null
            && await applyAndReport(session.Commands.LinkScene(project, sceneOutputId, scenesId, result), "Scenarie link oprettet.");
    }
}
