using System;
using System.Linq;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Problems;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// A fault in the tool, as the Problemer panel binds it. It fills the same columns a finding does and means
/// something different in several of them, so what is worth pinning is exactly where the two rows diverge — and
/// that the divergence is expressed by the TYPE rather than by nullable members a caller has to remember about.
/// </summary>
[TestFixture]
public class ProblemRowPolymorphismTests
{
    private static InternalErrorRowViewModel Row(string code = "internal.rule-failed") =>
        new(new InternalError(new ProblemCode(code),
                ProblemsTestData.RuleFailedMessage,
                "Rule threw", InternalErrorOrigin.Sdk, "at Foo()\n   at Bar()", DateTimeOffset.UnixEpoch),
            $"{code}@0");

    [Test]
    public void ItFillsTheFiveColumnsTheWayAFaultShould()
    {
        InternalErrorRowViewModel row = Row();

        Assert.Multiple(() =>
        {
            Assert.That(row.TierLabel, Is.EqualTo("Intern fejl"), "Alvor");
            Assert.That(row.TierIcon, Is.EqualTo("/Assets/severity-internal.svg"));
            Assert.That(row.CategoryLabel, Is.EqualTo("Intern fejl"),
                "Kategori — a real WORD, not an em-dash: the column sorts under Danish collation and a dash has "
                + "no place in that order");
            Assert.That(row.Message, Is.EqualTo(ProblemsTestData.RuleFailedMessage),
                "the catalogue's sentence, rendered whole");
            Assert.That(row.ElementName, Is.EqualTo("—"), "Element — a fault is about no element");
            Assert.That(row.Code, Is.EqualTo("internal.rule-failed"), "Kode");
        });
    }

    [Test]
    public void ItIsDimmedAndGoesNowhere()
    {
        InternalErrorRowViewModel row = Row();

        Assert.Multiple(() =>
        {
            Assert.That(row.NavigationKind, Is.EqualTo(NavigationKind.None));
            Assert.That(row.ElementEmphasis, Is.LessThan(1.0),
                "dimming is how every other unnavigable row already says it has nowhere to go");
            Assert.That(row.NavigationHint, Does.Not.Contain("Klik"),
                "and the tooltip does not promise a click that cannot land");
        });
    }

    /// <summary>
    /// The divergence is in the TYPE. A single row type would have had to answer "which severity?" and "which
    /// category?" for a fault, and every answer would be a claim about a project the fault says nothing about.
    /// </summary>
    [Test]
    public void AFaultRowHasNoSeverityAndNoTypedCategoryToAnswerWith()
    {
        Type row = typeof(InternalErrorRowViewModel);

        Assert.Multiple(() =>
        {
            Assert.That(row.GetProperty(nameof(ProblemRowViewModel.Severity)), Is.Null,
                "a fault has no severity, so the question is unaskable rather than answered with a lie");
            Assert.That(row.GetProperty(nameof(ProblemRowViewModel.Category)), Is.Null,
                "and no check family either — a category is a statement about project content");
            Assert.That(typeof(ProblemsPanelRowViewModel).IsAssignableFrom(row), Is.True,
                "while still being a row the panel can list beside a finding");
        });
    }

    /// <summary>Both row kinds bind the same members, which is what lets one list hold both.</summary>
    [Test]
    public void BothRowKindsBindTheSameColumnSurface()
    {
        string[] shared =
            [.. typeof(ProblemsPanelRowViewModel)
                .GetProperties()
                .Select(p => p.Name)
                .OrderBy(name => name, StringComparer.Ordinal)];

        Assert.That(shared, Is.EqualTo(new[]
        {
            "AccessibleText", "Code", "CategoryLabel", "Element", "ElementEmphasis", "ElementName",
            "Message", "NavigationHint", "NavigationKind", "OccurrenceId", "TierIcon", "TierLabel",
        }.OrderBy(name => name, StringComparer.Ordinal)).AsCollection,
            "the shared surface is exactly what the five columns and the row chrome bind — a member added here "
            + "is a member every row kind must be able to answer");
    }
}
