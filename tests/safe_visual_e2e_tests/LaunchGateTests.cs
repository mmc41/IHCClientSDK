using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using ihc_openvisual.Configuration;
using Ihc.Tests;
using Ihc.UiAutomation;
using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// The launch gate and the one launch site, checked without a desktop: the gate is a pure function of what a
/// launch reports, and the launch site is found in this suite's own IL. Both run in the headless leg.
/// </summary>
[TestFixture]
public sealed class LaunchGateTests
{
    private const string UnmarkedTitle = "unavngivet - IHC OpenVisual";

    /// <summary>
    /// The verb option only a launch request carries, so the methods loading it are the launch sites. Assembled at
    /// run time, because a literal here would be one more site the scan reports.
    /// </summary>
    private static string LaunchOption() => string.Concat("--", "laun", "ch");

    [Test]
    public void TheGate_RefusesAnUnidentifiedHost_AndAnUnmarkedTitle_Together()
    {
        var failures = E2E.LaunchGateFailures(identifyRequests: 0, windowTitle: UnmarkedTitle);

        Assert.That(failures, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(failures, Has.One.Contains("never identified"));
            Assert.That(failures, Has.One.Contains("no controller-stub marker"));
        });
    }

    [Test]
    public void TheGate_PassesAnIdentifiedHost_WithAMarkedTitle()
    {
        Assert.That(E2E.LaunchGateFailures(identifyRequests: 1, UnmarkedTitle + Constants.ControllerStubTitleSuffix), Is.Empty);
    }

    /// <summary>
    /// Every desktop launch passes the switch and the gate only because every launch goes through one site. A
    /// scenario that sent the launch verb itself would start the application with whatever settings file sits
    /// beside it, and nothing would check it.
    /// </summary>
    [Test]
    public void OnlyOneMethod_AsksTheDriverToLaunch()
    {
        string launchOption = LaunchOption();
        var sites = AuthoredMembers.Of(typeof(E2E).Assembly, nameof(safe_visual_e2e_tests))
            .Where(method => IlBody.Instructions(method).Any(instruction => instruction.Literal == launchOption))
            .Select(method => method.DeclaringType!.Name + "." + method.Name)
            .ToList();

        Assert.That(sites, Is.EquivalentTo(new[] { nameof(E2E) + ".RequestLaunch", nameof(DriverArguments) + "." + nameof(DriverArguments.LaunchRequested) }),
            "one site asks for a launch, and the one argument reader the drivers share is the only one that reads it");
    }

    /// <summary>
    /// ...which holds only while nothing but the launch option launches. A bare <c>doctor</c> is the readiness check
    /// a person or an agent sends first, so in either driver it reports that nothing runs and starts nothing: started,
    /// the application would be the ungated, unswitched launch this site exists to prevent.
    /// </summary>
    [Test]
    public void ABareDoctor_ReportsThatNothingRuns_AndLaunchesNothing()
    {
        E2E.CloseApp();

        E2E.Envelope doctor = E2E.Run("doctor");

        Assert.That((doctor.Ok, doctor.Code), Is.EqualTo((false, "AppNotRunning")), doctor.Raw);
    }

    /// <summary>
    /// ...and nothing below that site is reached around it. <c>AppLauncher.Start</c> is public in a project this
    /// suite references, so a scenario calling it, or <c>Process.Start</c>, itself could start the application from
    /// its own build output, beside the developer's real settings, with no clean copy, no switch and no gate. Only
    /// the real driver's launch verb may start it, and nothing in this suite starts a process directly.
    /// </summary>
    [Test]
    public void OnlyTheDriversLaunchVerb_StartsTheApplication()
    {
        // The whole assembly rather than the suite's namespace root: a helper linked in from tests/shared starts a
        // process as surely as a scenario does, and one of them exists to start processes.
        var suite = AuthoredMembers.Of(typeof(E2E).Assembly).ToList();
        var launcher = typeof(AppLauncher).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        Assert.Multiple(() =>
        {
            Assert.That(CallersOf(suite, typeof(AppLauncher), nameof(AppLauncher.Start)),
                Is.EqualTo(new[] { nameof(UiaDriver) + ".Launch" }), "the one caller of the launcher");
            Assert.That(CallersOf(suite, typeof(Process), nameof(Process.Start)), Is.Empty,
                "no process started by this suite itself");
            Assert.That(CallersOf(launcher, typeof(Process), nameof(Process.Start)),
                Does.Contain(nameof(AppLauncher) + "." + nameof(AppLauncher.Start)),
                "positive control: the scan sees the Process.Start call the launcher is known to make");
        });
    }

    /// <summary>The methods among <paramref name="members"/> that call <paramref name="type"/>'s
    /// <paramref name="method"/>, any overload, by declaring type and name.</summary>
    private static List<string> CallersOf(IEnumerable<MethodBase> members, Type type, string method) =>
        [.. members
            .Where(member => IlBody.CalledMethods(member).Any(called => called.DeclaringType == type && called.Name == method))
            .Select(member => member.DeclaringType!.Name + "." + member.Name)
            .Distinct()];
}
