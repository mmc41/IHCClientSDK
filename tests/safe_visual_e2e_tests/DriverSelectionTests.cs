using System;
using System.Runtime.InteropServices;

using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// Which driver a run gets, and what it says when it cannot run at all.
/// </summary>
/// <remarks>
/// These read the SELECTION, not a live application, so they belong to the headless leg and run on every push —
/// including on the Linux and macOS legs, which are the only ones that can prove the real driver refuses
/// cleanly rather than crashing on a missing Windows API.
/// </remarks>
[TestFixture]
public sealed class DriverSelectionTests
{
    [Test]
    public void TheHeadlessParameterWinsOverAnyDriverChoice()
    {
        IE2EDriver driver = E2E.CreateDriver(headless: true, requested: E2E.UiaDriverName);

        Assert.That(driver, Is.InstanceOf<HeadlessDriver>(),
            "headless=true is what CI runs and must not be overridable by the driver parameter; a run that "
            + "asked for both would otherwise take a desktop on a machine that has none");
    }

    [Test]
    public void TheRealUiaDriverIsOfferedOnlyWhereWindowsUiAutomationExists()
    {
        IE2EDriver driver = E2E.CreateDriver(headless: false, requested: E2E.UiaDriverName);

        if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            Assert.That(driver.UnmetRequirement, Is.Null,
                "on Windows the real driver has everything it needs");
            return;
        }

        Assert.Multiple(() =>
        {
            Assert.That(driver.UnmetRequirement, Does.Contain(RuntimeInformation.OSDescription),
                "a refusal must name the platform it is refusing on, or the reader cannot tell an unsupported "
                + "OS from a broken install");
            Assert.That(driver.UnmetRequirement, Does.Contain(E2E.HeadlessParameter),
                "…and must name the parameter that DOES work here, so the message is a way forward");
        });
    }

    [Test]
    public void AnUnknownDriverNameIsRefusedByName()
    {
        ArgumentException refusal = Assert.Throws<ArgumentException>(
            () => E2E.CreateDriver(headless: false, requested: "flaui"))!;

        Assert.Multiple(() =>
        {
            Assert.That(refusal.Message, Does.Contain("flaui"),
                "the refusal quotes what was asked for");
            Assert.That(refusal.Message, Does.Contain(E2E.UiaDriverName).And.Contain(E2E.HeadlessParameter),
                "…and lists what IS available — the driver it could have meant, and the headless mode — "
                + "because a typo in a run parameter is otherwise silent");
        });
    }

    [Test]
    public void TheDefaultIsThisSuitesOwnDriver()
    {
        Assert.Multiple(() =>
        {
            Assert.That(E2E.DefaultDriverName, Is.EqualTo(E2E.UiaDriverName),
                "a run that names no driver must get the driver this suite owns — the whole point of it is that "
                + "the suite needs nothing outside this repository to drive the application");

            Assert.That(E2E.CreateDriver(headless: false, requested: E2E.DefaultDriverName), Is.Not.Null,
                "and selecting the default explicitly must be a legal choice — otherwise a run with no "
                + "parameter and a run naming the default would behave differently");
        });
    }
}
