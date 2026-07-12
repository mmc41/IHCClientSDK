using Avalonia;
using Avalonia.Headless;
using NUnit.Framework;

[assembly: AvaloniaTestApplication(typeof(safe_visual_tests.TestAppBuilder))]

// Run sequentially: screenshot capture shares the static AvaloniaTestBase.CurrentTestWindow across tests.
[assembly: NonParallelizable]

namespace safe_visual_tests;

/// <summary>
/// Configures the headless Avalonia application every <c>[AvaloniaTest]</c> in this assembly runs against — the
/// real <see cref="ihc_visual.App"/> (so App.axaml, its styles and the ViewLocator are exercised), rendered
/// headlessly so it needs no native window or GPU on the CI runner. Rendering uses the real Skia renderer
/// (not the no-op headless drawing) so <c>Window.CaptureRenderedFrame()</c> works and
/// <see cref="CaptureScreenshotOnFailureAttribute"/> can attach failure screenshots — same setup as
/// tests/safe_lab_tests.
/// </summary>
public sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<ihc_visual.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false  // real Skia renderer, enables CaptureRenderedFrame() for failure screenshots
            });
}
