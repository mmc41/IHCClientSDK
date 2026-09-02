using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
using Ihc.Vis.Session;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal scene-value dialog (US-024/US-058): for a dimmer scene it asks a light level (%) and a ramp time
/// (minutes/seconds); for a relay/socket scene an ON/OFF state. Returns the edited <see cref="SceneValueResult"/>,
/// or null on Cancel.
/// <para>The level and ramp fields clamp to the constraints the SDK declares for a scene value (T045) — the same
/// level bounds its factory enforces — rather than to numbers repeated in this markup.</para>
/// </summary>
public partial class SceneValueWindow : ResultDialog<SceneValueResult>
{
    // The values the dialog opened with. An EMPTIED box commits the value it was opened with rather than a
    // constant written here — the same rule the advanced-dimmer window follows, and for the same reason: a
    // constant in the view is a second answer to "what is this field's value", and this one was WRONG. Zero is
    // below the declared minimum for a scene level, so clearing the box committed a level the dialog's own
    // bounds would have refused had the user typed it.
    private SceneValueInput _opened = new(string.Empty, IsDimmer: false, On: false, 0, 0, 0);

    public SceneValueWindow()
    {
        InitializeComponent();
    }

    public static Task<SceneValueResult?> ShowAsync(Window owner, SceneValueInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var window = new SceneValueWindow { Title = input.Title };
        window.Populate(input);
        return window.ShowDialogForResult(owner);
    }

    /// <summary>Fills the dialog. Separate from <see cref="ShowAsync"/> so a headless test drives the same
    /// wiring the application does rather than a window whose bounds and focus were never applied.</summary>
    internal void Populate(SceneValueInput input)
    {
        _opened = input;
        NumericFieldBounds.Apply(LevelBox, input.Level);
        NumericFieldBounds.Apply(RampMinutesBox, input.RampPart);
        NumericFieldBounds.Apply(RampSecondsBox, input.RampPart);
        DimmerPanel.IsVisible = input.IsDimmer;
        RelayPanel.IsVisible = !input.IsDimmer;
        LevelBox.Value = input.LevelPercent;
        RampMinutesBox.Value = input.RampMinutes;
        RampSecondsBox.Value = input.RampSeconds;
        StateCombo.SelectedIndex = input.On ? 1 : 0;
        // A route's field when it named one this variant HAS; otherwise the variant's own value field, which is
        // where the installer would start anyway.
        FocusOnOpen(FocusTarget(input.Focus) ?? (input.IsDimmer ? LevelBox : StateCombo));
    }

    /// <summary>
    /// The window's own map from a route's field key to the control holding that value.
    /// <para>Gated on the PANEL, not on the box: a member is either a dimmer or a relay, never both, and a
    /// control inside a collapsed panel still reports itself visible — so asking the box would land the caret on
    /// a field that is not on screen. A key belonging to the other variant answers null, and the dialog opens on
    /// the field it does have.</para>
    /// </summary>
    internal Control? FocusTarget(SceneDialogField? field) => field switch
    {
        SceneDialogField.State => RelayPanel.IsVisible ? StateCombo : null,
        SceneDialogField.Level => DimmerPanel.IsVisible ? LevelBox : null,
        SceneDialogField.RampTime => DimmerPanel.IsVisible ? RampMinutesBox : null,
        // Note belongs to the CONTAINER's dialog, which is a different window; naming it here would be a claim
        // this one cannot keep.
        _ => null,
    };

    private void OnOk(object? sender, RoutedEventArgs e) =>
        Accept(new SceneValueResult(
            StateCombo.SelectedIndex == 1,
            (int)(LevelBox.Value ?? _opened.LevelPercent),
            (int)(RampMinutesBox.Value ?? _opened.RampMinutes),
            (int)(RampSecondsBox.Value ?? _opened.RampSeconds)));
}
