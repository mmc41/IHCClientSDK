using System;
using System.Runtime.Versioning;

using ihc_openvisual.Services;

using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// A DRIVER CONTROL over the rule the real driver settles an activation by: which published movement means the
/// work behind a gesture has happened — and, the half that is easy to get wrong, which one does not.
/// </summary>
/// <remarks>
/// <para>The rule is a decision rather than a mechanism, so it is asserted here rather than on the desktop. The
/// scenario it exists for — a duplicate address authored through the pin dialog — can only fail INTERMITTENTLY
/// and only in the desktop leg, which is outside every default verification: the driver would return in the gap
/// between the dialog closing and the edit being committed, and the wait after it would accept the pre-edit
/// result. A test that reproduces it by racing a real window would be a worse test than one that states the
/// rule. Like <see cref="FaultReportingTests"/> and <see cref="DialogFocusProbeTests"/> this is not counted
/// against the end-to-end bar.</para>
///
/// <para>Windows-gated because the driver's type declares <c>SupportedOSPlatform("windows6.1")</c> for the sake
/// of everything else in it. This predicate would run anywhere; the contract is the type's.</para>
/// </remarks>
[SupportedOSPlatform("windows6.1")]
public class ActivationSettleTests
{
    [SetUp]
    public void SetUp()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("The real driver is declared Windows-only at its type.");
        }
    }

    /// <summary>A document at a version, with the findings on screen describing exactly that version.</summary>
    private static AutomationSnapshot Settled(int version) => new(
        Generation: 1,
        Version: version,
        ValidatedGeneration: 1,
        ValidatedVersion: version,
        Dirty: false,
        Faults: 0,
        LastFault: null,
        DocumentName: "Project1-SimpelWired.vis");

    /// <summary>
    /// The defect this rule exists for: a dialog closes on the click that dismisses it, and the edit behind an
    /// affirmative button is committed only after the awaited dialog returns.
    /// </summary>
    [Test]
    public void AModalThatClosed_IsNotOnItsOwnEvidenceTheWorkHappened()
    {
        AutomationSnapshot before = Settled(4);

        bool settled = UiaDriver.ActivationObservable(before, before, beforeModals: 1, modalsNow: () => 0);

        Assert.That(settled, Is.False,
            "returning here lands the verb between the close and the commit, where the version still reads "
            + "pre-edit and the bound result still describes the document from before the edit");
    }

    [Test]
    public void TheEditBehindTheClosedModal_Settles()
    {
        AutomationSnapshot before = Settled(4);
        AutomationSnapshot committed = before with { Version = 5 };

        Assert.That(UiaDriver.ActivationObservable(before, committed, beforeModals: 1, modalsNow: () => 0),
            Is.True, "the commit moves the version, which is what 'my edit landed' means");
    }

    /// <summary>An activation whose whole work is composing a window — a double-click that opens a dialog.</summary>
    [Test]
    public void AModalThatOpened_Settles()
    {
        AutomationSnapshot before = Settled(4);

        Assert.That(UiaDriver.ActivationObservable(before, before, beforeModals: 0, modalsNow: () => 1), Is.True);
    }

    [Test]
    public void ValidationCurrencyMoving_Settles()
    {
        AutomationSnapshot before = Settled(4);
        // What an edit looks like the instant before the version read catches up: the bound result now
        // describes an older document than the one on screen.
        AutomationSnapshot revalidating = before with { ValidatedVersion = 3 };

        Assert.That(UiaDriver.ActivationObservable(before, revalidating, beforeModals: 0, modalsNow: () => 0),
            Is.True);
    }

    [Test]
    public void NothingMoving_DoesNotSettle()
    {
        AutomationSnapshot before = Settled(4);

        Assert.That(UiaDriver.ActivationObservable(before, before, beforeModals: 0, modalsNow: () => 0),
            Is.False, "a settle that accepted an unmoved state would be the fixed sleep with extra steps");
    }

    /// <summary>
    /// The modal count is read LAST and only when it has to be: it is a cross-process enumeration of the
    /// application's windows, on a 25 ms poll, for the whole of a settle that has usually already ended.
    /// </summary>
    [Test]
    public void TheModalCountIsNotReadWhenTheEditAlreadyLanded()
    {
        int reads = 0;
        AutomationSnapshot before = Settled(4);

        UiaDriver.ActivationObservable(before, before with { Version = 5 }, beforeModals: 0,
            modalsNow: () => { reads++; return 0; });

        Assert.That(reads, Is.Zero);
    }
}
