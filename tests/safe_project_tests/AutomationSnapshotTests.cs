using ihc_openvisual.Services;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The published state format, from both ends. It is a CONTRACT rather than a debug string: two drivers and
/// this suite parse it, and the whole reason there is one implementation of the writer and the reader is that
/// the same vocabulary written twice is free to drift with nothing comparing the copies.
/// </summary>
/// <remarks>
/// The parse fails CLOSED, and every rejection below is a case where a tolerant parser would have been worse
/// than no parser. A snapshot that silently defaulted <c>val</c> to current would make every wait return
/// instantly — which is exactly the defect the surface exists to remove, reintroduced one layer down and much
/// harder to see.
/// </remarks>
public class AutomationSnapshotTests
{
    private static AutomationSnapshot Sample(string document = "Project1-SimpelWired.vis") =>
        new(Generation: 3, Version: 17, ValidatedGeneration: 3, ValidatedVersion: 17,
            Dirty: true, Faults: 0, LastFault: null, DocumentName: document);

    [Test]
    public void TheFormat_IsTheDocumentedFieldsInTheDocumentedOrder()
    {
        Assert.That(Sample().Format(),
            Is.EqualTo("gen=3|ver=17|val=3.17|dirty=1|faults=0|fault=-|doc=Project1-SimpelWired.vis"),
            "the order is written down so a diff of two snapshots is readable by a person");
    }

    [Test]
    public void AFaultIsPublishedWithTheCountItArrivedOn()
    {
        AutomationSnapshot faulted = Sample() with { Faults = 2, LastFault = "app.openvisual.unexpected" };

        Assert.That(faulted.Format(), Does.Contain("faults=2|fault=2:app.openvisual.unexpected"),
            "the code is keyed by the same counter the assertion uses, so a reader can line the two up");
    }

    [Test]
    public void ASnapshotSurvivesTheRoundTrip()
    {
        AutomationSnapshot original = Sample() with { Faults = 5, LastFault = "internal.rule-failed" };

        SnapshotRead read = AutomationSnapshot.Read(original.Format());

        Assert.Multiple(() =>
        {
            Assert.That(read.Rejection, Is.Null);
            Assert.That(read.Value, Is.EqualTo(original));
        });
    }

    [Test]
    public void NothingBoundYet_RoundTripsAsNothingBoundRatherThanAsZero()
    {
        AutomationSnapshot unbound = Sample() with { ValidatedGeneration = null, ValidatedVersion = null };

        SnapshotRead read = AutomationSnapshot.Read(unbound.Format());

        Assert.Multiple(() =>
        {
            Assert.That(unbound.Format(), Does.Contain("val=-"));
            Assert.That(read.Value, Is.EqualTo(unbound));
            Assert.That(read.Value!.Value.IsValidationCurrent, Is.False,
                "a panel with nothing bound describes nothing, and must not read as describing the current document");
        });
    }

    [Test]
    public void ADocumentNameCarryingTheSeparators_SurvivesIntact()
    {
        // The escape character has to be escaped too, or a document literally named '%7C' decodes to one
        // holding a bar — a silent corruption of the only free-text field in the format.
        AutomationSnapshot awkward = Sample(document: "a|b=c%7Cd.vis");

        SnapshotRead read = AutomationSnapshot.Read(awkward.Format());

        Assert.Multiple(() =>
        {
            Assert.That(awkward.Format().Split('|'), Has.Length.EqualTo(7),
                "an unencoded bar in the document name would have split the snapshot into a new field");
            Assert.That(read.Value!.Value.DocumentName, Is.EqualTo("a|b=c%7Cd.vis"));
        });
    }

    [Test]
    public void NothingPublished_IsNotAnError()
    {
        SnapshotRead read = AutomationSnapshot.Read(null);

        Assert.Multiple(() =>
        {
            Assert.That(read.Absent, Is.True);
            Assert.That(read.Rejection, Is.Null,
                "started without the flag is a documented state; a driver must report it differently from "
                + "a snapshot it could not understand");
            Assert.That(read.Value, Is.Null);
        });
    }

