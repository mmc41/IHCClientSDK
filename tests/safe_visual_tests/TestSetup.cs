using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(safe_visual_tests.TestAppBuilder))]

namespace safe_visual_tests;

/// <summary>
/// Configures the headless Avalonia application every <c>[AvaloniaTest]</c> in this assembly runs against — the
/// real <see cref="ihc_visual.App"/> (so App.axaml, its styles and the ViewLocator are exercised), rendered
/// headlessly so it needs no native window or GPU on the CI runner.
/// </summary>
public sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<ihc_visual.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
