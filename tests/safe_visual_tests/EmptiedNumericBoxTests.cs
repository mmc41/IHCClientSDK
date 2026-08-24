using System;
using System.Linq;
using System.Reflection;

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;

using ihc_openvisual.Services;
using ihc_openvisual.Views;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;

using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// RF Tier-4: an EMPTIED numeric box commits the value its dialog opened with — in the scene-value window as it
/// already does in the advanced-dimmer window.
///
/// <para>An Avalonia <see cref="NumericUpDown"/> whose text is cleared has a null <c>Value</c>. The dimmer window
/// answers that with <c>?? _opened.SoftOnMs</c> — the value the box was shown with — and says why: "the factory
/// defaults already live at the read site, and a second copy in the view could disagree with them". The scene
/// window answered it with <c>?? 0</c>, an invented constant, so clearing the level box committed 0% rather than
/// leaving the level alone.</para>
///
/// <para>Zero is not a harmless fallback here: it is BELOW the declared minimum for a scene level, so an emptied
/// box committed a value the dialog's own bounds would have refused had it been typed.</para>
/// </summary>
public class EmptiedNumericBoxTests : AvaloniaTestBase
{
    private const int OpenedLevel = 60;
    private const int OpenedMinutes = 2;
    private const int OpenedSeconds = 30;

    private static readonly SceneValueInput Input = new(
        "Scenarieværdi", IsDimmer: true, On: true, OpenedLevel, OpenedMinutes, OpenedSeconds,
        Level: FieldConstraintMetadata.Unconstrained with { Minimum = 1, Maximum = 100 },
        RampPart: FieldConstraintMetadata.Unconstrained with { Minimum = 0, Maximum = 59 });

    /// <summary>
    /// The window prepared exactly as <c>ShowAsync</c> prepares it, minus the modal show — which needs an owner
    /// and a dispatcher loop the headless tests do not run. The opened-value field is set by reflection for the
    /// same reason: the test is about what the window falls back TO, so it must be opened the way the dialog
    /// opens it.
    /// </summary>
    private static SceneValueWindow Opened()
    {
        SceneValueWindow window = new();
        foreach (FieldInfo field in typeof(SceneValueWindow)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(SceneValueInput)))
        {
            field.SetValue(window, Input);
        }

        window.Show();
        Box(window, "LevelBox").Value = Input.LevelPercent;
        Box(window, "RampMinutesBox").Value = Input.RampMinutes;
        Box(window, "RampSecondsBox").Value = Input.RampSeconds;
        window.FindControl<ComboBox>("StateCombo")!.SelectedIndex = Input.On ? 1 : 0;
        return window;
    }

    private static NumericUpDown Box(SceneValueWindow window, string name) =>
        window.FindControl<NumericUpDown>(name)!;

    /// <summary>Presses OK and reads the seam the dialog base exposes for exactly this.</summary>
    private static SceneValueResult? Ok(SceneValueWindow window)
    {
        typeof(SceneValueWindow)
            .GetMethod("OnOk", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [null, new RoutedEventArgs()]);
        return window.AcceptedResult;
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AnEmptiedLevelBoxCommitsTheOpenedValueAndNeverBelowTheDeclaredMinimum()
    {
        SceneValueWindow window = Opened();
        CurrentTestWindow = window;
        Box(window, "LevelBox").Value = null;   // the user cleared the box

        SceneValueResult? result = Ok(window);

        Assert.That(result, Is.Not.Null, "OK committed a result");
        Assert.Multiple(() =>
        {
            Assert.That(result!.LevelPercent, Is.EqualTo(OpenedLevel),
                "an emptied box leaves the level as the dialog opened it");
            Assert.That(result.LevelPercent, Is.GreaterThanOrEqualTo(1),
                "and never commits a value the dialog's own declared minimum would refuse");
        });
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AnEmptiedRampBoxCommitsTheOpenedValue()
    {
        SceneValueWindow window = Opened();
        CurrentTestWindow = window;
        Box(window, "RampMinutesBox").Value = null;
        Box(window, "RampSecondsBox").Value = null;

        SceneValueResult? result = Ok(window);

        Assert.Multiple(() =>
        {
            Assert.That(result!.RampMinutes, Is.EqualTo(OpenedMinutes));
            Assert.That(result.RampSeconds, Is.EqualTo(OpenedSeconds));
        });
    }

    /// <summary>The control: a box the user actually edited commits what they typed.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AnEditedBoxStillCommitsWhatWasTyped()
    {
        SceneValueWindow window = Opened();
        CurrentTestWindow = window;
        Box(window, "LevelBox").Value = 25;

        Assert.That(Ok(window)!.LevelPercent, Is.EqualTo(25));
    }
}
