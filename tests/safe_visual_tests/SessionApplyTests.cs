using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace safe_visual_tests;

/// <summary>fablerefac W2-14: the session's command-Apply surface the VM will drive — <c>ApplyAsync</c> commits and
/// bumps the version and enters the labelled history; an edit prepared against a stale version is refused; and
/// <c>CanApply</c>/<c>Preview</c> answer without committing anything.</summary>
public class SessionApplyTests
{
    [Test]
    public async Task ApplyAsync_Commits_BumpsVersion_AndEntersHistory()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.NewAsync();
        int before = harness.Session.Version;
        int groups = harness.Session.Current!.Groups.Count;

        EditOutcome outcome = await harness.Session.ApplyAsync(new AddLocality("Alpha"));

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
            Assert.That(harness.Session.Version, Is.GreaterThan(before), "a commit bumps the version");
            Assert.That(harness.Session.Current!.Groups.Count, Is.EqualTo(groups + 1));
            Assert.That(harness.Session.CanUndo, Is.True);
            Assert.That(harness.Session.UndoLabel, Is.EqualTo("Indsæt lokalitet"));
        });
    }

    [Test]
    public async Task ApplyAsync_WithStaleBaseVersion_IsRefused()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.NewAsync();
        int stale = harness.Session.Version;
        await harness.Session.ApplyAsync(new AddLocality("First"));   // the project moves on
        int groups = harness.Session.Current!.Groups.Count;

        EditOutcome outcome = await harness.Session.ApplyAsync(new AddLocality("Stale"), baseVersion: stale);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused), "an edit prepared against an older version is stale");
            Assert.That(harness.Session.Current!.Groups.Count, Is.EqualTo(groups), "nothing was applied");
        });
    }

    [Test]
    public async Task CanApplyAndPreview_AnswerWithoutCommitting()
    {
        using var harness = ShellHarness.Create();
        Assert.That(harness.Session.CanApply(new AddLocality("X")).Ok, Is.False, "no project open → refused");

        await harness.Session.NewAsync();
        int groups = harness.Session.Current!.Groups.Count;

        EditVerdict verdict = harness.Session.CanApply(new AddLocality("X"));
        PreviewOutcome preview = harness.Session.Preview(new AddLocality("X"));

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Ok, Is.True, "a locality insert is always allowed");
            Assert.That(preview.Status, Is.EqualTo(PreviewStatus.WouldChange));
            Assert.That(preview.Changes!.Added, Is.Not.Empty, "the preview names the new locality id");
            Assert.That(harness.Session.Current!.Groups.Count, Is.EqualTo(groups), "neither query committed anything");
        });
    }

    [Test]
    public async Task BuildAddFunctionBlock_ResolvesKnownBlock_NullForUnknown_AndApplies()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.NewAsync();
        ElementId loc = harness.Session.Current!.Groups[0].Id!.Value;
        string master = harness.ProjectService.GetAvailableFunctionBlocks()[0].MasterType;

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Commands.AddFunctionBlock(harness.Session.Current!, loc, master), Is.Not.Null, "a known master type builds a command");
            Assert.That(harness.Session.Commands.AddFunctionBlock(harness.Session.Current!, loc, "not-a-real-block"), Is.Null, "an unknown one builds nothing");
        });

        EditOutcome outcome = await harness.Session.ApplyAsync(harness.Session.Commands.AddFunctionBlock(harness.Session.Current!, loc, master)!);
        Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed), "the built command applies through the session");
    }
}
