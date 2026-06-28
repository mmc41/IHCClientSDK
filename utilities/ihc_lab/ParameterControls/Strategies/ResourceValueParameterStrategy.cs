using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for ResourceValue parameters. Renders the full WSResourceValueEnvelope a SetResourceValue(s) write
/// needs: a ResourceID field, a TypeString field, an IsValueRuntime toggle, a ValueKind dropdown (all 12 kinds),
/// and a payload editor that is rebuilt to match the selected ValueKind (decision D4) - so only the valid payload
/// field(s) are ever editable. ENUM is entered as two numeric id fields (decision D5). The composite scene kinds
/// (SceneDimmer/SceneRelay/SceneShutter) and PhoneNumber show their multiple payload fields.
/// </summary>
public class ResourceValueParameterStrategy : ParameterControlStrategyBase
{
    private enum PayloadKind { Int, Long, Double, Bool, String, Date, Time }

    private sealed record PayloadField(string Name, PayloadKind Kind);

    /// <summary>The payload field(s) for each ValueKind (field name matches the SOAP/UnionValue shape).</summary>
    private static PayloadField[] PayloadFieldsFor(ResourceValue.ValueKind kind) => kind switch
    {
        ResourceValue.ValueKind.BOOL => new[] { new PayloadField("value", PayloadKind.Bool) },
        ResourceValue.ValueKind.INT => new[] { new PayloadField("value", PayloadKind.Int) },
        ResourceValue.ValueKind.DOUBLE => new[] { new PayloadField("value", PayloadKind.Double) },
        ResourceValue.ValueKind.DATE => new[] { new PayloadField("value", PayloadKind.Date) },
        ResourceValue.ValueKind.TIME => new[] { new PayloadField("value", PayloadKind.Time) },
        ResourceValue.ValueKind.TIMER => new[] { new PayloadField("value", PayloadKind.Long) },
        ResourceValue.ValueKind.WEEKDAY => new[] { new PayloadField("value", PayloadKind.Int) },
        ResourceValue.ValueKind.ENUM => new[] { new PayloadField("definitionTypeID", PayloadKind.Int), new PayloadField("enumValueID", PayloadKind.Int) },
        ResourceValue.ValueKind.PhoneNumber => new[] { new PayloadField("number", PayloadKind.String) },
        ResourceValue.ValueKind.SceneDimmer => new[] { new PayloadField("dimmerPercentage", PayloadKind.Int), new PayloadField("delayTime", PayloadKind.Int), new PayloadField("rampTime", PayloadKind.Int) },
        ResourceValue.ValueKind.SceneRelay => new[] { new PayloadField("delayTime", PayloadKind.Int), new PayloadField("relayValue", PayloadKind.Bool) },
        ResourceValue.ValueKind.SceneShutter => new[] { new PayloadField("shutterPositionIsUp", PayloadKind.Bool), new PayloadField("delayTime", PayloadKind.Int) },
        _ => Array.Empty<PayloadField>()
    };

