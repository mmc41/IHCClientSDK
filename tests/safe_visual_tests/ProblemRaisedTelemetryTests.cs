using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Services;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// What installers actually hit.
///
/// The counter sits on the three <c>ShowProblemAsync</c> overloads rather than on
/// <c>RaisedProblemDisplay</c>, which looks like the tidier home: eleven call sites reach a dialog WITHOUT
/// going through that helper, so counting there would under-report by exactly the paths nobody remembered.
/// These three overloads are what a problem must pass through to become something a user sees.
/// </summary>
[TestFixture]
public class ProblemRaisedTelemetryTests : AvaloniaTestBase
{
    /// <summary>The counts carrying one problem code - the projection this fixture asserts on.</summary>
    private static CapturedPoint[] PointsFor(TelemetryCapture capture, string code) =>
        capture.Points.Where(p => (string?)p.Tag("ihc.problem.code") == code).ToArray();

    private static Problem ProblemWith(string code) =>
        new(ProblemCode.Parse(code), "En dansk sætning.", EquatableArray<ProblemArgument>.Empty, "An English diagnostic.");

    /// <summary>
    /// All three overloads, in one test: each must produce exactly ONE count carrying the code of the right
    /// problem. The task returned by each is deliberately not awaited - the dialog waits for a person, and
    /// the count is recorded before it opens so a problem shown is counted whether or not it is dismissed.
    /// </summary>
    [AvaloniaTest]
    public void EachOverloadCountsOnce_WithTheCodeAndFamilyOfTheRightProblem()
    {
        using TelemetryCapture counts = TelemetryCapture.ListenWithTracingDisabled(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            instruments: new[] { "ihc.problem.raised" });
        var dialogs = new AvaloniaDialogService(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        Problem plain = ProblemWith("app.openvisual.plain");
        Problem cause = ProblemWith("edit.the-cause");
        Problem operation = ProblemWith("app.openvisual.the-operation");
        Problem head = ProblemWith("app.openvisual.the-head");

        _ = dialogs.ShowProblemAsync("T", plain);
        _ = dialogs.ShowProblemAsync("T", new ProblemChain(operation, cause));
        _ = dialogs.ShowProblemAsync("T", new ProblemAggregate(head, EquatableArray.Create<Problem>([cause, plain])));

        Assert.Multiple(() =>
        {
            Assert.That(PointsFor(counts, "app.openvisual.plain"), Has.Length.EqualTo(1),
                "the plain overload counts its own code");

            Assert.That(PointsFor(counts, "edit.the-cause"), Has.Length.EqualTo(1),
                "a chain counts its CAUSE - the operation says what was attempted, the cause says what was wrong");
            Assert.That(PointsFor(counts, "app.openvisual.the-operation"), Is.Empty,
                "the operation is not the thing that went wrong, so it is not what is counted");

            Assert.That(PointsFor(counts, "app.openvisual.the-head"), Has.Length.EqualTo(1),
                "an aggregate counts its HEAD once - it is ONE dialog, not one per item");
            Assert.That(counts.Points.Count, Is.EqualTo(3),
                "three dialogs, three counts - counting an aggregate's items would inflate this to five");
        });
    }

    [AvaloniaTest]
    public void TheCountCarriesTheFamilyDerivedFromTheCode()
    {
        using TelemetryCapture counts = TelemetryCapture.ListenWithTracingDisabled(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            instruments: new[] { "ihc.problem.raised" });
        var dialogs = new AvaloniaDialogService(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        _ = dialogs.ShowProblemAsync("T", ProblemWith("edit.locked"));

        CapturedPoint point = PointsFor(counts, "edit.locked").Single();
        Assert.Multiple(() =>
        {
            Assert.That(point.Tag("ihc.problem.family"), Is.Not.Null);
            Assert.That(point.Tag("ihc.problem.family")?.ToString(), Is.Not.EqualTo("Unknown"),
                "a listed family, so the counter can be grouped without re-parsing every code");
            Assert.That(point.Tags.Keys, Is.EquivalentTo(new[] { "ihc.problem.code", "ihc.problem.family" }),
                "code and family only - a title or a message would be unbounded");
        });
    }
}
