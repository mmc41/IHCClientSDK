using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using IhcLab;

namespace Ihc.Tests
{
    /// <summary>
    /// Tests for the shared <see cref="DurationInput"/> control: empty extracts null, a valid hh:mm:ss extracts the
    /// TimeSpan, and invalid text shows the inline error and throws (so a mistyped duration is never silently zeroed).
    /// </summary>
    [TestFixture]
    public class DurationInputTests : AvaloniaTestBase
    {
        private static TextBlock ErrorLabel(DurationInput input) =>
            ((StackPanel)input.Content!).Children.OfType<TextBlock>().First();

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void GetValueOrThrow_Empty_ReturnsNull()
        {
            var input = new DurationInput();
            Assert.That(input.GetValueOrThrow(), Is.Null);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void GetValueOrThrow_ValidText_ReturnsTimeSpanAndHidesError()
        {
            var input = new DurationInput { Text = "02:15:30" };

            Assert.That(input.GetValueOrThrow(), Is.EqualTo(new TimeSpan(2, 15, 30)));
            Assert.That(ErrorLabel(input).IsVisible, Is.False);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void GetValueOrThrow_InvalidText_ShowsErrorAndThrows()
        {
            var input = new DurationInput { Text = "nonsense" };

            Assert.Throws<FormatException>(() => input.GetValueOrThrow());
            Assert.That(ErrorLabel(input).IsVisible, Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_ThenGet_RoundTrips()
        {
            var input = new DurationInput();

            input.SetValue(new TimeSpan(1, 2, 3));

            Assert.That(input.Text, Is.EqualTo("01:02:03"));
            Assert.That(input.GetValueOrThrow(), Is.EqualTo(new TimeSpan(1, 2, 3)));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_Null_ClearsText()
        {
            var input = new DurationInput { Text = "01:02:03" };

            input.SetValue(null);

            Assert.That(input.Text, Is.EqualTo(string.Empty));
        }
    }
}
