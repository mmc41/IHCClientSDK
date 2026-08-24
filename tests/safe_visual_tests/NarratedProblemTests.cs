using System;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Io;
using Ihc.Vis.Problems;

namespace safe_visual_tests;

/// <summary>
/// RF-1: what <see cref="HostProblems.Narrate"/> actually shows an installer when the SDK refused an operation
/// with a coded cause.
///
/// <para>The shell frames a failed open, save, import, block export or report with its own sentence naming the
/// FILE, and the SDK raises a chain whose cause names the CONDITION (<i>Filen er tom</i>). Only one of the two
/// reaches the dialog, because <see cref="ProblemPresenter"/> renders a chain's cause and only its cause. So the
/// composition Narrate builds decides which one an installer reads, and per D01 that must be the SDK's: a message
/// naming the file the installer just picked tells them nothing they did not already know, where the condition
/// tells them what to do about it. The framing is not lost — it becomes the chain's operation, and every call site
/// logs the exception besides.</para>
///
/// <para>Where the SDK raised no coded cause there is nothing more specific to show, so the framing stays the
/// rendered cause. That asymmetry is the point of the rule and is asserted here rather than left to a reader.</para>
/// </summary>
public class NarratedProblemTests
{
    private const string ProjectPath = @"C:\projects\Project1.vis";

    [Test]
    public void ARefusedOpenShowsTheSdksConditionRatherThanTheFileTheInstallerAlreadyChose()
    {
        ProjectFormatException raised = new(LoadRefusalCodes.Empty, "the stream holds no bytes");

        ProblemChain narrated = HostProblems.Narrate(HostProblems.ProjectOpenFailed(ProjectPath, raised), raised);

        Assert.That(ProblemPresenter.Text(narrated), Is.EqualTo("Filen er tom [load-empty]"),
            "the SDK's cause is the sentence that tells the installer WHY the open failed");
    }

    [Test]
    public void TheShellsFramingSurvivesAsTheOperationSoTheFileIsStillAvailable()
    {
        ProjectFormatException raised = new(LoadRefusalCodes.Empty, "the stream holds no bytes");
        Problem framing = HostProblems.ProjectOpenFailed(ProjectPath, raised);

        ProblemChain narrated = HostProblems.Narrate(framing, raised);

        Assert.Multiple(() =>
        {
            Assert.That(narrated.Operation.Code, Is.EqualTo(framing.Code),
                "the framing becomes the operation, so the path stays available to a title and to the log");
            Assert.That(narrated.Operation.Message, Does.Contain(ProjectPath),
                "and it still carries the file it was bound with");
        });
    }

    [Test]
    public void AnUncodedFailureStillShowsTheFramingBecauseNothingMoreSpecificExists()
    {
        InvalidOperationException raised = new("no identity was attached to this one");

        ProblemChain narrated = HostProblems.Narrate(HostProblems.ProjectOpenFailed(ProjectPath, raised), raised);

        Assert.That(ProblemPresenter.Text(narrated),
            Is.EqualTo($"Projektet '{ProjectPath}' kunne ikke åbnes. [app.openvisual.project-open-failed]"),
            "with no coded cause the framing is the most specific sentence there is");
    }
}
