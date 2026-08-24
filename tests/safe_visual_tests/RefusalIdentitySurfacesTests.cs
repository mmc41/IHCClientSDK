using System;
using System.Linq;
using System.Reflection;

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;

using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Session;

using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// RF Tier-4: the two refusal surfaces that showed a Danish sentence with no identity now carry their code, like
/// every dialog already does.
///
/// <para>R18's rule is that identity reaches the installer as a subordinate bracketed suffix — it is what lets a
/// user quote a refusal and a support reader find the row behind it. Every <c>ShowProblemAsync</c> box renders
/// through <see cref="ProblemPresenter"/> and gets it. The STATUS BAR and the name prompt's INLINE error did
/// not: they took the sentence off the outcome or the problem and showed it raw, so the same refusal was
/// identified in a dialog and anonymous in the status bar.</para>
/// </summary>
public class RefusalIdentitySurfacesTests : AvaloniaTestBase
{
    private static EditOutcome Refused(string code, string reason) =>
        new(EditStatus.Refused, "Slet", reason, null, new ProblemCode(code));

    [Test]
    public void ARefusedEditsStatusTextEndsInItsBracketedCode()
    {
        string? shown = MainWindowViewModel.UserFacingRefusal(
            Refused("edit.target-locked", "Blokken er låst."));

        Assert.That(shown, Is.EqualTo("Blokken er låst. [edit.target-locked]"),
            "the status bar identifies a refusal exactly as a dialog does");
    }

    /// <summary>
    /// A refusal carrying NO code renders its message alone rather than an empty bracket pointing at nothing —
    /// the rule <see cref="ProblemPresenter"/> already applies, reached through the same path.
    /// </summary>
    [Test]
    public void ARefusalWithoutACodeShowsItsMessageAlone()
    {
        string? shown = MainWindowViewModel.UserFacingRefusal(
            new EditOutcome(EditStatus.Refused, "Slet", "Ingen kode her.", null));

        Assert.That(shown, Is.EqualTo("Ingen kode her."));
    }

    /// <summary>A FAILED outcome is still not user-facing: its reason is the engine's English diagnostic.</summary>
    [Test]
    public void AFailedOutcomeIsStillNotShown()
    {
        Assert.That(
            MainWindowViewModel.UserFacingRefusal(
                new EditOutcome(EditStatus.Failed, "Slet", "Element 'group' carries attribute 'x'", null)),
            Is.Null);
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheNamePromptsInlineErrorEndsInItsBracketedCode()
    {
        Problem blank = new(
            new ProblemCode("edit.value-required"), "Feltet skal udfyldes.",
            EquatableArray<ProblemArgument>.Empty);

        NamePromptWindow window = new();
        CurrentTestWindow = window;
        foreach (FieldInfo field in typeof(NamePromptWindow)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(Func<string?, Problem?>)))
        {
            field.SetValue(window, (Func<string?, Problem?>)(_ => blank));
        }

        window.Show();
        window.FindControl<TextBox>("NameBox")!.Text = "   ";
        typeof(NamePromptWindow)
            .GetMethod("OnOk", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [null, new RoutedEventArgs()]);

        TextBlock error = window.FindControl<TextBlock>("NameError")!;

        Assert.Multiple(() =>
        {
            Assert.That(error.IsVisible, Is.True, "the refusal is shown rather than swallowed");
            Assert.That(error.Text, Is.EqualTo("Feltet skal udfyldes. [edit.value-required]"),
                "the inline error identifies the refusal exactly as a dialog does");
            Assert.That(window.AcceptedResult, Is.Null, "and the dialog stays open");
        });
    }
}
