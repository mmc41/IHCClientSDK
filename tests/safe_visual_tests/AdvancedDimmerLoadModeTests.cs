using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment (2026-08-11): the advanced-dimmer "Belastnings karakteristik" combo matches the original IHC Visual —
/// <b>Auto detektion / RC / RL</b>, Auto (the stored default) first — where IHC OpenVisual previously showed
/// Induktiv / Kapacitiv / Auto with Auto last. The stored tokens are the vendor .vis serialization (auto | rc | rl),
/// and the combo order maps 1:1 to them, so index 0 → "auto".
/// </summary>
public class AdvancedDimmerLoadModeTests : AvaloniaTestBase
{
    [Test]
    public void LoadModeTokens_AreTheVendorSerialization_AutoFirst()
    {
        Assert.That(AdvancedDimmerWindow.LoadModes, Is.EqualTo(new[] { "auto", "rc", "rl" }));
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void LoadModeCombo_ShowsVendorLabelsInVendorOrder()
    {
        var window = new AdvancedDimmerWindow();
        CurrentTestWindow = window;
        window.Show();

        var combo = window.FindControl<ComboBox>("LoadModeCombo")!;
        var labels = combo.Items.OfType<ComboBoxItem>().Select(i => i.Content as string).ToList();

        Assert.That(labels, Is.EqualTo(new[] { "Auto detektion", "RC", "RL" }),
            "the load-characteristic combo matches the original: Auto detektion / RC / RL, Auto first");
    }
}
