using System;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Tests.Shared;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// A command row that faults PAST the view-model's error boundary — which is every row registered through the
/// synchronous adapter, whose own comment says such a row "fails by throwing, which the invocation funnel records
/// for it" — is by construction one that boundary did not handle. The funnel records it on the span and rethrows,
/// deliberately: the row's caller is owed its own exception.
///
/// <para>What the rethrow reaches is the application's dispatcher net, which files a durable row. That row named
/// the DISPATCHER, because that is all the net can see — while the funnel, one frame below, holds the command id
/// the whole gesture is identified by. Measured over an injected fault in the programming-mode rows: the span
/// carried <c>ihc.command.id</c> and the durable record carried nothing that said which gesture it was. Reported
/// here, before the rethrow, so the record names the gesture; the supervisor's first-sighting claim is what keeps
/// the dispatcher from filing a second row for the same exception.</para>
/// </summary>
[TestFixture]
public class CommandFunnelFaultTests
{
    private static CommandRegistry RegistryWith(Func<ShellContext> context, Func<Task> body)
    {
        CommandRegistry registry = new(context);
        registry.Register(new CommandSpec(
            "test.faulting", null, Surfaces.MenuBar,
            Execute: async _ => { await body(); return Ihc.OperationOutcome.Ok; },
            Gate: _ => EditVerdict.Allow));
        return registry;
    }

    [Test]
    public void AFaultEscapingARow_LeavesADurableRecordNamingTheCommand()
    {
        using CapturedFaults faults = new();
        CommandRegistry registry = RegistryWith(
            () => ShellContext.Empty, () => throw new TimeoutException("the row faulted"));

        Assert.That(
            async () => await registry.Commands["test.faulting"].ExecuteAsync(null),
            Throws.TypeOf<TimeoutException>(), "the row's caller is still owed its own exception");

        Assert.Multiple(() =>
        {
            Assert.That(faults.Rows, Is.Not.Empty,
                "a fault the view-model's boundary did not handle needs a record of its own");
            Assert.That(faults.Rows.Single().Detail, Does.Contain("test.faulting"),
                "and the record has to say WHICH gesture failed — the funnel is the only frame that knows");
        });
    }

    [Test]
    public async Task ARowThatSucceedsRecordsNothing()
    {
        using CapturedFaults faults = new();
        CommandRegistry registry = RegistryWith(() => ShellContext.Empty, () => Task.CompletedTask);

        await registry.Commands["test.faulting"].ExecuteAsync(null);

        Assert.That(faults.Rows, Is.Empty);
    }

    /// <summary>
    /// The de-duplication that makes the report safe: the dispatcher net sees the SAME exception instance a
    /// moment later, and one fault must not become two rows.
    /// </summary>
    [Test]
    public void TheSameExceptionIsNotFiledTwice()
    {
        using CapturedFaults faults = new();
        TimeoutException failure = new("the row faulted");
        CommandRegistry registry = RegistryWith(() => ShellContext.Empty, () => throw failure);

        Assert.That(
            async () => await registry.Commands["test.faulting"].ExecuteAsync(null),
            Throws.TypeOf<TimeoutException>());
        // What Program's dispatcher net does with what it caught.
        ihc_openvisual.Services.TaskSupervisor.Report(failure, "Dispatcher.UnhandledException");

        Assert.That(faults.Rows, Has.Count.EqualTo(1),
            "one fault is one row, however many nets see it");
    }
}
