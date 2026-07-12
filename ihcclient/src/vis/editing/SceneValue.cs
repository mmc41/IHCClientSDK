#nullable enable
using System;
using System.Collections.Immutable;

using Ihc.Vis.Model;

namespace Ihc.Vis.Editing
{
    /// <summary>
    /// The typed value payload of a scene membership (US-024): the state the member output assumes when the
    /// scenario fires. Factory-constructed per member kind — <see cref="Relay"/> (<c>scene_relay</c>),
    /// <see cref="Dimmer"/> (<c>scene_dimmer</c>) or <see cref="Shutter"/> (<c>scene_shutter</c>) — and consumed
    /// by <see cref="ProjectEditor.LinkScene"/>, which derives the member row's element type and value attributes
    /// from it. Values equal to the DTD defaults (relay off, dimmer 0 % / 0 ms, shutter up) are elided on save by
    /// the canonicalizer's omit-if-default rule, exactly as the vendor writes them.
    /// </summary>
    public sealed class SceneValue
    {
        private SceneValue(string memberTag, ImmutableArray<(string Name, string Value)> attributes)
        {
            MemberTag = memberTag;
            Attributes = attributes;
        }

        /// <summary>The member row's element type (<c>scene_relay</c> / <c>scene_dimmer</c> / <c>scene_shutter</c>).</summary>
        internal string MemberTag { get; }

        /// <summary>The value attributes the member row carries, in DTD attribute order.</summary>
        internal ImmutableArray<(string Name, string Value)> Attributes { get; }

        /// <summary>A relay/socket member (<c>scene_relay</c>): the output switches <paramref name="on"/> or off.</summary>
        public static SceneValue Relay(bool on) =>
            new("scene_relay", ImmutableArray.Create(("relay_value", on ? "on" : "off")));

        /// <summary>
        /// A dimmer member (<c>scene_dimmer</c>): the output ramps to <paramref name="levelPercent"/> (0–100 %)
        /// over <paramref name="rampTime"/> (whole milliseconds, non-negative).
        /// </summary>
        public static SceneValue Dimmer(int levelPercent, TimeSpan rampTime)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(levelPercent, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(levelPercent, 100);
            ArgumentOutOfRangeException.ThrowIfNegative(rampTime.Ticks, nameof(rampTime));
            return new("scene_dimmer", ImmutableArray.Create(
                ("dimming_value", DecToken.Format(levelPercent)),
                ("ramptime_ms", DecToken.Format((long)rampTime.TotalMilliseconds))));
        }

        /// <summary>
        /// A shutter/jalousi member (<c>scene_shutter</c>): the shutter runs <paramref name="up"/> or down.
        /// Provisional — the grammar is spec-derived (ch. 08 §8.4.1); no vendor oracle exercises it yet.
        /// </summary>
        public static SceneValue Shutter(bool up) =>
            new("scene_shutter", ImmutableArray.Create(("shutter_position", up ? "up" : "down")));
    }
}
