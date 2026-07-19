using Ihc.Vis.Session;

namespace safe_project_tests;

/// <summary>
/// fablerefac W2-1: the Ihc.Vis.Session core value types — the EditVerdict/HistoryPolicy factories, and that
/// EditOutcome&lt;T&gt; is an EditOutcome that still pattern-matches on Status and Value.
/// </summary>
public class SessionCoreTypesTests
{
    [Test]
    public void EditVerdict_Allow_IsOkWithNoReason()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EditVerdict.Allow.Ok, Is.True);
            Assert.That(EditVerdict.Allow.Reason, Is.Null);
        });
    }

    [Test]
    public void EditVerdict_Refuse_CarriesTheReason()
    {
        EditVerdict verdict = EditVerdict.Refuse("not allowed");

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Ok, Is.False);
            Assert.That(verdict.Reason, Is.EqualTo("not allowed"));
        });
    }

    [Test]
    public void HistoryPolicy_UnlimitedHasNoCap_BoundedKeepsIt()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HistoryPolicy.Unlimited.Cap, Is.Null);
            Assert.That(HistoryPolicy.Bounded(1000).Cap, Is.EqualTo(1000));
        });
    }

    [Test]
    public void EditOutcomeOfT_IsAnEditOutcome_AndPatternMatchesOnStatusAndValue()
    {
        EditOutcome outcome = new EditOutcome<int>(EditStatus.Committed, "Add product", null, null, Value: 42);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.InstanceOf<EditOutcome>(), "the generic outcome is assignable to the base");
            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
            Assert.That(outcome is EditOutcome<int> { Value: 42 }, Is.True, "pattern-matches on the carried value");
        });
    }
}
