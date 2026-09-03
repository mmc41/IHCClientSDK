using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests;

/// <summary>
/// T002 (refac3 Phase-0 safety net): pins the <see cref="ihc_openvisual.Services.ProjectWorkflow"/> apply surface
/// that the facade relocation (T003 moves the scratch <c>Apply/CanApply/Preview</c> into <c>ProjectAppService</c>,
/// T004 rewires the workflow to delegate to it) must preserve unchanged. <c>SessionApplyTests</c> already covers a
/// committing edit, a stale-version refusal, and — via <c>Groups.Count</c> — that <c>CanApply</c>/<c>Preview</c>
/// do not commit; these cover the GAPS (an <c>ApplyAsync</c> that makes no change, and <c>ApplyAsync&lt;T&gt;</c>
/// surfacing the produced value) and STRENGTHEN the non-mutation guarantee to reference identity of
/// <c>Current</c> plus <c>Version</c>/<c>IsDirty</c>/<c>CanUndo</c>.
/// </summary>
public class ApplySurfaceCharacterizationTests
{
    [Test]
    public async Task ApplyAsyncOfT_OnCommit_SurfacesProducedValue_ThatResolvesInProject()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.NewAsync();

        // AddLocality is a ProjectCommand<ElementId>, so the generic overload runs and carries the new id.
        EditOutcome<ElementId> outcome = await harness.Session.ApplyAsync(new AddLocality("Room"));

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
            Assert.That(outcome.Value, Is.Not.EqualTo(default(ElementId)), "a committed value command surfaces its produced id");
            Assert.That(harness.Session.Current!.FindById(outcome.Value), Is.Not.Null,
                "the produced id resolves to a real element in the committed project");
        });
    }

    [Test]
    public async Task ApplyAsync_IdenticalEdit_IsNoChange_CurrentAndHistoryUntouched()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.NewAsync();
        ElementId loc = harness.Session.Current!.Groups[0].Id!.Value;

        // A first rename that definitely differs (a fixed name+note) commits, so the attributes now hold exactly
        // these values — the identical second rename below is then a guaranteed no-op.
        EditOutcome first = await harness.Session.ApplyAsync(new RenameLocality(loc, "Room", "note"));
        Assert.That(first.Status, Is.EqualTo(EditStatus.Committed), "precondition: the first rename changes something");

        var currentBefore = harness.Session.Current;
        int versionBefore = harness.Session.Version;
        string? undoLabelBefore = harness.Session.UndoLabel;

        EditOutcome outcome = await harness.Session.ApplyAsync(new RenameLocality(loc, "Room", "note"));

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(EditStatus.NoChange), "re-applying the same values changes nothing");
            Assert.That(harness.Session.Current, Is.SameAs(currentBefore), "a no-op does not swap Current");
            Assert.That(harness.Session.Version, Is.EqualTo(versionBefore), "a no-op does not bump the version");
            Assert.That(harness.Session.UndoLabel, Is.EqualTo(undoLabelBefore), "a no-op adds no undo entry");
        });
    }

    [Test]
    public async Task CanApplyAndPreview_LeaveWorkflowStateReferenceIdentical()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.NewAsync();
        await harness.Session.ApplyAsync(new AddLocality("Seed"));   // a non-trivial dirty state with history

        // Snapshot every observable piece of workflow state a probe must not disturb.
        var currentBefore = harness.Session.Current;
        int versionBefore = harness.Session.Version;
        bool dirtyBefore = harness.Session.IsDirty;
        bool canUndoBefore = harness.Session.CanUndo;

        EditVerdict verdict = harness.Session.CanApply(new AddLocality("Probe"));
        PreviewOutcome preview = harness.Session.Preview(new AddLocality("Probe"));

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Ok, Is.True, "a locality insert is always allowed");
            Assert.That(preview.Status, Is.EqualTo(PreviewStatus.WouldChange), "the probe would change the project");
            Assert.That(harness.Session.Current, Is.SameAs(currentBefore), "neither probe swaps Current");
            Assert.That(harness.Session.Version, Is.EqualTo(versionBefore), "neither probe bumps the version");
            Assert.That(harness.Session.IsDirty, Is.EqualTo(dirtyBefore), "neither probe touches the dirty flag");
            Assert.That(harness.Session.CanUndo, Is.EqualTo(canUndoBefore), "neither probe touches the undo history");
        });
    }
}