    /// <summary>
    /// Determines if this strategy can handle ResourceValue types.
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        return field.Type == typeof(ResourceValue);
    }

    /// <summary>
    /// Creates the ResourceValue editor: ResourceID, ValueKind dropdown, and a kind-specific payload host.
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

        var mainPanel = new StackPanel { Name = controlName, Orientation = Orientation.Vertical, Spacing = 5 };

        var resourceIdUpDown = new NumericUpDown
        {
            Name = $"{controlName}.ResourceID",
            FormatString = "F0",
            Increment = 1,
            MinWidth = 120,
            Value = 0,
            Minimum = 0,
            Maximum = int.MaxValue
        };
        mainPanel.Children.Add(OperationSupport.LabeledRow("Resource ID", resourceIdUpDown, labelAlignment: VerticalAlignment.Center));

        // TypeString + IsValueRuntime complete the envelope alongside ResourceID and the union value; a
        // SetResourceValue(s) write needs all of them, so the editor exposes them rather than sending defaults.
        var typeStringBox = new TextBox
        {
            Name = $"{controlName}.TypeString",
            Width = 180,
            Watermark = "e.g. dataline_output (optional)"
        };
        mainPanel.Children.Add(OperationSupport.LabeledRow("Type String", typeStringBox, labelAlignment: VerticalAlignment.Center));

        var isValueRuntimeCheck = new CheckBox
        {
            Name = $"{controlName}.IsValueRuntime",
            IsThreeState = false,
            IsChecked = true
        };
        mainPanel.Children.Add(OperationSupport.LabeledRow("Is Value Runtime", isValueRuntimeCheck, labelAlignment: VerticalAlignment.Center));

        var valueKindDropDown = new ComboBox
        {
            Name = $"{controlName}.ValueKind",
            MinWidth = 140,
            ItemsSource = Enum.GetNames(typeof(ResourceValue.ValueKind)),
            SelectedIndex = 0
        };
        mainPanel.Children.Add(OperationSupport.LabeledRow("Value Kind", valueKindDropDown, labelAlignment: VerticalAlignment.Center));

        var payloadHost = new StackPanel
        {
            Name = $"{controlName}.Payload",
            Orientation = Orientation.Vertical,
            Spacing = 5,
            Margin = new Avalonia.Thickness(20, 0, 0, 0)
        };
        mainPanel.Children.Add(payloadHost);

        // Build the payload for the initially-selected kind.
        RebuildPayload(payloadHost, SelectedKind(valueKindDropDown));

        // Changing the kind rebuilds the payload to only the valid field(s) and notifies the parameter changed.
        valueKindDropDown.SelectionChanged += (s, e) =>
        {
            RebuildPayload(payloadHost, SelectedKind(valueKindDropDown));
            RaiseContainerChanged(mainPanel);
        };

        // ResourceID / TypeString / IsValueRuntime edits also change the parameter.
        resourceIdUpDown.ValueChanged += (s, e) => RaiseContainerChanged(mainPanel);
        typeStringBox.TextChanged += (s, e) => RaiseContainerChanged(mainPanel);
        isValueRuntimeCheck.IsCheckedChanged += (s, e) => RaiseContainerChanged(mainPanel);

        ApplyDescriptionTooltip(mainPanel, field);

        return mainPanel;
    }

    /// <summary>
    /// Registers the parameter-changed handler; edits to ResourceID, ValueKind and payload controls route to it.
    /// </summary>
    public override void SubscribeToValueChanged(Control control, EventHandler handler)
        => RegisterContainerSubscription(control, handler);

    /// <summary>
    /// Extracts a ResourceValue (ResourceID + the union payload for the selected ValueKind) from the controls.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
        var mainPanel = RequireControl<StackPanel>(control);

        var resourceIdControl = FindNamed<NumericUpDown>(mainPanel, $"{mainPanel.Name}.ResourceID");
        var typeStringControl = FindNamed<TextBox>(mainPanel, $"{mainPanel.Name}.TypeString");
        var isValueRuntimeControl = FindNamed<CheckBox>(mainPanel, $"{mainPanel.Name}.IsValueRuntime");
        var valueKindControl = FindNamed<ComboBox>(mainPanel, $"{mainPanel.Name}.ValueKind");
        var payloadHost = FindNamed<StackPanel>(mainPanel, $"{mainPanel.Name}.Payload");

        int resourceId = (int)(resourceIdControl?.Value ?? 0);
        var kind = SelectedKind(valueKindControl);

        return new ResourceValue
        {
            ResourceID = resourceId,
            TypeString = TypeStringOrNull(typeStringControl?.Text),
            IsValueRuntime = isValueRuntimeControl?.IsChecked ?? true,
            Value = BuildUnion(kind, payloadHost)
        };
    }

    /// <summary>
    /// Restores a ResourceValue into the controls (ResourceID, ValueKind, and the payload for that kind).
    /// </summary>
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        var mainPanel = RequireControl<StackPanel>(control);

        var resourceIdControl = FindNamed<NumericUpDown>(mainPanel, $"{mainPanel.Name}.ResourceID");
        var typeStringControl = FindNamed<TextBox>(mainPanel, $"{mainPanel.Name}.TypeString");
        var isValueRuntimeControl = FindNamed<CheckBox>(mainPanel, $"{mainPanel.Name}.IsValueRuntime");
        var valueKindControl = FindNamed<ComboBox>(mainPanel, $"{mainPanel.Name}.ValueKind");
        var payloadHost = FindNamed<StackPanel>(mainPanel, $"{mainPanel.Name}.Payload");

        if (value is not ResourceValue resourceValue)
        {
            if (resourceIdControl != null && resourceIdControl.Value != 0) resourceIdControl.Value = 0;
            if (typeStringControl != null && !string.IsNullOrEmpty(typeStringControl.Text)) typeStringControl.Text = string.Empty;
            if (isValueRuntimeControl != null && isValueRuntimeControl.IsChecked != true) isValueRuntimeControl.IsChecked = true;
            if (valueKindControl != null) valueKindControl.SelectedIndex = 0;
            if (payloadHost != null) EnsurePayloadMatchesKind(payloadHost, SelectedKind(valueKindControl));
            return;
        }

        // Set ResourceID/TypeString/IsValueRuntime/ValueKind only when they actually differ, so a GUI->service->GUI
        // round-trip restore does not fire redundant change events.
        if (resourceIdControl != null && resourceIdControl.Value != resourceValue.ResourceID)
            resourceIdControl.Value = resourceValue.ResourceID;

        var typeString = resourceValue.TypeString ?? string.Empty;
        if (typeStringControl != null && (typeStringControl.Text ?? string.Empty) != typeString)
            typeStringControl.Text = typeString;

        if (isValueRuntimeControl != null && isValueRuntimeControl.IsChecked != resourceValue.IsValueRuntime)
            isValueRuntimeControl.IsChecked = resourceValue.IsValueRuntime;

        var kind = resourceValue.Value.ValueKind;
        if (valueKindControl?.ItemsSource != null)
        {
            var items = valueKindControl.ItemsSource.Cast<string>().ToList();
            int index = items.IndexOf(kind.ToString());
            // A real change to SelectedIndex triggers the SelectionChanged handler, which rebuilds the payload.
            if (index >= 0 && valueKindControl.SelectedIndex != index)
                valueKindControl.SelectedIndex = index;
        }

        if (payloadHost != null)
        {
            // Rebuild the payload editors ONLY when they do not already match the kind (e.g. the kind index was
            // unchanged, so the SelectionChanged handler did not fire). When the kind is unchanged we reuse the
            // existing editors and only push values into them - this is what makes a round-trip restore preserve
            // the control the user is editing instead of destroying it mid-keystroke (US-A3 focus preservation).
            EnsurePayloadMatchesKind(payloadHost, kind);
            SetPayloadFromUnion(payloadHost, kind, resourceValue.Value);
        }
    }

    /// <summary>
    /// Rebuilds the payload editors only when they do not already match <paramref name="kind"/>. The kind the
    /// payload was last built for is tracked on the payload host's Tag (set by <see cref="RebuildPayload"/>).
    /// </summary>
    private static void EnsurePayloadMatchesKind(StackPanel payloadHost, ResourceValue.ValueKind kind)
    {
        if (payloadHost.Tag is ResourceValue.ValueKind built && built == kind)
            return;

        RebuildPayload(payloadHost, kind);
    }

    // ---- Payload build / read / restore --------------------------------------------------------------------

    private static void RebuildPayload(StackPanel payloadHost, ResourceValue.ValueKind kind)
    {
        payloadHost.Children.Clear();

        foreach (var pf in PayloadFieldsFor(kind))
        {
            // Int editors are bounded to int range (an Int payload is read back via (int) cast); Long editors use
            // the full long range. This keeps a payload value from overflowing its target union field.
            Control editor = pf.Kind switch
            {
                PayloadKind.Bool => new CheckBox { IsThreeState = false, IsChecked = false },
                PayloadKind.String => new TextBox { Width = 180 },
                PayloadKind.Date => new DatePicker { SelectedDate = DateTimeOffset.Now },
                PayloadKind.Time => ZeroDurationInput(),
                PayloadKind.Double => new NumericUpDown { Width = 160, FormatString = "F2", Increment = 0.1m, Minimum = -999999999m, Maximum = 999999999m, Value = 0m },
                PayloadKind.Long => new NumericUpDown { Width = 160, FormatString = "F0", Increment = 1m, Minimum = long.MinValue, Maximum = long.MaxValue, Value = 0m },
                _ => new NumericUpDown { Width = 160, FormatString = "F0", Increment = 1m, Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0m }
            };
            editor.Name = $"{payloadHost.Name}.{pf.Name}";

            // Editing a payload control re-extracts the whole ResourceValue parameter (two-way sync). The
            // editor-type -> change-event mapping lives once in the shared base helper (SubscribeLeafChange).
            var mainPanel = MainPanelOf(payloadHost);
            if (mainPanel != null)
                SubscribeLeafChange(editor, (s, e) => RaiseContainerChanged(mainPanel));

            payloadHost.Children.Add(OperationSupport.LabeledRow(DisplayNameOf(pf.Name), editor, labelAlignment: VerticalAlignment.Center));
        }

        // Track the kind the payload was built for so a same-kind restore can reuse these editors (see SetValue).
        payloadHost.Tag = kind;
    }

    private static ResourceValue.UnionValue BuildUnion(ResourceValue.ValueKind kind, StackPanel? payloadHost)
    {
        var union = new ResourceValue.UnionValue { ValueKind = kind };
        if (payloadHost == null)
            return union;

        switch (kind)
        {
            case ResourceValue.ValueKind.BOOL: union.BoolValue = GetBool(payloadHost, "value"); break;
            case ResourceValue.ValueKind.INT: union.IntValue = GetInt(payloadHost, "value"); break;
            case ResourceValue.ValueKind.DOUBLE: union.DoubleValue = GetDouble(payloadHost, "value"); break;
            case ResourceValue.ValueKind.DATE: union.DateValue = GetDate(payloadHost, "value"); break;
            case ResourceValue.ValueKind.TIME: union.TimeValue = GetTime(payloadHost, "value"); break;
            case ResourceValue.ValueKind.TIMER: union.TimerValue = GetLong(payloadHost, "value"); break;
            case ResourceValue.ValueKind.WEEKDAY: union.WeekdayValue = GetInt(payloadHost, "value"); break;
            case ResourceValue.ValueKind.ENUM:
                union.EnumValue = new EnumValue { DefinitionTypeID = GetInt(payloadHost, "definitionTypeID"), EnumValueID = GetInt(payloadHost, "enumValueID") };
                break;
            case ResourceValue.ValueKind.PhoneNumber: union.PhoneNumberValue = GetString(payloadHost, "number"); break;
            case ResourceValue.ValueKind.SceneDimmer:
                union.DimmerPercentage = GetInt(payloadHost, "dimmerPercentage");
                union.DimmerDelayTime = GetInt(payloadHost, "delayTime");
                union.DimmerRampTime = GetInt(payloadHost, "rampTime");
                break;
            case ResourceValue.ValueKind.SceneRelay:
                union.RelayDelayTime = GetInt(payloadHost, "delayTime");
                union.RelayValue = GetBool(payloadHost, "relayValue");
                break;
            case ResourceValue.ValueKind.SceneShutter:
                union.ShutterPositionIsUp = GetBool(payloadHost, "shutterPositionIsUp");
                union.ShutterDelayTime = GetInt(payloadHost, "delayTime");
                break;
        }

        return union;
    }

    private static void SetPayloadFromUnion(StackPanel payloadHost, ResourceValue.ValueKind kind, ResourceValue.UnionValue union)
    {
        switch (kind)
        {
            case ResourceValue.ValueKind.BOOL: SetBool(payloadHost, "value", union.BoolValue ?? false); break;
            case ResourceValue.ValueKind.INT: SetNum(payloadHost, "value", union.IntValue ?? 0); break;
            case ResourceValue.ValueKind.DOUBLE: SetNum(payloadHost, "value", (decimal)(union.DoubleValue ?? 0)); break;
            case ResourceValue.ValueKind.DATE: SetDate(payloadHost, "value", union.DateValue ?? DateTimeOffset.Now); break;
            case ResourceValue.ValueKind.TIME: SetTime(payloadHost, "value", union.TimeValue ?? TimeSpan.Zero); break;
            case ResourceValue.ValueKind.TIMER: SetNum(payloadHost, "value", union.TimerValue ?? 0); break;
            case ResourceValue.ValueKind.WEEKDAY: SetNum(payloadHost, "value", union.WeekdayValue ?? 0); break;
            case ResourceValue.ValueKind.ENUM:
                SetNum(payloadHost, "definitionTypeID", union.EnumValue?.DefinitionTypeID ?? 0);
                SetNum(payloadHost, "enumValueID", union.EnumValue?.EnumValueID ?? 0);
                break;
            case ResourceValue.ValueKind.PhoneNumber: SetString(payloadHost, "number", union.PhoneNumberValue ?? string.Empty); break;
            case ResourceValue.ValueKind.SceneDimmer:
                SetNum(payloadHost, "dimmerPercentage", union.DimmerPercentage ?? 0);
                SetNum(payloadHost, "delayTime", union.DimmerDelayTime ?? 0);
                SetNum(payloadHost, "rampTime", union.DimmerRampTime ?? 0);
                break;
            case ResourceValue.ValueKind.SceneRelay:
                SetNum(payloadHost, "delayTime", union.RelayDelayTime ?? 0);
                SetBool(payloadHost, "relayValue", union.RelayValue ?? false);
                break;
            case ResourceValue.ValueKind.SceneShutter:
                SetBool(payloadHost, "shutterPositionIsUp", union.ShutterPositionIsUp ?? false);
                SetNum(payloadHost, "delayTime", union.ShutterDelayTime ?? 0);
                break;
        }
    }

    // ---- Helpers -------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a zero-seeded <see cref="DurationInput"/> for a TIME payload. The duration is seeded through the
    /// control's own API (matching <c>TimeSpanParameterStrategy</c>) so the rendered "hh:mm:ss" format stays owned
    /// by <see cref="DurationInput"/> instead of being duplicated as a literal here.
    /// </summary>
    private static DurationInput ZeroDurationInput()
    {
        var input = new DurationInput();
        input.SetValue(TimeSpan.Zero);
        return input;
    }

    /// <summary>
    /// Maps a raw SOAP/UnionValue payload field name to a friendly label. The raw name stays the editor's control
    /// name (used for lookup); only the visible label is humanised.
    /// </summary>
    private static string DisplayNameOf(string rawName) => rawName switch
    {
        "value" => "Value",
        "number" => "Number",
        "definitionTypeID" => "Definition Type ID",
        "enumValueID" => "Enum Value ID",
        "dimmerPercentage" => "Dimmer %",
        "delayTime" => "Delay Time",
        "rampTime" => "Ramp Time",
        "relayValue" => "Relay Value",
        "shutterPositionIsUp" => "Position Is Up",
        _ => rawName
    };

    private static ResourceValue.ValueKind SelectedKind(ComboBox? combo)
    {
        if (combo?.SelectedItem is string name && Enum.TryParse<ResourceValue.ValueKind>(name, out var kind))
            return kind;
        return ResourceValue.ValueKind.BOOL;
    }

    // An empty/blank TypeString box means "no type string" (null), matching the absent SOAP field, rather than "".
    private static string? TypeStringOrNull(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static StackPanel? MainPanelOf(StackPanel payloadHost)
        => payloadHost.Parent as StackPanel;

    private static T? FindNamed<T>(StackPanel mainPanel, string name) where T : Control
    {
        // Top-level named controls (ResourceID/ValueKind/Payload) live inside labeled rows or directly on the panel.
        foreach (var child in mainPanel.Children)
        {
            if (child is T t && t.Name == name)
                return t;
            if (child is Panel row)
            {
                var hit = row.Children.OfType<T>().FirstOrDefault(c => c.Name == name);
                if (hit != null)
                    return hit;
            }
        }
        return null;
    }

    // Payload editors are always added inside a LabeledRow, so the shared one-level lookup finds them by name.
    private static Control? FindPayload(StackPanel payloadHost, string fieldName)
        => FindNamed<Control>(payloadHost, $"{payloadHost.Name}.{fieldName}");

    private static int GetInt(StackPanel host, string name) => (int)((FindPayload(host, name) as NumericUpDown)?.Value ?? 0m);
    private static long GetLong(StackPanel host, string name) => (long)((FindPayload(host, name) as NumericUpDown)?.Value ?? 0m);
    private static double GetDouble(StackPanel host, string name) => (double)((FindPayload(host, name) as NumericUpDown)?.Value ?? 0m);
    private static bool GetBool(StackPanel host, string name) => (FindPayload(host, name) as CheckBox)?.IsChecked == true;
    private static string GetString(StackPanel host, string name) => (FindPayload(host, name) as TextBox)?.Text ?? string.Empty;
    private static DateTimeOffset GetDate(StackPanel host, string name) => (FindPayload(host, name) as DatePicker)?.SelectedDate ?? DateTimeOffset.Now;

    // Invalid text throws (via DurationInput) instead of silently coercing to zero, matching TimeSpanParameterStrategy.
    private static TimeSpan GetTime(StackPanel host, string name)
        => (FindPayload(host, name) as DurationInput)?.GetValueOrThrow() ?? TimeSpan.Zero;

    private static void SetNum(StackPanel host, string name, decimal value)
    {
        if (FindPayload(host, name) is NumericUpDown n) n.Value = value;
    }

    private static void SetBool(StackPanel host, string name, bool value)
    {
        if (FindPayload(host, name) is CheckBox c) c.IsChecked = value;
    }

    private static void SetString(StackPanel host, string name, string value)
    {
        if (FindPayload(host, name) is TextBox t) t.Text = value;
    }

    private static void SetDate(StackPanel host, string name, DateTimeOffset value)
    {
        if (FindPayload(host, name) is DatePicker d) d.SelectedDate = value;
    }

    // DurationInput.SetValue keeps the caret stable during the per-keystroke round-trip restore (it skips reformatting
    // a partial or already-equal entry).
    private static void SetTime(StackPanel host, string name, TimeSpan value)
    {
        if (FindPayload(host, name) is DurationInput d)
            d.SetValue(value);
    }
}
