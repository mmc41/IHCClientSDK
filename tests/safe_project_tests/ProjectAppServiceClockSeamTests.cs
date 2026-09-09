using System;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace safe_project_tests;

/// <summary>
/// The supported determinism seam: a caller may hand <see cref="ProjectAppService"/> the clock it stamps with,
/// without also supplying a catalog. It is what makes a produced <c>.vis</c> reproducible — the file's
/// <c>id1</c>/<c>id2</c> ARE the creation instant, so a service on the system clock writes different bytes every
/// run, and a fixture comparing bytes has nothing stable to compare against.
/// </summary>
public class ProjectAppServiceClockSeamTests
{
    private static readonly DateTimeOffset Instant = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    /// <summary>
    /// The seam itself: the stamp a new project carries is the SUPPLIED clock's instant, not the system clock's.
    /// Asserted as the packed stamp rather than as an opaque token, so a failure says which instant was written.
    /// </summary>
    [Test]
    public void CreateNew_StampsWithTheSuppliedClock()
    {
        var clock = new FakeTimeProvider(Instant);
        var service = new ProjectAppService(new IhcSettings(), clock);

        Project project = service.CreateNew(new ProjectDetails("P", "I", "DK"));

        // The instant the service was handed, packed the way the engine writes it: comparing tokens states WHICH
        // instant was expected, where comparing two opaque strings would only say they differ. Read back through
        // the same provider because the stamp is taken from GetLocalNow(), and a fake clock's local zone is its
        // own (UTC by default) rather than the machine's — deriving the expectation from DateTimeOffset.ToLocalTime
        // would make the test pass or fail by the time zone the runner happens to sit in.
        Assert.Multiple(() =>
        {
            Assert.That(project.Id1, Is.EqualTo(PackedStamp.FromDateTime(clock.GetLocalNow()).ToToken()));
            Assert.That(project.Id2, Is.EqualTo(project.Id1), "id1 == id2 at creation");
        });
    }

    /// <summary>
    /// The property the seam exists for: two services on the same instant produce the same stamp, which is what a
    /// byte-comparing fixture depends on. A system-clock service cannot promise this.
    /// </summary>
    [Test]
    public void TwoServicesOnTheSameInstantProduceTheSameStamp()
    {
        Project first = new ProjectAppService(new IhcSettings(), new FakeTimeProvider(Instant))
            .CreateNew(new ProjectDetails("P", "I", "DK"));
        Project second = new ProjectAppService(new IhcSettings(), new FakeTimeProvider(Instant))
            .CreateNew(new ProjectDetails("P", "I", "DK"));

        Assert.That(second.Id1, Is.EqualTo(first.Id1));
    }
}
