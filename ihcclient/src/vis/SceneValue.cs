#nullable enable
using System;
using System.Collections.Immutable;
using System.Globalization;

using Ihc.Vis.Model;

namespace Ihc.Vis
{
    /// <summary>The three scene-member kinds a <see cref="SceneValue"/> can carry (derived from its member tag).</summary>
    public enum SceneValueKind
    {
        Relay,
        Dimmer,
        Shutter,
    }

    /// <summary>
    /// The typed value payload of a scene membership (US-024): the state the member output assumes when the
    /// scenario fires. Factory-constructed per member kind — <see cref="Relay"/> (<c>scene_relay</c>),
    /// <see cref="Dimmer"/> (<c>scene_dimmer</c>) or <see cref="Shutter"/> (<c>scene_shutter</c>) — and consumed
    /// by the scene-link editing operations, which derive the member row's element type and value attributes
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

        /// <summary>The scene-member kind this value carries.</summary>
        public SceneValueKind Kind => MemberTag switch
        {
            "scene_relay" => SceneValueKind.Relay,
            "scene_dimmer" => SceneValueKind.Dimmer,
            _ => SceneValueKind.Shutter,
        };

        /// <summary>A relay/socket member's on-state (<c>false</c> for any other kind).</summary>
        public bool On => Attr("relay_value") == "on";

        /// <summary>A dimmer member's target light level (0–100 %); 0 for any other kind or a malformed value.</summary>
        public int LevelPercent =>
            int.TryParse(Attr("dimming_value"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

        /// <summary>A dimmer member's ramp time; <see cref="TimeSpan.Zero"/> for any other kind or a malformed value.</summary>
        public TimeSpan RampTime =>
            TimeSpan.FromMilliseconds(long.TryParse(Attr("ramptime_ms"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ms) && ms >= 0 ? ms : 0);

        /// <summary>A shutter member's up-state (<c>true</c> unless explicitly <c>down</c>; <c>true</c> for other kinds).</summary>
        public bool ShutterUp => Attr("shutter_position") != "down";

        private string? Attr(string name)
        {
            foreach ((string Name, string Value) attr in Attributes)
            {
                if (attr.Name == name)
                    return attr.Value;
            }
            return null;
        }

        /// <summary>
        /// Reads the typed value of an existing scene member row (US-024/US-058), <b>non-throwing</b> and tolerant of
        /// malformed/absent values (matching the GUI's historic defaulting so previously-viewable projects still
        /// render): a missing/non-numeric <c>dimming_value</c>/<c>ramptime_ms</c> reads as 0, an absent
        /// <c>relay_value</c> as off, an absent <c>shutter_position</c> as up. Returns <c>false</c> only when the
        /// element is not a scene member.
        /// </summary>
        public static bool TryParse(ProjectElement member, out SceneValue value)
        {
            ArgumentNullException.ThrowIfNull(member);
            switch (member.Tag)
            {
                case "scene_relay":
                    value = Relay(member.GetAttribute("relay_value") == "on");
                    return true;
                case "scene_dimmer":
                    int level = int.TryParse(member.GetAttribute("dimming_value"),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out int lv) ? lv : 0;
                    long ms = long.TryParse(member.GetAttribute("ramptime_ms"),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out long m) && m >= 0 ? m : 0;
                    // Bypass the Dimmer factory's range validation — a tolerant read must never throw on a
                    // malformed/out-of-range stored value.
                    value = new("scene_dimmer", ImmutableArray.Create(
                        ("dimming_value", DecToken.Format(level)),
                        ("ramptime_ms", DecToken.Format(ms))));
                    return true;
                case "scene_shutter":
                    value = Shutter(member.GetAttribute("shutter_position") != "down");
                    return true;
                default:
                    value = null!;
                    return false;
            }
        }

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
