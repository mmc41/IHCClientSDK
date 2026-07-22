#nullable enable
using System.Globalization;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>The kind of an ordinary FB resource variable's editable initial value (US-027, T016) — it picks both
    /// the dialog control and the <c>.vis</c> serialization. <see cref="None"/> means the variable exposes no editable
    /// initial value (Name/Note only).</summary>
    public enum ResourceValueKind
    {
        None,
        Bool,
        Number,
        Time,
    }

    /// <summary>A typed initial value for an ordinary FB resource variable (US-027, T016). One flat payload carries
    /// every representation; <see cref="Kind"/> selects which fields are live, and the factories keep call sites from
    /// having to know the layout. An edit payload moved down from the GUI dialog.</summary>
    public sealed record ResourceInitialValue(
        ResourceValueKind Kind, bool Bool, long Number, int Hour, int Minute, int Second, int Millisecond)
    {
        /// <summary>The "no editable initial value" sentinel (weekday/date/…): the write is a no-op.</summary>
        public static ResourceInitialValue None { get; } = new(ResourceValueKind.None, false, 0, 0, 0, 0, 0);

        /// <summary>A boolean initial value (resource_flag/input/output): serialises as <c>inivalue</c> on/off.</summary>
        public static ResourceInitialValue OfBool(bool on) => new(ResourceValueKind.Bool, on, 0, 0, 0, 0, 0);

        /// <summary>A numeric initial value (resource_counter/integer): serialises as a decimal <c>inivalue</c>.</summary>
        public static ResourceInitialValue OfNumber(long number) => new(ResourceValueKind.Number, false, number, 0, 0, 0, 0);

        /// <summary>A time/duration initial value (resource_timer/time): serialises as hour/minute/second, plus
        /// millisecond for a resource_timer.</summary>
        public static ResourceInitialValue OfTime(int hour, int minute, int second, int millisecond) =>
            new(ResourceValueKind.Time, false, 0, hour, minute, second, millisecond);

        /// <summary>Writes this value onto a resource variable handle per representation (US-027, T016): a bool writes
        /// <c>inivalue</c> on/off, a number a decimal <c>inivalue</c>, and a time writes <c>hour</c>/<c>minute</c>/
        /// <c>second</c> — plus <c>millisecond</c> for a <c>resource_timer</c> (a <c>resource_time</c> carries none).
        /// <see cref="ResourceValueKind.None"/> writes nothing. Shared by the value-only and combined commands.</summary>
        internal void WriteTo(ElementRef handle)
        {
            switch (Kind)
            {
                case ResourceValueKind.Bool:
                    handle.SetAttribute("inivalue", Bool ? "on" : "off");
                    break;
                case ResourceValueKind.Number:
                    handle.SetAttribute("inivalue", Number.ToString(CultureInfo.InvariantCulture));
                    break;
                case ResourceValueKind.Time:
                    handle.SetAttribute("hour", Dec(Hour)).SetAttribute("minute", Dec(Minute)).SetAttribute("second", Dec(Second));
                    if (handle.Tag == "resource_timer")
                    {
                        handle.SetAttribute("millisecond", Dec(Millisecond));
                    }
                    break;
            }
        }

        private static string Dec(int value) => value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Sets an ordinary FB resource variable's typed initial value (US-027, T016), refused inside a locked
    /// block by the T003 central predicate. Serialization lives in <see cref="ResourceInitialValue.WriteTo"/>.</summary>
    public sealed record SetResourceInitialValue(ElementId Id, ResourceInitialValue Value) : ProjectCommand
    {
        internal override string Describe(Project project) => "Set initial value";

        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireExists(Id, "variable")
                .And(context.RequireUnlockedTarget(Id, inclusive: true));   // T003: no edit inside a locked block

        internal override void Execute(ProjectEditor editor) => Value.WriteTo(editor.Resolve(Id, "variable"));
    }

    /// <summary>Edits an ordinary FB resource variable's Name, Note, and typed initial value in ONE undoable step
    /// (US-026/US-027, T016) — the whole Properties dialog applied atomically, refused inside a locked block by T003.
    /// A <see cref="ResourceValueKind.None"/> value leaves the initial value untouched.</summary>
    public sealed record SetVariableProperties(ElementId Id, string Name, string Note, ResourceInitialValue Value) : ProjectCommand
    {
        internal override string Describe(Project project) => "Edit " + Name;

        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireExists(Id, "variable")
                .And(context.RequireUnlockedTarget(Id, inclusive: true));   // T003

        internal override void Execute(ProjectEditor editor)
        {
            ElementRef handle = editor.Resolve(Id, "variable");
            handle.SetAttribute("name", Name).SetAttribute("note", Note);
            Value.WriteTo(handle);
        }
    }
}
