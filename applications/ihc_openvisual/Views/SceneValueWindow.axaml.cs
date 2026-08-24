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
        var window = new SceneValueWindow { Title = input.Title, _opened = input };
        NumericFieldBounds.Apply(window.LevelBox, input.Level);
        NumericFieldBounds.Apply(window.RampMinutesBox, input.RampPart);
        NumericFieldBounds.Apply(window.RampSecondsBox, input.RampPart);
        window.DimmerPanel.IsVisible = input.IsDimmer;
        window.RelayPanel.IsVisible = !input.IsDimmer;
        window.LevelBox.Value = input.LevelPercent;
        window.RampMinutesBox.Value = input.RampMinutes;
        window.RampSecondsBox.Value = input.RampSeconds;
        window.StateCombo.SelectedIndex = input.On ? 1 : 0;
        return window.ShowDialogForResult(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e) =>
        Accept(new SceneValueResult(
            StateCombo.SelectedIndex == 1,
            (int)(LevelBox.Value ?? _opened.LevelPercent),
            (int)(RampMinutesBox.Value ?? _opened.RampMinutes),
            (int)(RampSecondsBox.Value ?? _opened.RampSeconds)));
}
