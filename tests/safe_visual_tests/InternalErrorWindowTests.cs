using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Ihc.Vis.Problems;
using Avalonia.Input.Platform;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The details surface for one internal error.
///
/// <para><b>The invariant under test is the Danish sentence surviving whole.</b> Putting an English diagnostic on
/// screen is the leak the architecture gate exists to stop; putting it in a LABELLED readout underneath a Danish
/// sentence that says what happened is the posture that makes a fault transmissible instead of unactionable. The
/// difference between those two is entirely in what the top of the window says, which is what these tests
/// assert.</para>
/// </summary>
[TestFixture]
public class InternalErrorWindowTests : AvaloniaTestBase
{
    private const string Sentence = ProblemsTestData.RuleFailedMessage;

    private static InternalError Fault(
        string code = "internal.rule-failed",
        string? diagnostic = "Rule 'name-empty' threw",
        InternalErrorOrigin origin = InternalErrorOrigin.Sdk) =>
        new(new ProblemCode(code), Sentence, diagnostic, origin,
            "System.InvalidOperationException: boom\r\n   at Rule()", DateTimeOffset.UnixEpoch);

    private static T Find<T>(Window window, string name) where T : Control =>
        window.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);

    [AvaloniaTest]
    public void TheDanishSentenceIsRenderedWholeAtTheTop()
    {
        InternalErrorWindow window = new(Fault());

        Assert.That(Find<TextBlock>(window, "Sentence").Text, Is.EqualTo(Sentence),
            "WHOLE: not re-worded, not prefixed, not assembled from parts — the catalogue bound this sentence");
    }

    /// <summary>
    /// The English diagnostic belongs in the readout, never at the top. This is the D01 leak stated as a test:
    /// the two texts must not swap places.
    /// </summary>
    [AvaloniaTest]
    public void TheEnglishDiagnosticStaysInTheReadout()
    {
        InternalErrorWindow window = new(Fault());

        Assert.Multiple(() =>
        {
            Assert.That(Find<TextBlock>(window, "Sentence").Text, Does.Not.Contain("threw"));
            Assert.That(Find<TextBox>(window, "DetailText").Text, Does.Contain("Rule 'name-empty' threw"));
            Assert.That(Find<TextBox>(window, "DetailText").Text, Does.Contain("at Rule()"),
                "and the captured detail is below it, in the same readout");
        });
    }

    /// <summary>A fault with no diagnostic shows the detail alone rather than a blank line above it.</summary>
    [AvaloniaTest]
    public void AFaultWithNoDiagnosticShowsTheDetailAlone()
    {
        InternalErrorWindow window = new(Fault(diagnostic: null));

        Assert.That(Find<TextBox>(window, "DetailText").Text, Does.StartWith("System.InvalidOperationException"));
    }

    /// <summary>The identity line is what a support case quotes: which fault, from where, and when.</summary>
    [AvaloniaTest]
    public void TheIdentityLineNamesTheCodeAndTheOrigin()
    {
        InternalErrorWindow window = new(Fault(origin: InternalErrorOrigin.Host));

        string identity = Find<TextBlock>(window, "Identity").Text!;
        Assert.Multiple(() =>
        {
            Assert.That(identity, Does.Contain("internal.rule-failed"));
            Assert.That(identity, Does.Contain("Program"),
                "the origin in Danish — a reader has to be able to tell our bug from the platform's");
            Assert.That(identity, Does.Contain("1970"), "and when it was observed");
        });
    }

    /// <summary>Every origin renders; a new member must be given its Danish word rather than silently rendering
    /// as an enum name.</summary>
    [Test]
    public void EveryOriginHasADanishWord()
    {
        foreach (InternalErrorOrigin origin in Enum.GetValues<InternalErrorOrigin>())
        {
            Assert.That(InternalErrorViewModel.OriginText(origin), Is.Not.Empty);
        }
    }

    /// <summary>
    /// The dismissal contract: Luk is LAST in the tree, is the cancel button so Escape closes, and is the default
    /// so Enter does too. Asserted on the tree order rather than on a coordinate, because "last" is what the
    /// contract actually says.
    /// </summary>
    [AvaloniaTest]
    public void LukIsTheLastButtonAndClosesOnEscape()
    {
        InternalErrorWindow window = new(Fault());

        Button close = window.GetLogicalDescendants().OfType<Button>().Last();
        Assert.Multiple(() =>
        {
            Assert.That(close.Content, Is.EqualTo("Luk"));
            Assert.That(close.IsCancel, Is.True);
            Assert.That(close.IsDefault, Is.True);
        });
    }

    /// <summary>
    /// Resizable, which is the whole reason this is its own window rather than a <c>ShowButtonsAsync</c> call: a
    /// captured detail is 20+ long lines and a fixed, size-to-content dialog would clip it or grow off-screen.
    /// </summary>
    [AvaloniaTest]
    public void TheWindowIsResizable()
    {
        InternalErrorWindow window = new(Fault());

        Assert.Multiple(() =>
        {
            Assert.That(window.CanResize, Is.True);
            Assert.That(window.SizeToContent, Is.EqualTo(SizeToContent.Manual),
                "sized by the user, not by its content");
        });
    }

    /// <summary>Every visible piece of chrome is Danish; only the captured payload is technical text.</summary>
    [AvaloniaTest]
    public void TheChromeIsDanish()
    {
        InternalErrorWindow window = new(Fault());

        Assert.Multiple(() =>
        {
            Assert.That(window.Title, Is.EqualTo("Intern fejl"));
            Assert.That(Find<TextBlock>(window, "DetailLabel").Text, Is.EqualTo("Teknisk detalje"));
        });
    }

    /// <summary>The readout is selectable and read-only: it exists to be lifted out, never edited.</summary>
    [AvaloniaTest]
    public void TheReadoutIsReadOnly()
    {
        InternalErrorWindow window = new(Fault());

        Assert.That(Find<TextBox>(window, "DetailText").IsReadOnly, Is.True);
    }

    /// <summary>
    /// The port every caller reaches this through. <see cref="NullDialogService"/> is production code, so the
    /// interface growing a member is a compile-time obligation on it — this pins that it answers rather than
    /// throwing.
    /// </summary>
    [Test]
    public void TheNullPortAnswersWithoutShowingAnything()
    {
        Assert.DoesNotThrowAsync(async () => await new NullDialogService().ShowInternalErrorAsync(Fault()));
    }

    // ── The copy button ─────────────────────────────────────────────────────────────────────────────────────

    private static InternalErrorViewModel Model(InternalError? error = null) =>
        new(error ?? Fault(), "1.2.3");

    /// <summary>
    /// The payload carries every fact the design names. Asserted as a SET rather than as one expected
    /// string: the order is a presentation choice that may reasonably change, while a missing fact is a bug
    /// report someone cannot act on.
    /// </summary>
    [Test]
    public void ThePayloadCarriesEveryFactABugReportNeeds()
    {
        string payload = Model().Payload;

        Assert.Multiple(() =>
        {
            Assert.That(payload, Does.Contain("internal.rule-failed"), "the code");
            Assert.That(payload, Does.Contain(Sentence), "the Danish sentence");
            Assert.That(payload, Does.Contain("Rule 'name-empty' threw"), "the English diagnostic");
            Assert.That(payload, Does.Contain("SDK"), "the origin");
            Assert.That(payload, Does.Contain("1970-01-01"), "the timestamp");
            Assert.That(payload, Does.Contain("1.2.3"), "the app version");
            Assert.That(payload, Does.Contain("at Rule()"), "and the captured detail");
        });
    }

    /// <summary>
    /// THE POINT OF PUTTING THE PAYLOAD ON THE VIEW-MODEL: what is copied is what is shown. Held against the
    /// window's own rendered controls, so the two cannot drift — which they would the first time either changed
    /// if the copy text were assembled beside the clipboard call.
    /// </summary>
    [AvaloniaTest]
    public void WhatIsCopiedIsWhatTheDialogShows()
    {
        InternalErrorViewModel model = Model();
        InternalErrorWindow window = new();
        window.Show(model);

        string payload = model.Payload;
        Assert.Multiple(() =>
        {
            Assert.That(payload, Does.Contain(Find<TextBlock>(window, "Sentence").Text!));
            Assert.That(payload, Does.Contain(Find<TextBlock>(window, "Identity").Text!.Split(' ')[0]),
                "the identity's code, as rendered");
            Assert.That(payload, Does.Contain(Find<TextBox>(window, "DetailText").Text!),
                "and the whole readout, verbatim");
        });
    }

    /// <summary>The button reads Kopiér at rest and flips to Kopieret once the copy has happened.</summary>
    [Test]
    public void TheButtonFlipsToKopieretOnSuccess()
    {
        InternalErrorViewModel model = Model();
        Assert.That(model.CopyText, Is.EqualTo("Kopiér"));

        model.MarkCopied();

        Assert.That(model.CopyText, Is.EqualTo("Kopieret"));
    }

    /// <summary>
    /// No clipboard is a CODED refusal, said in place. Asserted against the catalogue entry rather than a
    /// literal, so the button and the catalogue cannot disagree about what the refusal says.
    /// </summary>
    [Test]
    public void NoClipboardIsReportedInPlaceAsTheCodedRefusal()
    {
        InternalErrorViewModel model = Model();

        model.MarkCopyUnavailable();

        Assert.Multiple(() =>
        {
            Assert.That(model.CopyText, Is.EqualTo(HostProblems.ClipboardUnavailable().Message));
            Assert.That(model.CopyText, Is.Not.EqualTo("Kopieret"), "and never reads as success");
        });
    }

    /// <summary>
    /// The whole copy path end to end through the SHIPPED handler: a real click on the real button puts the
    /// assembled payload on the real clipboard, and the bound label follows.
    /// </summary>
    /// <remarks>
    /// The headless platform DOES supply a clipboard — measured, not assumed; this test was first written for
    /// the refusal branch and the copy simply succeeded. So the success path is what a live click can prove, and
    /// the no-clipboard branch is proven on the view-model, which is the only layer that can be put in that
    /// state deliberately.
    /// </remarks>
    [AvaloniaTest]
    public async Task ClickingCopyPutsTheAssembledPayloadOnTheClipboard()
    {
        InternalErrorViewModel model = Model();
        InternalErrorWindow window = new();
        window.Show(model);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Button copy = window.GetLogicalDescendants().OfType<Button>()
            .First(b => Avalonia.Automation.AutomationProperties.GetAutomationId(b) == "CopyButton");
        copy.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        string? onClipboard = await TopLevel.GetTopLevel(window)!.Clipboard!.TryGetTextAsync();
        Assert.Multiple(() =>
        {
            Assert.That(onClipboard, Is.EqualTo(model.Payload),
                "the platform received exactly what the view-model assembled");
            Assert.That(model.CopyText, Is.EqualTo("Kopieret"));
            Assert.That(copy.Content, Is.EqualTo("Kopieret"),
                "and the BOUND label followed it, which is what the reader actually sees");
        });
        window.Close();
    }

    /// <summary>Kopiér comes before Luk: the reader copies, then closes, and Luk stays last.</summary>
    [AvaloniaTest]
    public void KopierComesBeforeLuk()
    {
        InternalErrorWindow window = new(Fault());

        var labels = window.GetLogicalDescendants().OfType<Button>()
            .Select(b => Avalonia.Automation.AutomationProperties.GetAutomationId(b)).ToList();

        Assert.That(labels, Is.EqualTo(new[] { "CopyButton", "CloseButton" }));
    }
}
