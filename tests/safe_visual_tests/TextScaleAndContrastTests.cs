using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Two accessibility affordances Avalonia does not provide, so the app must (accessibility review BP-12/BP-13):
/// <list type="bullet">
/// <item><b>Text scale.</b> Avalonia ignores the operating system's text-scaling setting entirely, so an in-app
/// control is the only route to WCAG 1.4.4 (Resize Text). Every workspace font size scales together, or the
/// hierarchy breaks.</item>
/// <item><b>High contrast.</b> Avalonia REPORTS the platform's contrast preference but ships no high-contrast
/// theme, so honouring the preference means supplying the palette.</item>
/// </list>
/// Spec: US-001 (Vis ▸ Tekststørrelse; contrast follows the OS preference).
/// </summary>
public class TextScaleAndContrastTests : AvaloniaTestBase
{
    /// <summary>The port's contract, with no Avalonia in play: the steps are ordered, Normal is the default and
    /// is exactly 1.0 (so an unscaled workspace is the untouched design), and applying one sticks.</summary>
    [Test]
    public void TextScaleSteps_AreOrderedAroundANormalOfExactlyOne()
    {
        var service = new NullThemeService();

        Assert.Multiple(() =>
        {
            Assert.That(service.TextScale, Is.EqualTo(TextScale.Normal), "the default step is Normal");
            Assert.That(TextScale.Normal.Factor(), Is.EqualTo(1.0),
                "Normal must not scale at all, or the unscaled design is unreachable");
            double[] factors = Enum.GetValues<TextScale>().Select(step => step.Factor()).ToArray();
            Assert.That(factors, Is.Ordered.Ascending, "the steps run smallest to largest");
            Assert.That(factors, Is.Unique, "each step is a distinct size");
            Assert.That(factors.Length, Is.GreaterThanOrEqualTo(3), "there are several steps to choose from");
        });

        service.ApplyTextScale(TextScale.Large);
        Assert.That(service.TextScale, Is.EqualTo(TextScale.Large), "the chosen step is what the port reports");
    }

    /// <summary>The rendering half: choosing a larger step really does grow the workspace font tokens, and grows
    /// ALL of them — scaling the tree labels but not the status bar would break the visual hierarchy.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ChoosingALargerTextSize_GrowsEveryWorkspaceFontToken()
    {
        using var harness = ShellHarness.Create();
        var theme = new ThemeService();
        MainWindowViewModel vm = harness.CreateViewModel(theme: theme);
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        double[] before = FontTokens();

        vm.SetTextScaleCommand.Execute(TextScale.Largest);
        Dispatcher.UIThread.RunJobs();

        double[] after = FontTokens();
        Assert.That(after.Zip(before, (a, b) => a > b), Has.All.True,
            $"every workspace font token grows together: {string.Join("/", before)} -> {string.Join("/", after)}");
    }

    /// <summary>
    /// The tree labels scale too — and they are the case that matters most, being the bulk of the workspace's text
    /// and the whole point of a Resize Text affordance. They carry no explicit font size of their own, so this only
    /// works if the scaled size is INHERITED from the window; a token applied solely to the headers and the status
    /// bar would leave the trees at a fixed size while everything around them grew.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ChoosingALargerTextSize_AlsoGrowsTheTreeLabels()
    {
        using var harness = ShellHarness.Create();
        var theme = new ThemeService();
        MainWindowViewModel vm = harness.CreateViewModel(theme: theme);
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        double before = FirstTreeLabelFontSize(window);

        vm.SetTextScaleCommand.Execute(TextScale.Largest);
        Dispatcher.UIThread.RunJobs();

        Assert.That(FirstTreeLabelFontSize(window), Is.GreaterThan(before),
            "a tree row's label inherits the scaled workspace size, rather than staying at the theme default");
    }

    // A realized tree row's label: the first TextBlock inside the installation pane's tree.
    private static double FirstTreeLabelFontSize(MainWindow window) =>
        window.FindControl<TreeView>("InstallationTree")!
            .GetVisualDescendants().OfType<TextBlock>().First().FontSize;

    /// <summary>Back down again: the steps are a live setting, not a one-way door.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ReturningToNormal_RestoresTheUnscaledSizes()
    {
        using var harness = ShellHarness.Create();
        var theme = new ThemeService();
        MainWindowViewModel vm = harness.CreateViewModel(theme: theme);
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        double[] unscaled = FontTokens();

        vm.SetTextScaleCommand.Execute(TextScale.Large);
        Dispatcher.UIThread.RunJobs();
        vm.SetTextScaleCommand.Execute(TextScale.Normal);
        Dispatcher.UIThread.RunJobs();

        Assert.That(FontTokens(), Is.EqualTo(unscaled), "Normal returns the tokens to their design values");
    }

    /// <summary>High contrast replaces the theme's ink and surface tokens rather than merely tweaking them: a
    /// "high-contrast" palette that resolves to the same brushes would be indistinguishable from doing nothing.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void HighContrast_ReplacesTheThemeInkTokens()
    {
        var theme = new ThemeService();
        theme.Apply(AppTheme.Light);
        Color ordinaryIcon = TokenColor("IconColor");
        var ordinaryWarning = (SolidColorBrush)Token("WarningBrush");

        theme.ApplyContrast(isHighContrast: true);
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(theme.IsHighContrast, Is.True, "the port reports the active preference");
            Assert.That(TokenColor("IconColor"), Is.Not.EqualTo(ordinaryIcon), "icon ink switches to the HC value");
            Assert.That(((SolidColorBrush)Token("WarningBrush")).Color, Is.Not.EqualTo(ordinaryWarning.Color),
                "and so does the warning ink");
        });

        theme.ApplyContrast(isHighContrast: false);
        Dispatcher.UIThread.RunJobs();
        Assert.That(TokenColor("IconColor"), Is.EqualTo(ordinaryIcon),
            "clearing the preference returns the ordinary palette — the switch is live in both directions");
    }

    // The workspace font tokens, resolved from the running application's resources.
    private static double[] FontTokens() =>
        new[] { "WorkspaceFontSize", "TitleFontSize", "BodyFontSize", "CaptionFontSize" }
            .Select(key => (double)Token(key)).ToArray();

    private static object Token(string key)
    {
        Application app = Application.Current!;
        app.TryGetResource(key, app.ActualThemeVariant, out object? value);
        Assert.That(value, Is.Not.Null, $"the '{key}' token resolves");
        return value!;
    }

    private static Color TokenColor(string key) => (Color)Token(key);
}
