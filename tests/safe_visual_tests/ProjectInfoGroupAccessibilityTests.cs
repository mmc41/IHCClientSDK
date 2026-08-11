using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-38 (tmp/align-campaign-2026-08-10.md): the Projektinfo dialog's three group captions must reach
/// AUTOMATION, not only the screen.
///
/// <para>Measured 2026-08-11: the reference application draws three captioned group boxes — <c>Projekt
/// oplysninger</c>, <c>Installatør information</c>, <c>Kunde oplysninger</c> — and exposes each caption as a
/// control. OpenVisual renders all three (the rendered dialog shows them) but the automation inventory listed
/// <b>61 controls without any of them</b>: the caption is a <c>TextBlock</c> inside the
/// <c>HeaderedContentControl</c>'s control TEMPLATE, which does not reach the content view.</para>
///
/// <para>That matters more here than almost anywhere else in the app. The installer block and the customer block
/// hold the <b>same eight field labels</b> — Navn, Vej, Telefon, Postnummer, Mobil telefon, By, Email, Land — so
/// the caption is the ONLY thing that says which party a field belongs to. Without it a screen-reader user hears
/// "Navn, Vej, Telefon…" twice and cannot tell the installer's address from the customer's.</para>
///
/// <para>Same defect family as the menu separators (F-11) and the block dialog's missing user-properties caption
/// (F-24): rendered grouping that carries meaning and publishes none of it.</para>
/// </summary>
public class ProjectInfoGroupAccessibilityTests : AvaloniaTestBase
{
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void EveryGroupPublishesItsCaptionToAutomation()
    {
        var window = new ProjectInfoWindow();
        CurrentTestWindow = window;
        window.Show();

        var groups = window.GetVisualDescendants().OfType<HeaderedContentControl>()
            .Where(g => g.Classes.Contains("group")).ToList();

        Assert.That(groups, Has.Count.EqualTo(3), "the vendor draws three captioned groups");
        Assert.Multiple(() =>
        {
            foreach (HeaderedContentControl group in groups)
            {
                // The PEER's name, not AutomationProperties.GetName: the peer is what an automation client reads,
                // and the attached property is only one of the ways to feed it. Asserting the property instead
                // would pass on a control whose peer never reaches the tree — which is exactly the state this
                // dialog was in.
                var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(group);
                Assert.That(peer.GetName(), Is.EqualTo(group.Header as string),
                    $"the '{group.Header}' group must announce its own caption — the two contact groups carry "
                    + "identical field labels, so the caption is the only thing telling them apart");
            }
        });
    }

    /// <summary>A name is only half of it: the control must also APPEAR in the automation tree. Avalonia's
    /// default peer decides that, and turn 4 already met a control (Separator) whose peer reported
    /// <c>ControlType.None</c> and so vanished from the tree entirely — a name on such a control publishes
    /// nothing. This pins the peer's type, which is what the live inventory actually walks.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void EveryGroupAppearsInTheAutomationTree()
    {
        var window = new ProjectInfoWindow();
        CurrentTestWindow = window;
        window.Show();

        var groups = window.GetVisualDescendants().OfType<HeaderedContentControl>()
            .Where(g => g.Classes.Contains("group")).ToList();

        Assert.Multiple(() =>
        {
            foreach (HeaderedContentControl group in groups)
            {
                var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(group);
                Assert.That(peer, Is.Not.Null, $"'{group.Header}' must have an automation peer at all");
                Assert.That(peer!.GetAutomationControlType(),
                    Is.Not.EqualTo(Avalonia.Automation.Peers.AutomationControlType.None),
                    $"'{group.Header}' must appear in the tree — a ControlType.None peer is not walked, so its "
                    + "name reaches nothing (the F-11 Separator lesson)");
            }
        });
    }

    /// <summary>The captions themselves, so a rename cannot quietly drop one of the vendor's three.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheThreeCaptionsAreTheVendorsOwn()
    {
        var window = new ProjectInfoWindow();
        CurrentTestWindow = window;
        window.Show();

        var captions = window.GetVisualDescendants().OfType<HeaderedContentControl>()
            .Where(g => g.Classes.Contains("group")).Select(g => g.Header as string).ToList();

        Assert.That(captions,
            Is.EqualTo(new[] { "Projekt oplysninger", "Installatør information", "Kunde oplysninger" }),
            "measured on the reference application's own dialog, in its order");
    }
}
