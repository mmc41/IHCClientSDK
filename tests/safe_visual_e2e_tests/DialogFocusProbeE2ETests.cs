using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// The driver can read which control holds KEYBOARD FOCUS — the one observable difference between a dialog that
/// merely opened and one that opened AT a field.
///
/// <para>Asserted against a window whose focus behaviour is already settled and not part of this feature's work:
/// <c>ProjectInfoWindow</c> focuses its project-number box on open. That makes this a test of the PROBE rather
/// than of any new focus code — if it fails, the probe is wrong, not the app.</para>
///
/// <para>It is an E2E rather than a headless test on purpose. Focus is a real windowing-system fact: a headless
/// render can be asked which control a view-model wanted focused, but only a live run can say what UI Automation
/// actually publishes, and publishing it is the whole point of the probe.</para>
/// </summary>
public class DialogFocusProbeE2ETests
{
    private const string FixtureFile = "Project6-Errors.vis";

    /// <summary>The field <c>ProjectInfoWindow</c> focuses when it opens.</summary>
    private const string FocusedOnOpen = "ProjNumberBox";

    [OneTimeSetUp]
    public void LaunchApp() => E2E.Launch(E2E.Fixture(FixtureFile));

    [OneTimeTearDown]
    public void CloseApp() => E2E.KillApp();

    [Test]
    [Category(E2E.DesktopOnly)]
    public void TheProjectInfoDialogReportsItsPreFocusedFieldThroughTheProbe()
    {
        E2E.RunOk("projectInfo", "get");
        try
        {
            E2E.Envelope read = E2E.RunOk("dialog", "read");
            JsonElement focused = read.Field("focused");

            Assert.That(focused.ValueKind, Is.Not.EqualTo(JsonValueKind.Null),
                "null means the APP does not hold focus — re-acquire the foreground; it is not a statement "
                + "about the dialog");
            Assert.Multiple(() =>
            {
                Assert.That(focused.GetProperty("id").GetString(), Is.EqualTo(FocusedOnOpen),
                    "the dialog opens at its project-number box, and the probe says which control that is");

                // The per-row flag and the summary must name the SAME control, or a caller could read one and
                // assert the other.
                string[] flagged = [.. read.Field("controls").EnumerateArray()
                    .Where(c => c.GetProperty("focused").GetBoolean())
                    .Select(c => c.GetProperty("id").GetString() ?? string.Empty)];
                Assert.That(flagged, Is.EqualTo(new[] { FocusedOnOpen }).AsCollection,
                    "exactly one control carries the per-row flag, and it is the one the summary named");
            });
        }
        finally
        {
            E2E.RunOk("dialog", "cancel");
        }
    }
}
