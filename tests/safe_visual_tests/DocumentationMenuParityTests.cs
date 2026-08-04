using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The Dokumentation menu's SHAPE, pinned against the reference application (measured 2026-08-04 on
/// <c>g10 4-10-2025</c> via the ihcvisual MCP driver's passive <c>menu.dumpBar</c>). The vendor's menu is exactly:
/// <code>
///   &amp;Datalinie moduler...          id 24587
///   &amp;Projektinfo...                id 30501
///   Rapporter...                       id 30502
///   &amp;Rediger data tabeller...      id 30506
/// </code>
/// <para>
/// Two deviations this catches. First, OpenVisual listed the vendor's three surviving items in its own order
/// (Projektinfo, Rediger data tabeller, Datalinie moduler) rather than the vendor's — an installer who knows the
/// vendor menu reaches for the wrong row. Second, the bar title carried the mnemonic <c>Dok_umentation</c> while
/// the vendor's is <c>&amp;Dokumentation</c>; <c>D</c> is unclaimed by every other bar title, so the vendor's
/// access key was given away for nothing.
/// </para>
/// <para>
/// <b>Rapporter is deliberately different</b> and is the one item NOT held to vendor parity: OpenVisual replaces
/// the vendor's single report dialog with three named report entries (US-040). Following the Bibliotek precedent
/// (<see cref="LibraryMenuParityTests"/>), what the vendor does not have lives BELOW a separator rather than
/// interleaved with the vendor's block — so the vendor's three keep their own contiguous, vendor-ordered region
/// and a future extension drifting up into it fails here.
/// </para>
/// </summary>
public class DocumentationMenuParityTests
{
    // Items in document order; "---" stands for a Separator. The vendor's three come first, in the vendor's order.
    private static readonly string[] Expected =
    [
        "_Datalinie moduler…",
        "_Projektinfo…",
        "_Rediger data tabeller…",
        "---",
        "_Funktionsdokumentation…",
        "_Installationsdokumentation…",
        "Functions_blok dokumentation…",
    ];

    [Test]
    public void DokumentationMenu_CarriesTheVendorsThreeItemsInVendorOrder_ThenTheReportsBelowASeparator()
    {
        Assert.That(ReadMenuItems("MenuDocumentation", "MenuHelp"), Is.EqualTo(Expected));
    }

    /// <summary>The bar title takes the vendor's own access key. No other bar title claims D.</summary>
    [Test]
    public void DokumentationTitle_TakesTheVendorsDAccessKey()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Xaml(), Does.Contain(
                "Header=\"_Dokumentation\" a:AutomationProperties.AutomationId=\"MenuDocumentation\""),
                "the vendor's title is &Dokumentation — D, not the u of Dok_umentation");
            Assert.That(BarTitleAccessKeys().Where(k => k == 'D').ToArray(), Has.Length.EqualTo(1),
                "…which is free to take: no other bar title claims D");
        });
    }

    /// <summary>Within the popup, every access key is distinct — Functionsblok takes <c>b</c> precisely because
    /// Datalinie moduler holds the vendor's <c>D</c>.</summary>
    [Test]
    public void DokumentationItems_CarryDistinctAccessKeys()
    {
        char[] keys = ReadMenuItems("MenuDocumentation", "MenuHelp")
            .Where(h => h != "---")
            .Select(AccessKey)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(keys, Has.None.EqualTo('\0'), "every item declares an access key");
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Length),
                $"…and no two collide — got [{new string(keys)}]");
        });
    }

    /// <summary>The letter after the first '_' , lower-cased; '\0' when the header declares none.</summary>
    private static char AccessKey(string header)
    {
        int i = header.IndexOf('_');
        return i >= 0 && i + 1 < header.Length ? char.ToLowerInvariant(header[i + 1]) : '\0';
    }

    private static IEnumerable<char> BarTitleAccessKeys() =>
        Regex.Matches(Xaml(), @"Header=""(?<h>[^""]*)"" a:AutomationProperties.AutomationId=""Menu[^""]*""")
            .Select(m => char.ToUpperInvariant(AccessKey(m.Groups["h"].Value)));

    private static string Xaml() =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "appxaml", "MainWindow.axaml"));

    /// <summary>The markup of one bar menu: from its own AutomationId to the start of the NEXT menu's item tag, so
    /// it survives edits either side of it. See <see cref="LibraryMenuParityTests"/> for why the end backs up to
    /// the next item's opening tag rather than slicing at the id.</summary>
    private static string Region(string automationId, string nextAutomationId)
    {
        string xaml = Xaml();
        int start = xaml.IndexOf($"AutomationId=\"{automationId}\"", StringComparison.Ordinal);
        int nextId = xaml.IndexOf($"AutomationId=\"{nextAutomationId}\"", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"{automationId} not found in the markup");
        Assert.That(nextId, Is.GreaterThan(start), $"{nextAutomationId} must follow {automationId}");
        int end = xaml.LastIndexOf("<MenuItem", nextId, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(start), "the next menu's opening tag must follow this menu's id");
        return xaml[start..end];
    }

    private static IReadOnlyList<string> ReadMenuItems(string automationId, string nextAutomationId) =>
        Regex.Matches(Region(automationId, nextAutomationId), @"<Separator\s*/>|Header=""(?<h>[^""]*)""")
            .Select(m => m.Value.StartsWith("<Separator", StringComparison.Ordinal) ? "---" : m.Groups["h"].Value)
            .ToList();
}
