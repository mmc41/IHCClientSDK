using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: Ihc.Tests.NoLeakedHarness]

namespace Ihc.Tests;

/// <summary>
/// Fails the test that leaves a <see cref="ShellHarness"/> undisposed, rather than the test that pays for it.
///
/// <para>An undisposed harness is not merely untidy. It owns a <see cref="ihc_openvisual.Services.ProjectWorkflow"/>,
/// which owns the validation monitor, which owns a timer — and a timer keeps its own callback reachable, so the
/// whole graph stays alive whether or not anything still references the harness. On a system clock that timer
/// then fires into whatever test happens to be running, and the damage shows up somewhere else entirely; that is
/// the shape of failure this attribute exists to name at its source. The harnesses now run on a fake clock, so
/// the leak is currently inert — but the fix and the guard are separate claims, and this is the one that keeps
/// holding if a test ever asks for <c>TimeProvider.System</c> back.</para>
///
/// <para>Applied to the ASSEMBLY, so it covers every fixture without any of them inheriting anything. The count
/// is compared against its value before the test rather than against zero, so a fixture that legitimately holds
/// a harness across several tests would be measured on its delta.</para>
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Assembly)]
public sealed class NoLeakedHarnessAttribute : System.Attribute, ITestAction
{
    private int _before;

    public ActionTargets Targets => ActionTargets.Test;

    public void BeforeTest(ITest test) => _before = ShellHarness.Live;

    public void AfterTest(ITest test)
    {
        int leaked = ShellHarness.Live - _before;
        if (leaked > 0)
        {
            Assert.Fail(
                $"{test.Name} left {leaked} ShellHarness instance(s) undisposed. A harness owns a validation "
                + "timer that outlives the test, so dispose it — the idiom is a helper that RETURNS the harness "
                + "and a caller that writes `using var _ = harness;`.");
        }
    }
}
