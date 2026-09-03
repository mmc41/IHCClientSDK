using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// Alignment F-18 — <i>save this block to the library</i> is ONE command (the reference application's id
/// <c>24765</c>, Ctrl+G) that it captions <b>differently on its two surfaces</b>. Both measured live
/// 2026-08-11 on an empty unlocked block:
/// <code>
///   Bibliotek menu bar   &amp;Gem Funktionsblok...  Ctrl+G      id 24765
///   node context flyout  &amp;Gem...                            id 24765
/// </code>
/// <para>The short form is right where it sits: a flyout acts on the row that was right-clicked, so naming the
/// noun again adds nothing. The bar has no such context and spells it out. OpenVisual used the long form in both
/// places — its bar caption already matched, and only the flyout diverged.</para>
///
/// <para>Both halves are pinned, and the bar half is the point: renaming the shared caption, or "fixing" both
/// occurrences together, would have broken a surface that was already correct. The two labels are measured
/// separately because the reference application sets them separately.</para>
/// </summary>
public class SaveBlockLabelParityTests
{
    /// <summary>The flyout carries the reference application's short caption.</summary>
    [Test]
    public void TheNodeFlyout_CaptionsTheCommandAsTheOriginalDoes()
    {
        Assert.That(HeaderOf("ctx.node.saveBlock"), Is.EqualTo("_Gem…"),
            "the flyout acts on the row that was right-clicked, so the original names no noun there");
    }

    /// <summary>…and the Bibliotek bar keeps the long one. This is the half that guards against over-correcting
    /// a caption that was never wrong.</summary>
    [Test]
    public void TheLibraryMenu_KeepsTheOriginalsLongCaption()
    {
        Assert.That(HeaderOf("node.saveBlock"), Is.EqualTo("_Gem Funktionsblok…"),
            "the bar has no right-clicked row to lend it context, and the original spells the noun out");
    }

    /// <summary>Both keep the original's <c>G</c> access key — it is the letter the original underlines on each
    /// surface, and Ctrl+G is the same command's shortcut.</summary>
    [Test]
    public void BothCaptions_KeepTheOriginalsAccessKey()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HeaderOf("ctx.node.saveBlock"), Does.StartWith("_G"));
            Assert.That(HeaderOf("node.saveBlock"), Does.StartWith("_G"));
        });
    }

    /// <summary>The Header declared on the menu item bearing <paramref name="automationId"/>. Read from the
    /// markup because a caption is authored there; the live flyout is checked separately, against both
    /// applications, when the alignment turn runs.</summary>
    private static string HeaderOf(string automationId)
    {
        string xaml = File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "appxaml", "MainWindow.axaml"));
        Match m = Regex.Match(xaml,
            @"Header=""(?<h>[^""]*)""[^>]*?AutomationId=""" + Regex.Escape(automationId) + @"""",
            RegexOptions.Singleline);
        Assert.That(m.Success, Is.True, $"no menu item declares AutomationId=\"{automationId}\"");
        return m.Groups["h"].Value;
    }
}
