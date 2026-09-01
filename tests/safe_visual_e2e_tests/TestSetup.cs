using NUnit.Framework;

// One desktop, one foreground, one running application: these tests cannot overlap each other by construction,
// so this is not a shared-state judgement that could be revisited.
[assembly: NonParallelizable]

namespace safe_visual_e2e_tests;

/// <summary>
/// The precondition the whole suite needs, and the one owner of the driver's lifetime.
///
/// <para>These tests are NOT <c>[Explicit]</c> — a plain <c>dotnet test</c> over the solution runs them, which
/// is the point of the split. So they have to answer honestly on a machine that cannot host them.</para>
///
/// <para><b>The requirement belongs to the DRIVER, not to the assembly.</b> Only the real mode needs Windows,
/// because only it speaks UI Automation through <c>pwsh</c>; the headless mode is Avalonia's own cross-platform
/// backend and needs nothing but a runtime. Asking the selected driver is what makes that true in code rather
/// than only in the README — the guard used to ignore the whole assembly off Windows, which silently withheld
/// the one mode built to run anywhere.</para>
///
/// <para><b>The guard tests a CAPABILITY, never an outcome.</b> Ignoring when the platform cannot run UI
/// Automation states a fact about the machine. Ignoring because the driver's <c>doctor</c> came back unhappy
/// would state a fact about the application — and turn every real breakage into a green run, which is the one
/// failure mode a suite this expensive exists to avoid. A Windows box with no session therefore FAILS here, and
/// it should.</para>
/// </summary>
[SetUpFixture]
public class DesktopSessionRequirement
{
    [OneTimeSetUp]
    public void RequireDriverPlatform()
    {
        if (E2E.Driver.UnmetRequirement is { } reason)
        {
            Assert.Ignore($"End-to-end tests skipped: {reason}");
        }
    }

    /// <summary>
    /// Releases the driver once the whole assembly is done. Nothing else owns it — it is reached through a
    /// lazily-initialised static — so without this the headless session and its dispatcher thread would run to
    /// process exit.
    /// </summary>
    [OneTimeTearDown]
    public void ReleaseDriver() => E2E.DisposeDriver();
}
