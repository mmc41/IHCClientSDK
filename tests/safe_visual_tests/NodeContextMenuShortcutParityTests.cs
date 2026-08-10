using System;
using System.IO;
using NUnit.Framework;

namespace safe_visual_tests;

public class NodeContextMenuShortcutParityTests
{
    [Test]
    public void ContextMenuAdvertisesNoKeyboardShortcutsWhileMenuBarStillDoes()
    {
        string xaml = File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "appxaml", "MainWindow.axaml"));

        Assert.Multiple(() =>
        {
            Assert.That(NodeContextMenu(xaml), Does.Not.Contain("InputGesture="),
                "the vendor's node context menus show no shortcut text");
            Assert.That(ItemTag(xaml, "node.saveBlock"), Does.Contain("InputGesture="));
            Assert.That(ItemTag(xaml, "edit.cut"), Does.Contain("InputGesture="));
            Assert.That(ItemTag(xaml, "edit.copy"), Does.Contain("InputGesture="));
            Assert.That(ItemTag(xaml, "edit.paste"), Does.Contain("InputGesture="));
            Assert.That(ItemTag(xaml, "link.jumpOpposite"), Does.Contain("InputGesture=\"F4\""));
            Assert.That(ItemTag(xaml, "insert.emptyFunctionBlock"), Does.Contain("InputGesture="));
            Assert.That(ItemTag(xaml, "view.showProgram"), Does.Contain("InputGesture=\"F3\""));
        });
    }

    private static string NodeContextMenu(string xaml)
    {
        int start = xaml.IndexOf("<MenuFlyout x:Key=\"NodeContextMenu\"", StringComparison.Ordinal);
        int end = xaml.IndexOf("</MenuFlyout>", start, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        Assert.That(end, Is.GreaterThan(start));
        return xaml[start..(end + "</MenuFlyout>".Length)];
    }

    private static string ItemTag(string xaml, string automationId)
    {
        int id = xaml.IndexOf($"AutomationId=\"{automationId}\"", StringComparison.Ordinal);
        Assert.That(id, Is.GreaterThanOrEqualTo(0), $"{automationId} not found in the shell markup");
        int start = xaml.LastIndexOf("<controls:AccessibleMenuItem", id, StringComparison.Ordinal);
        int end = xaml.IndexOf("/>", id, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        Assert.That(end, Is.GreaterThan(id));
        return xaml[start..(end + 2)];
    }
}