    [TestCase("gen=3|ver=17|val=3.17|dirty=1|faults=0|fault=-|doc=a.vis|mode=2",
        TestName = "Read_Rejects_AnUnknownKey")]
    [TestCase("gen=3|gen=4|ver=17|val=3.17|dirty=1|faults=0|fault=-|doc=a.vis",
        TestName = "Read_Rejects_ADuplicateKey")]
    [TestCase("gen=3|val=3.17|dirty=1|faults=0|fault=-|doc=a.vis",
        TestName = "Read_Rejects_AMissingKey")]
    [TestCase("gen=3|ver=x|val=3.17|dirty=1|faults=0|fault=-|doc=a.vis",
        TestName = "Read_Rejects_ANumberThatIsNotOne")]
    [TestCase("gen=3|ver=17|val=3|dirty=1|faults=0|fault=-|doc=a.vis",
        TestName = "Read_Rejects_AValidationKeyThatIsNotAPair")]
    [TestCase("gen=3|ver=17|val=3.17|dirty=yes|faults=0|fault=-|doc=a.vis",
        TestName = "Read_Rejects_ADirtyFlagThatIsNeitherZeroNorOne")]
    [TestCase("gen=3|ver=17|val=3.17|dirty=1|faults=1|fault=nocolon|doc=a.vis",
        TestName = "Read_Rejects_AFaultThatNamesNoSequence")]
    // The two fault fields are one reading of one counter, so a pair the writer could not have produced is a
    // snapshot that was never written whole — the fail-closed rule holds between fields as well as within one.
    [TestCase("gen=3|ver=17|val=3.17|dirty=1|faults=1|fault=-|doc=a.vis",
        TestName = "Read_Rejects_ACountWithNoCodeBesideIt")]
    [TestCase("gen=3|ver=17|val=3.17|dirty=1|faults=2|fault=1:internal.unexpected|doc=a.vis",
        TestName = "Read_Rejects_AFaultKeyedByASequenceThatIsNotTheCount")]
    [TestCase("gen=3|ver=17|val=3.17|dirty=1|faults=1|fault=x:internal.unexpected|doc=a.vis",
        TestName = "Read_Rejects_AFaultWhoseSequenceIsNotANumber")]
    [TestCase("gen=3|ver=17|val=3.17|dirty=1|faults=0|fault=0:internal.unexpected|doc=a.vis",
        TestName = "Read_Rejects_ACodeWithNoCountBehindIt")]
    [TestCase("gen=3|ver=17|val=3.17|dirty=1|faults=0|fault=-|doca.vis",
        TestName = "Read_Rejects_APairWithNoSeparator")]
    public void AnythingUnexpected_RejectsTheWholeSnapshotAndSaysWhat(string published)
    {
        SnapshotRead read = AutomationSnapshot.Read(published);

        Assert.Multiple(() =>
        {
            Assert.That(read.Value, Is.Null, "a rejected snapshot must not yield a default-valued one");
            Assert.That(read.Absent, Is.False, "unreadable is not the same state as not published");
            Assert.That(read.Rejection, Does.Contain(published),
                "the rejection carries the offending text, because a reader cannot go back and look at it");
        });
    }

    [Test]
    public void ValidationIsCurrent_OnlyWhenBothKeysMatch()
    {
        AutomationSnapshot current = Sample();

        Assert.Multiple(() =>
        {
            Assert.That(current.IsValidationCurrent, Is.True);
            Assert.That((current with { Version = 18 }).IsValidationCurrent, Is.False,
                "an edit past the bound result is exactly the state a driver must not read as settled");
            Assert.That((current with { Generation = 4 }).IsValidationCurrent, Is.False,
                "a result about the previous document must not answer a question about this one");
        });
    }
}
