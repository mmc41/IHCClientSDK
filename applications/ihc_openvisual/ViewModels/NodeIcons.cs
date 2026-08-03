namespace ihc_openvisual.ViewModels;

/// <summary>
/// Resolves the <c>/Assets/*.svg</c> glyph for a tree node from the project element's tag and its <c>icon</c> code
/// attribute (per <c>docs/icon_codes.md</c>). Tag-first for the types where a tag is decisive (localities, pins,
/// containers, function blocks, variables, program elements); otherwise by the vendor <c>_0xNN</c> code (products fan
/// out by code). Covers the full E12 icon language — every node category has a distinct glyph; unknown elements fall
/// back to a neutral glyph rather than failing. The library-vs-editable function block distinction is applied by the
/// tree builders (they pass <c>fb-lk</c>/<c>fb-editable</c> per the <c>locked</c> attribute); simulation red/green
/// state colouring is out of scope (E8).
/// </summary>
public static class NodeIcons
{
    public const string Locality = "/Assets/locality.svg";

    /// <summary>The two function-block glyphs: a locked library block vs an editable authored block.</summary>
    public const string FunctionBlockLibrary = "/Assets/fb-lk.svg";
    public const string FunctionBlockEditable = "/Assets/fb-editable.svg";

    /// <summary>The function-block glyph, keyed by whether the block is a locked library block (<c>fb-lk</c>) or an
    /// editable authored block (<c>fb-editable</c>) — the distinction the tree builders apply per the element's
    /// <c>locked</c> attribute.</summary>
    public static string FunctionBlock(bool locked) => locked ? FunctionBlockLibrary : FunctionBlockEditable;

    /// <summary>The two status-bar controller-connection glyphs (W9/F10). Two GLYPHS, not one in two colours — a
    /// colour-only signal fails <c>docs/icons_design.md</c>.</summary>
    public const string ControllerConnected = "/Assets/controller-connected.svg";
    public const string ControllerDisconnected = "/Assets/controller-disconnected.svg";

    /// <summary>The connection indicator's glyph, keyed by whether a controller is connected.</summary>
    public static string ControllerConnection(bool connected) => connected ? ControllerConnected : ControllerDisconnected;

    public static string For(string tag, string? iconCode) => tag switch
    {
        "groups" or "group" => Locality,
        "resource_input" or "dataline_input" => "/Assets/pin-in.svg",
        "resource_output" or "dataline_output" => "/Assets/pin-out.svg",
        "resource_scene" => "/Assets/scenario.svg",
        "resource_flag" => "/Assets/var-flag.svg",
        "resource_counter" => "/Assets/var-counter.svg",
        "resource_integer" => "/Assets/var-integer.svg",
        "resource_floating_point" => "/Assets/var-decimal.svg",
        "resource_timer" => "/Assets/var-timer.svg",
        "resource_timertime" => "/Assets/var-timer-duration.svg",
        "resource_weekday" => "/Assets/var-weekday.svg",
        "resource_date" => "/Assets/var-date.svg",
        "resource_time" => "/Assets/var-time.svg",
        "resource_temperature" => "/Assets/var-temperature.svg",
        "resource_light" => "/Assets/var-illuminance.svg",
        "resource_light_level" => "/Assets/var-light-level.svg",
        "resource_humidity_level" => "/Assets/var-humidity.svg",
        "resource_holiday" => "/Assets/var-holiday.svg",
        "resource_enum" => "/Assets/var-enum.svg",
        // The S0/meter power & energy types are UNIT-NAMED element tags, not resource_* ones (icon_codes.md §3b),
        // and carry no `icon` code — so without these four cases they fell through to the neutral fallback and every
        // meter variable rendered as a locality. All four share one glyph, as the doc and the report mapping specify.
        "kW" or "kWh" or "W" or "Wh" => "/Assets/var-energy.svg",
        "inputs" => "/Assets/section-input.svg",
        "outputs" => "/Assets/section-output.svg",
        "settings" => "/Assets/section-settings.svg",
        "internalsettings" => "/Assets/section-internal-vars.svg",
        "programs" or "program_simple" => "/Assets/prog-program.svg",
        "events" => "/Assets/event-group.svg",
        "actions" => "/Assets/command-group.svg",
        "event" or "event_power" => "/Assets/event.svg",
        "action" => "/Assets/command.svg",
        "program_sub" => "/Assets/prog-subprogram.svg",
        "program_case" => "/Assets/prog-subprogram.svg",
        "case_action" => "/Assets/command-group.svg",
        "condition" => "/Assets/condition.svg",
        "conditions" => "/Assets/cond-and.svg",
        "conditions-or" => "/Assets/cond-or.svg",
        "functionblock" => FunctionBlockLibrary,
        "dataline_input_modules" or "dataline_output_modules" or "documentation_modules" => "/Assets/rs485-module.svg",
        _ => ByCode(iconCode),
    };

    private static string ByCode(string? code) => code switch
    {
        "_0x83" => "/Assets/product-sensor.svg",
        "_0x85" => "/Assets/product-button.svg",
        "_0x86" => "/Assets/product-lamp.svg",
        "_0x88" => "/Assets/product-socket.svg",
        "_0x99" => "/Assets/product-s0.svg",
        "_0x36" => "/Assets/pin-in.svg",
        "_0x39" => "/Assets/pin-out.svg",
        "_0x89" => "/Assets/scenario.svg",
        _ => Locality,   // neutral fallback until the full icon language (E12) lands
    };
}
