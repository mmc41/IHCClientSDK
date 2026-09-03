using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The TWO rows one faulted validation run leaves behind, pinned against the composed wiring rather than one
/// route at a time.
///
/// <para><b>Why two rows are correct.</b> The composition root hands the SAME sink to the SDK service as its
/// fault port and to the workflow as its fault sink. A validation run that throws therefore reports twice, and
/// both sentences are true and say different things: the SDK's <c>internal.unexpected</c> names WHICH operation
/// faulted, and the host's <c>app.openvisual.validation-faulted</c> names the CONSEQUENCE — the rows still on
/// screen describe a document state the run never reached, and they read as current. Suppressing the SDK row
/// would need a per-call opt-out on the one channel whose whole job is never to miss a fault.</para>
///
/// <para><b>Why the existing fixtures cannot see this.</b> They exercise the two routes separately — a crashed
/// rule, and a run that threw — each over its own sink. A test per route cannot observe how many rows the two
/// produce TOGETHER, which is the only question a later reader asking "is this a duplicate?" is really asking.
/// This fixture is the recorded answer: it is not.</para>
///
/// <para><b>What the SDK-side throw stands in for.</b> The shipped rule set cannot be made to crash from outside
/// the SDK — a rule that throws needs a substituted executor, and that seam is <c>internal</c> to ihcclient. So
/// the run here fails inside a DIFFERENT real traced app-service operation, which fires the identical port by
/// the identical route. What that costs is one word: the SDK row names the operation that actually threw, where
/// a crashed validation would name <c>ValidateStructured</c>. Everything asserted below — one sink, two
/// reporters, two rows, their order, their origins, their de-duplication — is unaffected by which operation
/// it was.</para>
/// </summary>
[TestFixture]
public class ValidationFaultRoutingCompositionTests
{
    /// <summary>
    /// Fixed rather than unique per call: the sink de-duplicates by code AND detail, so a varying path would
    /// make each run a new SDK row and hide the folding this fixture pins.
    /// </summary>
    private const string MissingProject = "no-such-project-for-the-composed-fault-route.vis";

    /// <summary>
    /// The composition root's wiring: ONE sink, given to the SDK service as its fault port and to the workflow
    /// as its fault sink, with the real monitor over the real workflow.
    /// </summary>
    private sealed class ComposedRig : IDisposable
    {
        public InternalErrorLog Sink { get; } = new();

        public ShellHarness Harness { get; }

        public ProjectAppService Service { get; }

        public ValidationMonitor Validation { get; }

        public ComposedRig()
        {
            // A service of its own rather than the harness's: giving that one a port needs the internal
            // constructor taking a catalog, a clock and a port together, and this assembly is deliberately not an
            // InternalsVisibleTo of the SDK (L5). Both are built over the SAME sink, which is the fact under test.
            Service = new ProjectAppService(new IhcSettings(), Sink.Append);
            Harness = ShellHarness.Create(faultSink: Sink.Append);
            Validation = new ValidationMonitor(Harness.Session, Validate, onFault: Sink.Append);
        }

        /// <summary>The run, failing inside a real traced app-service call so the SDK's own port fires first.</summary>
        private StructuredValidationResult Validate(Project project)
        {
            Service.Load(MissingProject).GetAwaiter().GetResult();
            return StructuredValidationResult.Empty;
        }

        public Task SettleAsync() => Harness.SettleValidationAsync(Validation);

        public void Dispose()
        {
            Validation.Dispose();
            Harness.Dispose();
        }
    }

    private static async Task<ComposedRig> FaultedRunAsync()
    {
        ComposedRig rig = new();
        await rig.Harness.Session.NewAsync();
        await rig.SettleAsync();
        return rig;
    }

    [Test]
    public async Task OneFaultedRunLeavesTheSdkRowAndTheHostRowInThatOrder()
    {
        using ComposedRig rig = await FaultedRunAsync();

        Assert.That(rig.Sink.Rows.Select(row => row.Error.Code.Value).ToArray(), Is.EqualTo(new[]
        {
            "internal.unexpected",
            "app.openvisual.validation-faulted",
        }).AsCollection, "two rows, deliberately — and the SDK's comes first, because the fault is reported "
            + "where it is raised before it is reported where its consequence is known");
    }

    /// <summary>
    /// The two rows say different things, which is the whole justification for keeping both. Asserted on their
    /// ORIGINS and their sentences rather than only their codes, so a change that made one a copy of the other
    /// fails here instead of quietly halving what the reader is told.
    /// </summary>
    [Test]
    public async Task TheTwoRowsCarryDifferentOriginsAndDifferentSentences()
    {
        using ComposedRig rig = await FaultedRunAsync();

        InternalErrorRow sdk = rig.Sink.Rows[0];
        InternalErrorRow host = rig.Sink.Rows[1];

        Assert.Multiple(() =>
        {
            Assert.That(sdk.Error.Origin, Is.EqualTo(InternalErrorOrigin.Sdk),
                "the SDK raised its own — the host only supplied somewhere to put it");
            Assert.That(host.Error.Origin, Is.EqualTo(InternalErrorOrigin.Host),
                "the loop AROUND the engine is what failed, so the host owns the second");
            Assert.That(host.Error.Message, Is.Not.EqualTo(sdk.Error.Message),
                "different facts, different sentences: which operation faulted, versus what that costs the "
                + "reader looking at the panel");
            Assert.That(host.Error.Detail, Does.Contain(nameof(ValidationMonitor)),
                "and the host row says where it was observed, which the exception cannot say itself");
        });
    }

    /// <summary>
    /// Each row stays ONE row: the sink de-duplicates by code and detail, so a second faulted run over the same
    /// failure raises the occurrence counts and adds nothing. That is what stops a fault storm from burying the
    /// panel, and it is worth pinning on the composed route because two reporters make it twice as easy to get
    /// wrong.
    /// </summary>
    [Test]
    public async Task ASecondFaultedRunAddsNoFurtherRows()
    {
        using ComposedRig rig = await FaultedRunAsync();
        Assert.That(rig.Sink.Rows, Has.Count.EqualTo(2), "precondition: the first run left the pair");

        await rig.Harness.Session.ApplyAsync(new AddLocality("Ny stue"));
        await rig.SettleAsync();

        Assert.That(rig.Sink.Rows.Select(row => row.Error.Code.Value).ToArray(), Is.EqualTo(new[]
        {
            "internal.unexpected",
            "app.openvisual.validation-faulted",
        }).AsCollection, "both rows repeat verbatim, so each folds into the row already there");
    }
}
