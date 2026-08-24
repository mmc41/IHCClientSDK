using System;
using System.Reflection;

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;

using ihc_openvisual.Services;
using ihc_openvisual.Views;

using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// RF downgraded item 1: the name prompt's blank-validator can no longer be ABSENT in a way that lets OK through.
///
/// <para>The decision is supplied, and rightly so — a View may not name <c>ProjectAppService</c>, and an emptiness
/// test written in the window is exactly the shell-side duplicate this window stopped carrying. But the field was
/// invoked with <c>?.</c>, so on the parameterless construction path OK SKIPPED the check entirely and went
/// straight to <c>Accept(NameBox.Text!.Trim())</c> — committing an unchecked name, and dereferencing a
/// <see langword="null"/> text with a <c>!</c> that has nothing behind it.</para>
///
/// <para>The fix is not a fallback validator, which would reintroduce the duplicate. With no validator there is no
/// decision to apply, so OK REFUSES: the dialog stays open, which is the same outcome a blank name produces.</para>
/// </summary>
public class NamePromptWithoutValidatorTests : AvaloniaTestBase
{
    private static NamePromptWindow Shown()
    {
        NamePromptWindow window = new();   // the parameterless path: XAML previewer, and tests
        window.Show();
        return window;
    }

    private static void PressOk(NamePromptWindow window) =>
        typeof(NamePromptWindow)
            .GetMethod("OnOk", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [null, new RoutedEventArgs()]);

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void WithNoValidatorOkRefusesABlankName()
    {
        NamePromptWindow window = Shown();
        CurrentTestWindow = window;
        window.FindControl<TextBox>("NameBox")!.Text = "   ";

        PressOk(window);

        Assert.That(window.AcceptedResult, Is.Null,
            "with no decision to apply, OK commits nothing — it does not fall through to Accept");
    }

    /// <summary>
    /// The null text is the other half: <c>Accept(NameBox.Text!.Trim())</c> dereferenced it behind a <c>!</c>, and
    /// a never-touched box on the parameterless path is exactly where the text is null.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void WithNoValidatorOkDoesNotDereferenceANullText()
    {
        NamePromptWindow window = Shown();
        CurrentTestWindow = window;
        window.FindControl<TextBox>("NameBox")!.Text = null;

        Assert.That(() => PressOk(window), Throws.Nothing, "OK must not throw on an untouched box");
        Assert.That(window.AcceptedResult, Is.Null);
    }

    /// <summary>
    /// The control: with a validator that ACCEPTS, OK still commits. The refusal above must come from the missing
    /// decision, not from OK having stopped working.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void WithAValidatorThatAcceptsOkStillCommits()
    {
        NamePromptWindow window = NamePromptWindow.Create(
            new NamePromptInput("Titel", "Stue", _ => null));
        CurrentTestWindow = window;
        window.Show();

        PressOk(window);

        Assert.That(window.AcceptedResult, Is.EqualTo("Stue"));
    }
}
