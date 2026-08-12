using System;
using System.Linq;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-38, generalized: EVERY captioned group in EVERY dialog must reach automation, not just the one
/// dialog the defect was first measured on.
///
/// <para>The defect is structural, not per-dialog: Avalonia has no GroupBox, so these dialogs draw one with a
/// <see cref="HeaderedContentControl"/> whose template holds a caption TextBlock — and that control's default peer
/// reports <see cref="AutomationControlType.None"/>, so the group never enters the automation tree and its caption
/// reaches nothing. Four dialogs use the pattern, so fixing the one where it was noticed would have left three
/// carrying the same defect.</para>
///
/// <para>The enum manager shows why it is not cosmetic: its values pane is captioned
/// <c>Enumerator værdier - &lt;selected type&gt;</c> — the reference application does the same, verified live by
/// switching type — and that caption is the ONLY thing saying which type's values are on screen. The two panes are
/// otherwise identical lists side by side.</para>
/// </summary>
public class GroupCaptionAccessibilityTests : AvaloniaTestBase
{
    private static Window New(string dialog) => dialog switch
    {
        nameof(ProjectInfoWindow) => new ProjectInfoWindow(),
        nameof(EnumTypeManagerWindow) => new EnumTypeManagerWindow(),
        nameof(ModuleMapWindow) => new ModuleMapWindow(),
        nameof(NamePromptWindow) => new NamePromptWindow(),
        _ => throw new ArgumentOutOfRangeException(nameof(dialog), dialog, "unknown dialog"),
    };

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    [TestCase(nameof(ProjectInfoWindow))]
    [TestCase(nameof(EnumTypeManagerWindow))]
    [TestCase(nameof(ModuleMapWindow))]
    [TestCase(nameof(NamePromptWindow))]
    public void EveryCaptionedGroup_AppearsInTheTree_AndAnnouncesItsCaption(string dialog)
    {
        Window window = New(dialog);
        CurrentTestWindow = window;
        window.Show();

        var groups = window.GetVisualDescendants().OfType<HeaderedContentControl>()
            // Every group that HAS a caption, not just the ones carrying the styling class: NamePromptWindow's
            // "Navn" group is captioned without it, and the rule is about publishing a caption, not about a class.
            .Where(g => g.Header is string).ToList();

        Assert.That(groups, Is.Not.Empty, $"{dialog} draws at least one captioned group");
        Assert.Multiple(() =>
        {
            foreach (HeaderedContentControl group in groups)
            {
                AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(group);
                Assert.That(peer.GetAutomationControlType(), Is.Not.EqualTo(AutomationControlType.None),
                    $"{dialog}: the '{group.Header}' group must be IN the tree — a ControlType.None peer is not "
                    + "walked, so any name on it reaches nothing (the F-11 Separator lesson)");
                Assert.That(peer.GetName(), Is.EqualTo(group.Header as string),
                    $"{dialog}: the '{group.Header}' group must announce its own caption");

                // A group that claims the `group` class must also be SEEN as one, not merely announced.
                // That class exists to apply a Template wrapping the content in a bordered box, because the
                // vendor draws one -- but the style selector naming the SUBCLASS never matched, since
                // AccessibleGroupBox sets StyleKeyOverride to HeaderedContentControl and a XAML type
                // selector matches the style KEY. The setter was silently ignored in FOUR dialogs at once,
                // the stock theme went on drawing the header, and the result was a caption floating over
                // unboxed fields. Found on product 002's vendor composite (T036); asserted here so it
                // cannot come back in any dialog.
                //
                // Scoped to the class, not to every captioned group: NamePromptWindow's 'Navn' group is
                // captioned WITHOUT it (see the walk above), and that is a deliberate shape rather than a
                // missing frame -- the class is what promises a box.
                if (!group.Classes.Contains("group")) { continue; }

                Border? frame = group.GetVisualDescendants().OfType<Border>()
                    .FirstOrDefault(b => b.Child is ContentPresenter);
                Assert.That(frame, Is.Not.Null,
                    $"{dialog}: the '{group.Header}' group must DRAW its box, not just announce it");
                Assert.That(frame?.BorderThickness.Top, Is.GreaterThan(0),
                    $"{dialog}: the '{group.Header}' group's frame must have a thickness that draws");
                Assert.That(frame?.BorderBrush, Is.Not.Null,
                    $"{dialog}: the '{group.Header}' group's BorderBrush must resolve");
            }
        });
    }

    /// <summary>The enum manager's values caption is DYNAMIC — it names the selected type, as the vendor's does.
    /// A group that only announced a static caption would pass the test above and still leave the installer unable
    /// to tell which type's values they are editing.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void EnumManager_ValuesGroup_AnnouncesTheSelectedType()
    {
        var window = new EnumTypeManagerWindow();
        CurrentTestWindow = window;
        window.Show();

        var values = window.GetVisualDescendants().OfType<HeaderedContentControl>()
            .Single(g => g.Name == "ValuesGroup");
        values.Header = "Enumerator værdier - Persienne tilstand [read only]";

        Assert.That(ControlAutomationPeer.CreatePeerForElement(values).GetName(),
            Is.EqualTo("Enumerator værdier - Persienne tilstand [read only]"),
            "the caption follows the selected type, so its announced name must follow too");
    }
}
