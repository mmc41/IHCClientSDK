using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Ihc.UiAutomation;

/// <summary>
/// Saving a region of the screen as a PNG, for evidence attached to a test result.
/// </summary>
/// <remarks>
/// A read of the composited desktop, not of the application's own drawing. That is the point: it captures what
/// a person would actually see, including anything sitting on top. A window that is behind another, minimized
/// or off-screen therefore captures whatever occupies its rectangle — bring it to the foreground first.
/// </remarks>
public static class ScreenCapture
{
    /// <summary>
    /// Captures <paramref name="region"/> — in PHYSICAL pixels, as UI Automation reports rectangles — and
    /// writes it to <paramref name="path"/> as a PNG, creating the containing directory if it is missing.
    /// </summary>
    /// <returns>The path written.</returns>
    public static string Save(Rectangle region, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(region.Width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(region.Height, 1);

        // The copy reads screen coordinates, so it is in the same space as the rectangle only inside the scope.
        using DpiScope scope = DpiScope.Enter();

        if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
            Directory.CreateDirectory(directory);

        using Bitmap bitmap = new(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
        }

        bitmap.Save(path, ImageFormat.Png);
        return path;
    }
}
