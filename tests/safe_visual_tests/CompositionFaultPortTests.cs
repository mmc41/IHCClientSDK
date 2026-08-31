using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Problems;
using ihc_openvisual.Services;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The composition root's fault wiring: what the SDK's port is connected to, and in what order.
///
/// <para><b>The order is the part that can go wrong silently.</b> The port is supplied AT CONSTRUCTION, so the
/// sink has to exist before the service does. Build the service first — which is what the root used to do — and
/// there is nothing to pass, so the port stays null and every fault escaping an app-service operation continues
/// to be a rethrow and nothing else. Nothing fails; the feature is simply absent.</para>
///
/// <para><b>What this pins, and what it does not.</b> It pins the SHAPE the root composes: a sink, a service
/// holding that sink's append as its port, and a fault that travels from one to the other and lands as a row a
/// panel can list. It does not construct <c>App</c> itself — that needs a desktop lifetime — so the root's own
/// three lines are verified by reading. The value is in the shape: the shape is where the mistake lives, and a
/// root that composed something else would have to disagree with this test to do it.</para>
/// </summary>
[TestFixture]
public class CompositionFaultPortTests
{
    /// <summary>The composition root's own two lines, in its own order.</summary>
    private static (InternalErrorLog Sink, ProjectAppService Service) Compose()
    {
        InternalErrorLog sink = new();
        return (sink, new ProjectAppService(new IhcSettings(), sink.Append));
    }

    /// <summary>
    /// The gate's assertion: an exception escaping an app-service operation becomes a durable row naming that
    /// operation — and still reaches the caller, because reporting a fault must not change what a caller sees.
    /// </summary>
    [Test]
    public async Task AThrowingAppServiceOperationBecomesOneInternalRowNamingTheOperation()
    {
        (InternalErrorLog sink, ProjectAppService service) = Compose();
        string missing = $"does-not-exist-{nameof(AThrowingAppServiceOperationBecomesOneInternalRowNamingTheOperation)}.vis";

        Exception thrown = Assert.CatchAsync(async () => await service.Load(missing))!;

        InternalErrorRow row = sink.Rows.Single();
        Assert.Multiple(() =>
        {
            Assert.That(row.Error.Code.Value, Is.EqualTo("internal.unexpected"));
            Assert.That(row.Error.Origin, Is.EqualTo(InternalErrorOrigin.Sdk),
                "the SDK raised it — the host only supplied somewhere to put it");
            Assert.That(row.Error.Message, Is.EqualTo("Uventet fejl under 'Load'."),
                "the {operation} slot is bound, so the row says WHICH operation failed");
            Assert.That(row.Error.Detail, Does.Contain(missing),
                "and the captured detail still names what could not be found");
            Assert.That(thrown, Is.Not.Null,
                "the caller's exception surfaced unchanged: the port reports, it does not swallow");
        });
    }

    /// <summary>
    /// The same fault twice is ONE row with two occurrences, not two rows. A failing operation the user retries
    /// is the ordinary case, and a list that grew a row per attempt would bury the first one.
    /// </summary>
    [Test]
    public async Task RepeatingTheSameFailureGroupsOntoOneRow()
    {
        (InternalErrorLog sink, ProjectAppService service) = Compose();
        string missing = $"does-not-exist-{nameof(RepeatingTheSameFailureGroupsOntoOneRow)}.vis";

        Assert.CatchAsync(async () => await service.Load(missing));
        Assert.CatchAsync(async () => await service.Load(missing));
        await Task.CompletedTask;

        Assert.That(sink.Rows.Single().Occurrences, Is.EqualTo(2));
    }

    /// <summary>
    /// No port is still a valid composition — the design-time root is one, and it must stay side-effect free.
    /// The operation behaves exactly as it always did.
    /// </summary>
    [Test]
    public void AServiceComposedWithNoPortStillThrows()
    {
        ProjectAppService service = new(new IhcSettings());

        Assert.CatchAsync(async () => await service.Load(
            $"does-not-exist-{nameof(AServiceComposedWithNoPortStillThrows)}.vis"));
    }
}
