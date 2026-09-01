using Avalonia;
using Avalonia.Headless;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// OpenVisual built the way the shipped executable builds it, rendered headlessly — the one builder every
    /// suite that hosts the real application points its <c>[AvaloniaTestApplication]</c> at.
    /// </summary>
    /// <remarks>
    /// Shared because this is what decides FONT METRICS, and font metrics decide layout. The end-to-end driver
    /// reports the rows a virtualizing list has realized, ordered by their bounds, so a renderer or font step
    /// added to one suite's builder and not the other's makes the two disagree about which rows are on screen —
    /// with no compile error, and with the suite that was not updated staying green.
    /// <para>Fonts come from <c>Program.WithAppFonts</c>, the same call the shipped executable makes, so text is
    /// laid out in the font the app actually renders in rather than the runner's platform default. That is what
    /// makes a font assertion a test of the application's configuration and not of this file.</para>
    /// <para><c>UseHeadlessDrawing = false</c> keeps the real Skia renderer, which is what makes
    /// <c>CaptureRenderedFrame()</c> return pixels rather than null.</para>
    /// </remarks>
    internal sealed class OpenVisualHeadlessApp
    {
        public static AppBuilder BuildAvaloniaApp() =>
            ihc_openvisual.Program.WithAppFonts(AppBuilder.Configure<ihc_openvisual.App>())
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
