using Avalonia.Headless.NUnit;
using NUnit.Framework;
using Ihc;
using IhcLab;

namespace Ihc.Tests
{
    /// <summary>
    /// Smoke tests that the dialogs load their XAML - including every design-token <c>StaticResource</c> reference -
    /// without error. The rest of the suite does not otherwise instantiate these windows, so a mistyped resource key
    /// would only surface here.
    /// </summary>
    [TestFixture]
    public class DialogSmokeTests : AvaloniaTestBase
    {
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void LoginDialog_LoadsXamlAndTokens()
        {
            var dialog = new LoginDialog(new IhcSettings());
            Assert.That(dialog, Is.Not.Null);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void AboutWindow_LoadsXamlAndTokens()
        {
            var window = new AboutWindow();
            Assert.That(window, Is.Not.Null);
        }
    }
}
