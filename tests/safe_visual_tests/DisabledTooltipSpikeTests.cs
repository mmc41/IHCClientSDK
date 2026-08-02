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
/// headless session and pins whatever the platform actually does. The verdict is recorded in
/// tmp/crudarch/proposal-backlog.md → Discoveries.
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
}
