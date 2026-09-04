using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// What every fixture that drives the application sits on — bar the one that raises a fault on purpose — for the
/// one assertion each of them should make and none of them did: that the application recorded no internal fault
/// while the test ran.
/// </summary>
/// <remarks>
/// <para><b>Why it is free.</b> Every scenario already walks a route through the real application. Each one
/// therefore already passes over every fault the route can raise — a dispatcher exception, a dropped task, a
/// configuration fallback, an SDK app-service fault — and until now none of them looked. Adding the check adds
/// no scenario and no runtime; it widens what every scenario already here proves.</para>
///
/// <para><b>Why a base class and not an attribute.</b> NUnit runs an <c>ITestAction</c> INSIDE the fixture's own
/// <c>[SetUp]</c>, so a fault raised by a fixture's own arrangement would fall outside the window and go
/// unseen. A base class's <c>[SetUp]</c> runs FIRST and its <c>[TearDown]</c> LAST, which is the window the
/// assertion needs.</para>
///
/// <para><b>Scope, stated honestly.</b> A fixture that shares ONE launch across its tests would otherwise report
/// an earlier scenario's faults against every later one, so the baseline is re-taken per test. That bounds the
/// damage; it does not establish causality, and <see cref="E2E.AssertNoNewFaults"/> says why.</para>
/// </remarks>
public abstract class E2EScenario
{
    [SetUp]
    public void TakeFaultBaseline() => E2E.TakeFaultBaseline();

    [TearDown]
    public void AssertTheScenarioRecordedNoInternalFault() => E2E.AssertNoNewFaults();
}
