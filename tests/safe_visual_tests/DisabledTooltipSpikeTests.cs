using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// crudarch T016 SPIKE (decision D03, proposal §3.5): does Avalonia 12 show <c>ToolTip.Tip</c> on a DISABLED
/// control? WPF needs <c>ShowOnDisabled</c>; Avalonia's behaviour was unproven, and T021's design branches on
/// it — tooltip bindings alone (branch A) vs a window-level input handler writing the reason to the status bar
/// (branch B). This test IS the runnable evidence: it hovers a disabled button carrying a tooltip in the
/// headless session and pins whatever the platform actually does.
/// </summary>
public class DisabledTooltipSpikeTests : AvaloniaTestBase
{
    [AvaloniaTest]
    public void DisabledButton_PointerOver_TooltipBehaviourPinned()
    {
        // ARMED CHECK first: the identical probe on an ENABLED control must open the tooltip, proving the
        // headless pointer→ToolTipService pipeline works — otherwise the disabled-case verdict would be vacuous.
        bool enabledOpened = ProbeTooltip(enabled: true);
        Console.WriteLine($"SPIKE armed check: tooltip on ENABLED control opened = {enabledOpened}");
        Assert.That(enabledOpened, Is.True, "probe validity: an enabled control's tooltip must open under hover");

        bool disabledOpened = ProbeTooltip(enabled: false);
        Console.WriteLine($"SPIKE VERDICT: tooltip on DISABLED control opened = {disabledOpened}");

        // MEASURED 2026-08-02 (Avalonia 12 headless): a disabled control's tooltip does NOT open — disabled
        // controls receive no pointer-enter, so ToolTipService never fires. T021 therefore takes BRANCH B:
        // ToolTip.Tip bindings stay for enabled-but-restricted states, and a window-level gesture handler
        // writes the bar Availability.Reason to the status bar for disabled commands (D03 fallback).
        Assert.That(disabledOpened, Is.False,
            "T016 verdict pin: Avalonia does NOT show tooltips on disabled controls -> T021 branch B " +
            "(window-level input handler writes Availability.Reason to the status bar). If the platform " +
            "changes this in an upgrade, revisit T021 — branch A (pure tooltip bindings) becomes available.");
    }

    /// <summary>
    /// The follow-up the first verdict leaves open: if the DISABLED control never receives the pointer, does an
    /// ENABLED ANCESTOR of it? That decides whether a greyed control with no keyboard gesture — the Problemer
    /// panel's export button, which the status-bar channel of T021 branch B cannot reach, because a click on a
    /// disabled button raises no event to hang it on — can show its reason to a sighted mouse user at all.
    /// </summary>
    [AvaloniaTest]
    public void DisabledButton_PointerOver_ParentTooltipBehaviourPinned()
    {
        // Same arming as above, on the same wrapper shape: the parent's tooltip must open over an ENABLED child,
        // or the disabled verdict below would only be saying that this probe's wrapper never works.
        bool overEnabledChild = ProbeParentTooltip(childEnabled: true);
        Console.WriteLine($"SPIKE armed check: parent tooltip over an ENABLED child opened = {overEnabledChild}");
        Assert.That(overEnabledChild, Is.True,
            "probe validity: a parent's tooltip must open while the pointer is over its enabled child");

        bool overDisabledChild = ProbeParentTooltip(childEnabled: false);
        Console.WriteLine($"SPIKE VERDICT: parent tooltip over a DISABLED child opened = {overDisabledChild}");

        // MEASURED 2026-08-25 (Avalonia 12 headless): it DOES open. What T016 measured is narrower than
        // "a disabled control is invisible to the pointer" — the control itself raises no pointer-enter, but the
        // hit test still lands inside it and the enter is delivered to the nearest ENABLED ancestor. So a greyed
        // control CAN explain itself on hover, by putting the tip one level up.
        //
        // This is the channel the Problemer panel's export button uses (ProblemsPanel.axaml): a padding-free
        // Border wraps the Button and carries ToolTip.Tip, so the withheld-reason reaches a sighted mouse user
        // as well as a screen reader. It does NOT reopen T021 branch B: the menu bar and toolbar rows are gated
        // by KEYBOARD gestures whose refusal has no pointer at all, and the status bar remains their channel.
        Assert.That(overDisabledChild, Is.True,
            "verdict pin: a disabled child passes the pointer-enter to its nearest ENABLED ancestor, so a "
            + "tooltip-carrying wrapper IS a channel for a greyed control's reason. If a platform upgrade "
            + "changes this, ProblemsPanel's export button loses its hover explanation and falls back to the "
            + "AutomationProperties.HelpText it also carries.");
    }

    private static bool ProbeTooltip(bool enabled)
    {
        var button = new Button { Content = "Probe", IsEnabled = enabled, Width = 200, Height = 40 };
        ToolTip.SetTip(button, "The reason this command is unavailable.");
        ToolTip.SetShowDelay(button, 0);   // no hover delay — the headless probe asserts synchronously
        var window = new Window { Width = 300, Height = 100, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Point centre = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)
                       ?? new Point(150, 50);
        window.MouseMove(centre);
        Dispatcher.UIThread.RunJobs();

        bool opened = ToolTip.GetIsOpen(button);
        window.Close();
        Dispatcher.UIThread.RunJobs();
        return opened;
    }

    // The same probe one level up: the TIP is on the Border, the pointer goes to the child Button's centre, and
    // the answer read back is the BORDER's. The button carries no tip of its own, so an open one could only have
    // come from the wrapper.
    private static bool ProbeParentTooltip(bool childEnabled)
    {
        var button = new Button { Content = "Probe", IsEnabled = childEnabled, Width = 200, Height = 40 };
        var border = new Border { Child = button };
        ToolTip.SetTip(border, "The reason this command is unavailable.");
        ToolTip.SetShowDelay(border, 0);
        var window = new Window { Width = 300, Height = 100, Content = border };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Point centre = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)
                       ?? new Point(150, 50);
        window.MouseMove(centre);
        Dispatcher.UIThread.RunJobs();

        bool opened = ToolTip.GetIsOpen(border);
        window.Close();
        Dispatcher.UIThread.RunJobs();
        return opened;
    }
}
