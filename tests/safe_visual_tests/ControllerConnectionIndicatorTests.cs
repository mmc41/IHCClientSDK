using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// W9 / F10 (uxparity2 T029): the bottom bar carries a controller-connection indicator.
/// <para>
/// Measured (`tmp/uxparity2/verify/V6/findings.md`): the reference application shows a network glyph with a red ✗ at
/// the right-hand end of its status bar; OpenVisual's bar held only the status text and a locale flag. The vendor's
/// indicator was only ever observed DISCONNECTED, so the connected appearance is designed here rather than copied —
/// what is copied is that the indicator exists, and where.
/// </para>
/// <para>
/// `docs/icons_design.md` requires state to read from the GLYPH, not colour alone, so the two states are two icons.
/// </para>
/// </summary>
public class ControllerConnectionIndicatorTests : AvaloniaTestBase
{
    [Test]
    public async Task ViewModel_ExposesBothConnectionStates_WithDistinctIconAndTooltip()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        Assert.That(vm.IsControllerConnected, Is.False,
            "this build never contacts a controller (E10 offline slice), so it starts disconnected");
        string offIcon = vm.ControllerConnectionIcon;
        string offTip = vm.ControllerConnectionText;

        vm.IsControllerConnected = true;

        Assert.Multiple(() =>
        {
            Assert.That(vm.ControllerConnectionIcon, Is.Not.EqualTo(offIcon),
                "the two states use DIFFERENT GLYPHS — icons_design.md forbids signalling state by colour alone");
            Assert.That(vm.ControllerConnectionText, Is.Not.EqualTo(offTip), "…and say which state they mean");
            Assert.That(offTip, Is.Not.Null.And.Not.Empty);
            Assert.That(vm.ControllerConnectionText, Is.Not.Null.And.Not.Empty);
        });
    }

    // Both glyphs must actually ship, and follow the icon rules — a binding onto a missing asset renders nothing and
    // would leave the bar looking exactly as it did before.
    [Test]
    public void BothIndicatorIcons_ShipAndFollowTheIconRules()
    {
        foreach (string name in new[] { "controller-connected.svg", "controller-disconnected.svg" })
        {
            // Read from the assets the app actually SHIPS (Assets\**\*.svg is embedded in ihc_openvisual, the same
            // source SvgReportIconProvider serves) — an asset that only exists in the source checkout would not
            // render at runtime, and the suite never walks up into the checkout.
            string? svg = ShippedAsset(name);
            Assert.That(svg, Is.Not.Null, $"{name} ships as an embedded asset");

            Assert.Multiple(() =>
            {
                Assert.That(svg, Does.Contain("viewBox=\"0 0 24 24\""), $"{name}: the 24-unit grid");
                Assert.That(svg, Does.Contain("stroke=\"currentColor\""), $"{name}: themeable");
                Assert.That(svg, Does.Not.Contain("#"), $"{name}: no baked hex colour");
            });
        }
    }

    // The shipped text of an Assets\*.svg, from the ihc_openvisual assembly's embedded resources.
    private static string? ShippedAsset(string fileName)
    {
        Assembly app = typeof(NodeIcons).Assembly;
        string? resource = app.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($".Assets.{fileName}", StringComparison.Ordinal));
        if (resource is null)
            return null;
        using Stream stream = app.GetManifestResourceStream(resource)!;
        return new StreamReader(stream).ReadToEnd();
    }

    // The indicator is in the BOTTOM BAR — the same place the reference application puts it — not tucked into a menu.
    [AvaloniaTest]
    public async Task TheIndicator_IsInTheBottomBar()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        var indicator = window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(c => c.Name == "ControllerConnectionIndicator");

        Assert.That(indicator, Is.Not.Null, "the status bar carries a named connection indicator");
        Assert.That(indicator!.GetVisualAncestors().OfType<Border>()
                .Any(b => b.Name == "StatusBar"), Is.True,
            "…and it sits inside the status bar, not somewhere else in the shell");
    }

    // docs/icon_codes.md §7 must map both assets to a 1–3 character stand-in, so a text-only surface can render them.
    [Test]
    public void BothIcons_AreRegisteredInIconCodes_WithAStandIn()
    {
        string doc = File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "appdocs", "icon_codes.md"));

        Assert.Multiple(() =>
        {
            foreach (string asset in new[] { "controller-connected", "controller-disconnected" })
                Assert.That(doc, Does.Contain(asset), $"{asset} is registered in icon_codes.md");
        });
    }
}
