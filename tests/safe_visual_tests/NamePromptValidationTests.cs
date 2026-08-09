using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// The name prompt refuses a blank name OUT LOUD (QC-07). It always refused — an all-whitespace name is not a name
/// the engine should have to second-guess — but it did so by doing nothing at all: the OK button appeared broken,
/// and a keyboard or screen-reader user got no signal whatsoever. The refusal now states its reason in a polite
/// live region, the shape this app already uses for exactly this class of "the command declined" message.
/// </summary>
public class NamePromptValidationTests : AvaloniaTestBase
{
    private static (NamePromptWindow Window, TextBox Box, TextBlock Error) Open()
    {
        var window = new NamePromptWindow();
        CurrentTestWindow = window;   // so a failure screenshot has something to capture
        window.Show();
        return (window,
            window.FindControl<TextBox>("NameBox")!,
            window.FindControl<TextBlock>("NameError")!);
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ANameIsNotDemandedBeforeItIsRefused()
    {
        (_, _, TextBlock error) = Open();

        Assert.That(error.IsVisible, Is.False, "nothing is wrong yet, so nothing is said");
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void OkOnABlankName_SaysWhyInsteadOfDoingNothing()
    {
        (NamePromptWindow window, TextBox box, TextBlock error) = Open();
        box.Text = "   ";   // whitespace only: the case that used to be a silent no-op

        window.FindControl<Button>("OkButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Multiple(() =>
        {
            Assert.That(error.IsVisible, Is.True, "the refusal is stated");
            Assert.That(error.Text, Is.Not.Empty.And.Not.Null);
            Assert.That(window.IsVisible, Is.True, "and the dialog stays open so the name can be fixed");
        });
    }

    /// <summary>The complaint is about what the box holds now, so typing retracts it.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TypingAName_RetractsTheComplaint()
    {
        (NamePromptWindow window, TextBox box, TextBlock error) = Open();
        window.FindControl<Button>("OkButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.That(error.IsVisible, Is.True, "precondition: the complaint is up");

        box.Text = "Stue";

        Assert.That(error.IsVisible, Is.False);
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void OkOnARealName_StillAcceptsIt()
    {
        (NamePromptWindow window, TextBox box, TextBlock error) = Open();
        box.Text = "  Stue  ";

        window.FindControl<Button>("OkButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Multiple(() =>
        {
            Assert.That(error.IsVisible, Is.False, "no complaint about a perfectly good name");
            Assert.That(window.IsVisible, Is.False, "OK closed the dialog");
        });
    }
}
