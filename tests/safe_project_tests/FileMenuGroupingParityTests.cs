using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// Alignment F-12 — the <b>Filer</b> menu's GROUPING, measured live 2026-08-11 on a fresh unnamed project via the
/// reference application's passive <c>menu.dumpBar</c>:
/// <code>
///   &amp;Nyt projekt      Ctrl+N   57600
///   Å&amp;bn projekt...   Ctrl+O   57601
///   &amp;Gem projekt      Ctrl+S   57603
///   Gem projekt &amp;som...          57604
///   ────────────────────────────────────
///   &amp;1…&amp;4  (the four MRU paths)   57616–57619
///   ────────────────────────────────────
///   &amp;Luk                          57665
/// </code>
/// <para>Three groups: <i>the file commands</i>, <i>the recent list</i>, <i>closing</i>. OpenVisual had four, and
/// closed the project BEFORE offering the recent list rather than after — so an installer who knows the vendor's
/// menu reaches past <i>Luk projekt</i> for something that is above it.</para>
///
/// <para>Two differences here are registered and are NOT what this pins: the recent list is a <i>submenu</i> where
/// the vendor inlines four numbered paths, and OpenVisual splits the vendor's single <i>Luk</i> into
/// <i>Luk projekt</i> + <i>Afslut</i>. Both keep their registered form — what changes is only WHERE they sit, and
/// the split pair lands together in the vendor's own third group, which is where closing belongs.</para>
///
/// <para>This became visible only once F-11 made OpenVisual's separators automation-readable, and comparable only
/// once F-46 stopped a phantom separator leaking into every dump. Grouping is the last thing a menu comparison
/// gets to see and the easiest to get wrong.</para>
/// </summary>
public class FileMenuGroupingParityTests
{
    // Document order; "---" stands for a separator. Three groups, mapping one-to-one onto the vendor's three.
    private static readonly string[] Expected =
    [
        "_Nyt projekt",
        "Å_bn projekt…",
        "_Gem projekt",
        "Gem projekt _som…",
        "---",
        "Seneste _projekter",
        "---",
        "_Luk projekt",
        "_Afslut",
    ];

    [Test]
    public void FilerMenu_GroupsAsTheReferenceApplicationDoes()
    {
        Assert.That(ReadMenuItems("MenuFile", "MenuEdit"), Is.EqualTo(Expected));
    }

    /// <summary>Exactly two rules, so the menu has the vendor's THREE groups. Pinned separately from the order
    /// above because a count is what a reviewer checks first, and because the failure it guards against —
    /// a group appearing or vanishing — reads very differently from an item moving.</summary>
    [Test]
    public void FilerMenu_DrawsTheVendorsTwoRules()
    {
        Assert.That(ReadMenuItems("MenuFile", "MenuEdit").Count(h => h == "---"), Is.EqualTo(2),
            "the reference application's Filer menu is three groups, so two rules");
    }

    /// <summary>The closing pair must be LAST and adjacent: the vendor's single Luk closes the menu, and
    /// OpenVisual's registered split of it into two items only stays faithful while the two stay together at the
    /// end. This is the half F-12 actually got wrong.</summary>
    [Test]
    public void TheClosingCommands_EndTheMenuTogether()
    {
        List<string> items = [.. ReadMenuItems("MenuFile", "MenuEdit")];

        Assert.Multiple(() =>
        {
            Assert.That(items[^2], Is.EqualTo("_Luk projekt"));
            Assert.That(items[^1], Is.EqualTo("_Afslut"));
            Assert.That(items.IndexOf("Seneste _projekter"), Is.LessThan(items.IndexOf("_Luk projekt")),
                "the recent list comes BEFORE closing, as the vendor's MRU block does");
        });
    }

    private static string Xaml() =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "appxaml", "MainWindow.axaml"));

    /// <summary>The markup of one bar menu: from its own AutomationId to the start of the NEXT menu's item tag,
    /// so it survives edits either side of it. Same shape as <see cref="DocumentationMenuParityTests"/>.</summary>
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

    // Headers and separators in document order. The recent list's ItemTemplate/Styles carry no Header attribute,
    // so the generated entries do not appear here — which is what makes the submenu read as the single row it is.
    private static IReadOnlyList<string> ReadMenuItems(string automationId, string nextAutomationId) =>
        Regex.Matches(Region(automationId, nextAutomationId), @"<(?:controls:)?(?:Accessible)?Separator\s*/>|Header=""(?<h>[^""]*)""")
            .Select(m => m.Value.Contains("Separator", StringComparison.Ordinal) ? "---" : m.Groups["h"].Value)
            .ToList();
}
