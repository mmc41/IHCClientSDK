#nullable enable

namespace Ihc.Vis.Schema
{
    /// <summary>
    /// The scene-capable output families and their pinned member kinds (US-024, spec ch. 08 §8.4): relays/sockets
    /// take <c>scene_relay</c>, dimmer regulation takes <c>scene_dimmer</c>. A peer of <see cref="ReciprocalTags"/>
    /// — one table for every consumer (today the editor's LinkScene guard; a validator kind-rule or a GUI's
    /// member-insert menu reads the same fact). Unknown families answer <c>null</c> — the open-world CanInsert
    /// convention: only a known mismatch is a hard error.
    /// </summary>
    internal static class SceneRules
    {
        /// <summary>The member tag the bound output family pins, or <c>null</c> for unknown (permissive) families.</summary>
        public static string? PinnedMemberTagFor(string boundOutputTag) => boundOutputTag switch
        {
            "dataline_output" or "airlink_relay" => "scene_relay",
            "airlink_dimming" => "scene_dimmer",
            _ => null,
        };
    }
}
