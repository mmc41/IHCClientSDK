using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The Bibliotek menu's SHAPE, pinned against the reference application (measured 2026-08-04 on
/// <c>g10 4-10-2025</c> via the ihcvisual MCP driver's passive <c>menu.dumpBar</c>). The vendor's menu is exactly:
/// <code>
///   &amp;Rediger Enumerator typer            id 24588
///   &amp;Gem Funktionsblok...   Ctrl+G       id 24765
///   &amp;Oplås                               id 24766
/// </code>
/// <para>
/// Two things this catches. First, OpenVisual used to LEAD Bibliotek with an "Indsæt Funktionsblok" submenu — a
/// second copy of <i>Indsæt ▸ FunktionsBlokke</i> that the vendor does not have here and that displaced the
/// vendor's first item. Second, <i>Gem Funktionsblok</i> and <i>Oplås</i> were missing from the bar entirely
/// (context-flyout only), so two of the vendor's three items had no bar route at all.
/// </para>
/// <para>
/// The two <i>Importer katalog…</i> entries are OpenVisual's own: the vendor has no counterpart, so they are kept
/// BELOW a separator rather than interleaved with the vendor's three. That placement is asserted too — an
/// extension that drifts up into the vendor's block is the same defect as a missing item.
/// </para>
/// </summary>
public class LibraryMenuParityTests
{
    // Items in document order; "---" stands for a Separator. The vendor's three come first, in the vendor's order.
    private static readonly string[] Expected =
    [
        "_Rediger Enumerator typer",
        "_Gem Funktionsblok…",
        "_Oplås",
        "---",
        "_Importer katalogfil…",
        "Importer katalog_mappe…",
    ];

    [Test]
    public void BibliotekMenu_CarriesTheVendorsThreeItemsInOrder_ThenOurExtensionsBelowASeparator()
    {
        Assert.That(ReadMenuItems("MenuLibrary", "MenuController"), Is.EqualTo(Expected));
    }

    // The submenu the vendor DOES have — under Indsæt, where it belongs and where the automation registry's
    // fb.insertTemplate / catalog.functionBlocks rows now address it. (The node context flyout offers it too,
    // which is correct and is why this counts within the BAR menus rather than across the whole markup.)
    [Test]
    public void TheFunctionBlockCatalog_IsOnIndsæt_AndNotOnBibliotek()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReadMenuItems("MenuInsert", "MenuLibrary"), Does.Contain("_FunktionsBlokke"),
                "the vendor's Indsæt ▸ FunktionsBlokke");
            Assert.That(CatalogBindingsIn("MenuInsert", "MenuLibrary"), Is.EqualTo(1),
                "…bound exactly once there");
            Assert.That(CatalogBindingsIn("MenuLibrary", "MenuController"), Is.Zero,
                "and NOT a second time on Bibliotek — that duplicate is what this test exists to stop");
        });
    }

    private static int CatalogBindingsIn(string automationId, string nextAutomationId) =>
        Regex.Matches(Region(automationId, nextAutomationId), @"ItemsSource=""\{Binding FunctionBlocksMenu\}""").Count;

    private static string Xaml() =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "appxaml", "MainWindow.axaml"));

    /// <summary>The markup of one bar menu: from its own AutomationId to the start of the NEXT menu's item tag, so
    /// it survives edits either side of it. The end backs up from the next AutomationId to that item's opening
    /// <c>&lt;MenuItem</c> — the two attributes sit in ONE tag, and slicing at the id alone would swallow the next
    /// menu's own Header (which is written before it) and report it as a Bibliotek item.</summary>
    private static string Region(string automationId, string nextAutomationId)
    {
        string xaml = Xaml();
        int start = xaml.IndexOf($"AutomationId=\"{automationId}\"", StringComparison.Ordinal);
        int nextId = xaml.IndexOf($"AutomationId=\"{nextAutomationId}\"", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"{automationId} not found in the markup");
        Assert.That(nextId, Is.GreaterThan(start), $"{nextAutomationId} must follow {automationId}");
        int end = xaml.LastIndexOf("<controls:AccessibleMenuItem", nextId, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(start), "the next menu's opening tag must follow this menu's id");
        return xaml[start..end];
    }

    /// <summary>The children of one bar menu, in document order, as their Header text ("---" per Separator). Nested
    /// items (a submenu's leaves) would show up here too — which is fine: the point is that Bibliotek has no
    /// submenu at all.</summary>
    private static IReadOnlyList<string> ReadMenuItems(string automationId, string nextAutomationId) =>
        Regex.Matches(Region(automationId, nextAutomationId), @"<(?:controls:)?(?:Accessible)?Separator\s*/>|Header=""(?<h>[^""]*)""")
            .Select(m => m.Value.EndsWith("Separator/>", StringComparison.Ordinal) || m.Value.EndsWith("Separator />", StringComparison.Ordinal) ? "---" : m.Groups["h"].Value)
            .ToList();
}
