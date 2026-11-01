using ihc_openvisual.Services;
using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// A stopped telemetry collector makes a desktop run warn, not fail: the fault checks set aside this launch's
/// telemetry-pipeline-down fault once, and only when it is the most recent fault, so a real fault beside it still
/// fails. The decision is a pure function, checked here without a desktop.
/// </summary>
[TestFixture]
public sealed class PipelineDownExemptionTests
{
    private static readonly string PipelineDown = HostProblemCodes.TelemetryPipelineDown.Value;

    [Test]
    public void ThePipelineDownFault_IsSetAside_TheFirstTime()
    {
        Assert.That(E2E.UnexplainedFaults(1, PipelineDown, alreadySetAside: false), Is.EqualTo((0L, true)));
    }

    [Test]
    public void ASecondPipelineDownFault_InTheSameLaunch_Counts()
    {
        Assert.That(E2E.UnexplainedFaults(1, PipelineDown, alreadySetAside: true), Is.EqualTo((1L, false)));
    }

    [Test]
    public void AnotherFault_BesideThePipelineDownFault_StillCounts()
    {
        Assert.That(E2E.UnexplainedFaults(2, PipelineDown, alreadySetAside: false), Is.EqualTo((1L, true)));
    }

    [Test]
    public void AFaultWithAnotherCode_IsNeverSetAside()
    {
        Assert.That(E2E.UnexplainedFaults(1, HostProblemCodes.PlatformFault.Value, alreadySetAside: false), Is.EqualTo((1L, false)));
    }

    [Test]
    public void NoFault_SetsNothingAside()
    {
        Assert.That(E2E.UnexplainedFaults(0, PipelineDown, alreadySetAside: false), Is.EqualTo((0L, false)));
    }
}
