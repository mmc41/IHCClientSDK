using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-42 (tmp/align-campaign-2026-08-10.md): the time editor shows a millisecond field only for the
/// types that HAVE one.
///
/// <para>Measured 2026-08-11 on the reference application's three time-family dialogs: <c>Tidspunkt</c>
/// (<c>resource_time</c>) shows <c>00.00.00</c>, while <c>Timer</c> and <c>Timertid</c> show
/// <c>00:00:00,000</c>. The DTD agrees — <c>resource_time</c> declares no <c>millisecond</c> attribute at all.
/// OpenVisual showed <c>T / M / S / ms</c> for all three.</para>
///
/// <para>The file was never at risk: the engine's writer deliberately writes no millisecond for
/// <c>resource_time</c>, and <c>TimerTimeInitialValueTests</c> pins that. The defect is the other half — a box
/// the installer can type into whose value is then silently discarded.</para>
/// </summary>
public class TimeEditorMillisecondParityTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm)> WithVariableAsync(string tag, string name)
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        ElementId section = vm.InstallationNodes[0].Children
            .Single(n => n.NodeKind == "section:internalsettings").ElementId!.Value;
        await harness.Session.AddVariableAsync(section, tag, name);
        return (harness, vm);
    }

    [TestCase("resource_time", false)]
    [TestCase("resource_timer", true)]
    [TestCase("resource_timertime", true)]
    public async Task TheDialogIsToldWhetherTheTypeHasMilliseconds(string tag, bool expected)
    {
        var (harness, vm) = await WithVariableAsync(tag, "Probe");
        using var _ = harness;

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Probe")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.ShowMilliseconds, Is.EqualTo(expected),
            $"{tag}: the reference application shows the millisecond field only where the type declares one");
    }

    /// <summary>And the window acts on it — asserted on the CONTROLS, because an input flag nothing reads would
    /// satisfy the test above while the box stayed on screen.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    [TestCase(false)]
    [TestCase(true)]
    public void TheMillisecondFieldIsShownOnlyWhenTheTypeHasOne(bool showMilliseconds)
    {
        var window = new VariablePropertiesWindow();
        CurrentTestWindow = window;
        window.Populate(new VariablePropertiesInput("Rediger Probe egenskaber", "Probe", "",
            ResourceInitialValue.OfTime(1, 2, 3, 4), ShowMilliseconds: showMilliseconds));

        Assert.Multiple(() =>
        {
            Assert.That(window.FindControl<TextBox>("MsBox")!.IsVisible, Is.EqualTo(showMilliseconds));
            Assert.That(window.FindControl<TextBlock>("MsLabel")!.IsVisible, Is.EqualTo(showMilliseconds),
                "the unit caption goes with its box — a lone 'ms' label is worse than none");
            Assert.That(window.FindControl<TextBox>("SecondBox")!.IsVisible, Is.True,
                "hours, minutes and seconds are on every time type");
        });
    }
}
