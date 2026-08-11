#nullable enable
using System;
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

        /// <summary>One of a fixed set of enumerated TOKENS — a <c>resource_weekday</c>'s
        /// <c>monday…sunday</c>. Distinct from <see cref="Bool"/> and <see cref="Number"/> because the file
        /// stores the token itself, and the labels a user picks from are the application's, not the format's.</summary>
        Choice,

        /// <summary>A calendar day and month — a <c>resource_date</c>. The type also stores a <c>year</c>, which
        /// the original's dialog does not offer and this value therefore never writes.</summary>
        Date,

        /// <summary>A real number stored with exactly two fraction digits and a period separator — the
        /// <c>kW</c>/<c>kWh</c>/<c>W</c>/<c>Wh</c>/<c>resource_floating_point</c>/<c>resource_temperature</c>/
        /// <c>resource_humidity_level</c> family, every one of which declares <c>inivalue CDATA "0.00"</c>.
        /// Distinct from <see cref="Number"/>, whose types declare <c>"0"</c> and store a bare integer: the two
        /// cannot share a writer, because <c>43</c> and <c>43.00</c> are different bytes (F-44).</summary>
        Decimal,
    }

    /// <summary>A typed initial value for an ordinary FB resource variable (US-027, T016). One flat payload carries
    /// every representation; <see cref="Kind"/> selects which fields are live, and the factories keep call sites from
    /// having to know the layout. An edit payload moved down from the GUI dialog.</summary>
    public sealed record ResourceInitialValue(
        ResourceValueKind Kind, bool Bool, long Number, int Hour, int Minute, int Second, int Millisecond,
        string Token = "", int Day = 0, int Month = 0, double Decimal = 0)
    {
        /// <summary>The "no editable initial value" sentinel (date/decimal types/…): the write is a no-op.</summary>
        public static ResourceInitialValue None { get; } = new(ResourceValueKind.None, false, 0, 0, 0, 0, 0);

        /// <summary>An enumerated initial value (resource_weekday's <c>monday…sunday</c>): serialises as the
        /// <c>inivalue</c> token verbatim, so the file never depends on how a label is spelled (F-41).</summary>
        public static ResourceInitialValue OfChoice(string token) =>
            new(ResourceValueKind.Choice, false, 0, 0, 0, 0, 0, token);

        /// <summary>A calendar day/month initial value (resource_date): serialises as <c>day</c> and
        /// <c>month</c>. The type's <c>year</c> is deliberately NOT written — the original's dialog offers day
        /// and month only, so an edit must leave the stored year exactly as it found it (F-41).</summary>
        public static ResourceInitialValue OfDate(int day, int month) =>
            new(ResourceValueKind.Date, false, 0, 0, 0, 0, 0, string.Empty, day, month);

        /// <summary>A boolean initial value (resource_flag/input/output): serialises as <c>inivalue</c> on/off.</summary>
        public static ResourceInitialValue OfBool(bool on) => new(ResourceValueKind.Bool, on, 0, 0, 0, 0, 0);

        /// <summary>An integer initial value (resource_counter/integer/light/light_level — the types whose
        /// declared default is <c>"0"</c>): serialises as a bare integer <c>inivalue</c>.</summary>
        public static ResourceInitialValue OfNumber(long number) => new(ResourceValueKind.Number, false, number, 0, 0, 0, 0);

        /// <summary>A real initial value for the two-decimal family (kW/kWh/W/Wh/floating-point/temperature/
        /// humidity): serialises as <c>inivalue</c> with exactly two fraction digits and a period, whatever
        /// precision the type shows on screen and whatever culture the machine runs in (F-41/F-44).</summary>
        public static ResourceInitialValue OfDecimal(double value) =>
            new(ResourceValueKind.Decimal, false, 0, 0, 0, 0, 0, string.Empty, 0, 0, value);

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
                case ResourceValueKind.Decimal:
                    handle.SetAttribute("inivalue", TwoDecimals(Decimal));
                    break;
                case ResourceValueKind.Choice:
                    handle.SetAttribute("inivalue", Token);
                    break;
                case ResourceValueKind.Date:
                    // day and month only: `year` is #REQUIRED and already present, and the original's dialog does
                    // not offer it — rewriting it would change a byte the installer never touched.
                    handle.SetAttribute("day", Dec(Day)).SetAttribute("month", Dec(Month));
                    break;
                case ResourceValueKind.Time:
                    handle.SetAttribute("hour", Dec(Hour)).SetAttribute("minute", Dec(Minute)).SetAttribute("second", Dec(Second));
                    // Milliseconds belong to the types that DECLARE them: the DTD gives both resource_timer and
                    // resource_timertime hour/minute/second/millisecond #REQUIRED, while resource_time has no
                    // millisecond at all — and the reference application's dialogs agree, showing 00:00:00,000 for
                    // the first two and 00.00.00 for the third (measured 2026-08-11, alignment F-41). Keying this
                    // to resource_timer alone silently dropped a Timertid's millisecond, writing three of its four
                    // required fields.
                    if (handle.Tag is "resource_timer" or "resource_timertime")
                    {
                        handle.SetAttribute("millisecond", Dec(Millisecond));
                    }
                    break;
            }
        }

        private static string Dec(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// The two-fraction-digit, period-separated text the decimal family stores (F-41/F-44). Two fraction digits
        /// and a period ALWAYS, whatever precision the type displays and whatever culture the machine runs in: kW
        /// shows three decimals on screen but stores <c>1.55</c>, Fugtighed shows one but stores <c>55.50</c>, and W
        /// stores <c>43.00</c> rather than <c>43</c>.
        /// <para>
        /// Rounding is the reference application's, which is C's <c>printf("%.2f")</c>: it rounds the value's EXACT
        /// binary expansion, with a true tie going away from zero. Both halves were measured, and each rules out an
        /// obvious shortcut — typing <c>1,125</c> saved <c>1.13</c> (away from zero, not the to-even <c>1.12</c>),
        /// while <c>1,555</c> saved <c>1.55</c>, because that literal is really 1.55499999…, below the midpoint.
        /// .NET's own <c>ToString("0.00")</c> gets the first right and the second wrong (<c>1.56</c>): it rounds the
        /// shortest decimal that round-trips rather than the stored binary value. Going through the 17-digit
        /// expansion restores the exact value before rounding, which reproduces both.
        /// </para>
        /// </summary>
        private static string TwoDecimals(double value) =>
            decimal.TryParse(value.ToString("G17", CultureInfo.InvariantCulture), NumberStyles.Float,
                CultureInfo.InvariantCulture, out decimal exact)
                ? Math.Round(exact, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture)
                // Only reachable for a magnitude beyond decimal's range, which no declared type's range allows;
                // formatting the double directly still yields well-formed two-decimal text.
                : value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>Sets an ordinary FB resource variable's typed initial value (US-027, T016), refused inside a locked
    /// block by the T003 central predicate. Serialization lives in <see cref="ResourceInitialValue.WriteTo"/>.</summary>
    public sealed record SetResourceInitialValue(ElementId Id, ResourceInitialValue Value) : ProjectCommand
    {
        internal override string Describe(Project project) => "Sæt startværdi";

        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireExists(Id, "variable")
                .And(context.RequireUnlockedTarget(Id, inclusive: true));   // T003: no edit inside a locked block

        internal override void Execute(ProjectEditor editor) => Value.WriteTo(editor.Resolve(Id, "variable"));
    }

    /// <summary>Edits an ordinary FB resource variable's Name, both documentation fields, and typed initial value in
    /// ONE undoable step (US-026/US-027, T016) — the whole Properties dialog applied atomically, refused inside a
    /// locked block by T003. A <see cref="ResourceValueKind.None"/> value leaves the initial value untouched.
    /// <para>
    /// <paramref name="HelpNote"/> is the SECOND documentation field, <c>note-2</c> (W5): the installer-facing help
    /// text the reference application shows alongside the function documentation. It defaults to the empty string so
    /// existing callers are unaffected, and because its DTD default is also empty an unset help note writes NO
    /// attribute — a project that never had one stays byte-identical.
    /// </para>
    /// </summary>
    public sealed record SetVariableProperties(
        ElementId Id, string Name, string Note, ResourceInitialValue Value, string HelpNote = "") : ProjectCommand
    {
        internal override string Describe(Project project) => "Rediger " + Name;

        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireExists(Id, "variable")
                .And(context.RequireUnlockedTarget(Id, inclusive: true));   // T003

        internal override void Execute(ProjectEditor editor)
        {
            ElementRef handle = editor.Resolve(Id, "variable");
            handle.SetAttribute("name", Name).SetAttribute("note", Note).SetAttribute("note-2", HelpNote);
            Value.WriteTo(handle);
        }
    }
}
