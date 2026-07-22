#nullable enable
using System;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>The edited scenario-link value (US-024/US-058): ON/OFF for a relay, or light-level % + ramp for a
    /// dimmer. An edit payload for the scene commands (moved down from the GUI dialog, W2-7).</summary>
    public sealed record SceneValueResult(bool On, int LevelPercent, int RampMinutes, int RampSeconds);

    /// <summary>Creates a follow-link from a source pin to a target pin (US-022/US-023): the source drives, the target
    /// is driven. Legality is the vendor data-flow rule (<see cref="ProjectEditor.CanLink"/>), which also refuses a
    /// pin-to-itself link (D06); the explicit <c>Source != Target</c> guard is kept as a belt-and-suspenders check.</summary>
    public sealed record LinkPins(ElementId Source, ElementId Target) : ProjectCommand
    {
        internal override string Describe(Project project) => "Link";
        internal override EditVerdict Evaluate(EditContext context) =>
            Source != Target && context.Project.Edit().CanLink(Source, Target)
                ? EditVerdict.Allow
                : EditVerdict.Refuse("These pins cannot be linked in this direction.");
        internal override void Execute(ProjectEditor editor) => editor.Link(Source, Target);
    }

    /// <summary>Removes a link by one of its rows (US-057), cascading the reciprocal half.</summary>
    public sealed record RemoveLink(ElementId LinkRowId) : ProjectCommand
    {
        internal override string Describe(Project project) => "Remove link";
        internal override EditVerdict Evaluate(EditContext context) => context.RequireExists(LinkRowId, "link");
        internal override void Execute(ProjectEditor editor) => editor.DeleteById(LinkRowId);
    }

    /// <summary>Creates a scenario link (US-024) from a function-block scene output pin to a product's scenes
    /// container, with the given value.</summary>
    public sealed record LinkScene(ElementId SceneOutputId, ElementId ScenesId, SceneValueResult Result, bool IsDimmer)
        : ProjectCommand
    {
        internal override string Describe(Project project) => "Link scenario";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(SceneOutputId) is not null && context.Index.FindById(ScenesId) is not null
                ? EditVerdict.Allow : EditVerdict.Refuse("A scene endpoint no longer exists.");
        internal override void Execute(ProjectEditor editor) =>
            editor.LinkScene(SceneOutputId, ScenesId, SceneValues.From(Result, IsDimmer));
    }

    /// <summary>Edits an existing scenario link's stored value in place (US-058); the member kind is derived from the
    /// row's tag so id/name/link/note are preserved.</summary>
    public sealed record UpdateSceneValue(ElementId MemberId, SceneValueResult Result) : ProjectCommand
    {
        internal override string Describe(Project project) => "Edit scene value";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireTag(MemberId, "a relay or dimmer scene member", "scene_dimmer", "scene_relay");
        internal override void Execute(ProjectEditor editor)
        {
            ElementRef handle = editor.Resolve(MemberId, "scene member");
            editor.SetSceneValue(MemberId, SceneValues.From(Result, handle.Tag == "scene_dimmer"));
        }
    }

    /// <summary>Edits a scenes container's note (US-024).</summary>
    public sealed record UpdateSceneContainer(ElementId ScenesId, string Note) : ProjectCommand
    {
        internal override string Describe(Project project) => "Edit scenario container";
        internal override EditVerdict Evaluate(EditContext context) => context.RequireExists(ScenesId, "scenes container");
        internal override void Execute(ProjectEditor editor) =>
            editor.Resolve(ScenesId, "scenes container").SetAttribute("note", Note);
    }

    internal static class SceneValues
    {
        public static SceneValue From(SceneValueResult r, bool isDimmer) =>
            isDimmer
                ? SceneValue.Dimmer(r.LevelPercent, TimeSpan.FromSeconds((r.RampMinutes * 60) + r.RampSeconds))
                : SceneValue.Relay(r.On);
    }
}
