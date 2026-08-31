using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The whole route a crashed rule takes: the engine's own fault, out of the validation loop, into the sink, onto
/// the screen as an Internal row.
///
/// <para><b>Why it is worth a fixture of its own.</b> Every layer of that route was already tested in isolation
/// and the route still did not exist — the workflow adapted the structured result down to its findings and threw
/// the faults away in the one line between them. A test per layer cannot see a gap BETWEEN layers, which is
/// exactly where this one was.</para>
///
/// <para><b>Two different faults, two different origins.</b> A rule that threw is the SDK's: the engine caught
/// it, kept going, and says so in the result. A run that threw is the HOST's: the exception escaped the engine's
/// per-rule guard, so the loop around it is what failed. They arrive by different routes and both must land.</para>
/// </summary>
[TestFixture]
public class ValidationFaultRoutingTests : AvaloniaTestBase
{
    private static InternalError RuleFailed(string rule = "name-empty") => ProblemsTestData.RuleFailed(rule);

    /// <summary>
    /// The gate's assertion: a rule that crashed is listed as an Internal row, and NOT as the Filintegritet error
    /// the engine used to invent for it.
    /// </summary>
    [Test]
    public async Task ACrashedRuleIsListedAsAnInternalRow()
    {
        InternalError fault = RuleFailed();
        using ProblemsRig rig = new(_ => new StructuredValidationResult(
            EquatableArray<ValidationFinding>.Empty, ImmutableArray.Create(fault)));

        await rig.WithNewProjectAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Rows.OfType<InternalErrorRowViewModel>().Select(r => r.Code),
                Is.EqualTo(new[] { "internal.rule-failed" }));
            Assert.That(rig.Panel.Rows.OfType<ProblemRowViewModel>(), Is.Empty,
                "and it is NOT a finding: the project has no file-integrity error, our rule crashed");
            Assert.That(rig.Panel.Internals.Count, Is.EqualTo(1),
                "counted on the Internal chip, which is the tier a fault belongs to");
        });
    }

    /// <summary>
    /// A crashed rule does not block. Blocking is a statement about the PROJECT, and a fault says nothing about
    /// one — reading it as blocking would refuse a transfer on the strength of our own bug.
    /// </summary>
    [Test]
    public async Task ACrashedRuleDoesNotBlockTheGate()
    {
        using ProblemsRig rig = new(_ => new StructuredValidationResult(
            EquatableArray<ValidationFinding>.Empty, ImmutableArray.Create(RuleFailed())));

        await rig.WithNewProjectAsync();

        Assert.That(rig.Validation.HasBlockingFindings, Is.False);
    }

    /// <summary>
    /// A run that threw outright reaches the sink too — the route <c>OnFaulted</c> had no exit from before. It is
    /// reported as HOST origin, because what failed is the loop around the engine rather than a rule inside it.
    /// </summary>
    [Test]
    public async Task ARunThatThrewIsListedAsAHostFault()
    {
        // The delegate type is NAMED because a throw-only lambda infers no return type, so it fits both
        // ProblemsRig overloads equally.
        using ProblemsRig rig = new(
            (Func<Project, StructuredValidationResult>)(_ => throw new TimeoutException("the rule set hung")));

        await rig.WithNewProjectAsync();

        InternalErrorRow row = rig.InternalErrors.Rows.Single();
        Assert.Multiple(() =>
        {
            Assert.That(row.Error.Origin, Is.EqualTo(InternalErrorOrigin.Host));
            Assert.That(row.Error.Code.Value, Does.StartWith("app."),
                "a host code: the SDK did not raise this, the host's loop did");
            Assert.That(row.Error.Detail, Does.Contain(nameof(ValidationMonitor)),
                "and the detail names where it was observed, which the exception cannot say itself");
            Assert.That(rig.Panel.Rows.OfType<InternalErrorRowViewModel>().Count(), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// A run that threw leaves whatever was bound STILL bound. A failed run is not evidence that the previous
    /// findings went away, and blanking the list on a fault would hide real findings behind our own bug.
    /// </summary>
    [Test]
    public async Task ARunThatThrewKeepsTheFindingsItAlreadyBound()
    {
        bool shouldThrow = false;
        ValidationFinding finding = new(
            new Problem(new ProblemCode("doc-project-info-blank"), "Projektinformationen er tom.",
                EquatableArray<ProblemArgument>.Empty),
            ValidationSeverity.Warning, ValidationCategory.Documentation,
            new FindingLocation("utcs_project", null, null), EquatableArray<FindingLocation>.Empty);
        using ProblemsRig rig = new(_ => shouldThrow
            ? throw new TimeoutException("the rule set hung")
            : new StructuredValidationResult(ImmutableArray.Create(finding), EquatableArray<InternalError>.Empty));

        await rig.WithNewProjectAsync();
        shouldThrow = true;
        await rig.Harness.Session.ApplyAsync(new AddLocality("Ny stue"));
        await rig.SettleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Rows.OfType<ProblemRowViewModel>().Select(r => r.Code),
                Is.EqualTo(new[] { "doc-project-info-blank" }), "the bound findings survive the fault");
            Assert.That(rig.Panel.Rows.OfType<InternalErrorRowViewModel>().Count(), Is.EqualTo(1),
                "and the fault is listed beside them rather than instead of them");
        });
    }
}
