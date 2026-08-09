using Avalonia;
using Avalonia.Headless;
using NUnit.Framework;

[assembly: AvaloniaTestApplication(typeof(safe_visual_tests.TestAppBuilder))]

// Run sequentially: screenshot capture shares the static AvaloniaTestBase.CurrentTestWindow across tests.
[assembly: NonParallelizable]

namespace safe_visual_tests;

/// <summary>
/// Configures the headless Avalonia application every <c>[AvaloniaTest]</c> in this assembly runs against — the
/// real <see cref="ihc_openvisual.App"/> (so App.axaml, its styles and the ViewLocator are exercised), rendered
/// headlessly so it needs no native window or GPU on the CI runner. Rendering uses the real Skia renderer
/// (not the no-op headless drawing) so <c>Window.CaptureRenderedFrame()</c> works and
/// <see cref="CaptureScreenshotOnFailureAttribute"/> can attach failure screenshots — same setup as
/// tests/safe_lab_tests.
/// <para>Fonts come from <c>Program.WithAppFonts</c>, the same call the shipped executable makes, so text here is
/// laid out in the font the app actually renders in rather than the runner's platform default — which is what makes
/// <see cref="AppFontTests"/> a test of the application's configuration and not of this file.</para>
/// </summary>
public sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        ihc_openvisual.Program.WithAppFonts(AppBuilder.Configure<ihc_openvisual.App>())
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false  // real Skia renderer, enables CaptureRenderedFrame() for failure screenshots
            });
}
