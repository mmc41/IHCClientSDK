using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>The data and paths the Problemer tests build from: a constructed finding, and where things live.</summary>
internal static class ProblemsTestData
{
    /// <summary>One constructed finding — the only way to exercise a tier no shipped rule emits.</summary>
    public static ValidationFinding Finding(
        ValidationSeverity severity,
        string code,
        string message,
        ValidationCategory category = ValidationCategory.Documentation,
        string locator = "Stue") =>
        new(new Problem(new ProblemCode(code), message, EquatableArray<ProblemArgument>.Empty),
            severity, category, new FindingLocation(locator, null, null),
            EquatableArray<FindingLocation>.Empty);

    /// <summary>
    /// The checkout root, found by walking up from the test assembly to the solution file. Needed by anything
    /// that reaches SOURCE rather than build output — a driver script, a checked-in document to regenerate.
    /// </summary>
    /// <exception cref="InvalidOperationException">No <c>IHCClientSDK.sln</c> above the test directory.</exception>
    public static string RepositoryRoot()
    {
        for (DirectoryInfo? dir = new(TestContext.CurrentContext.TestDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "IHCClientSDK.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("repo root (IHCClientSDK.sln) not found above the test directory");
    }

    /// <summary>A fixture under <c>tests/testdata/projects</c>, beside the built test assembly.</summary>
    public static string FixturePath(string name) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "projects", name);
}

/// <summary>
/// The panel ALONE, over a validation that returns exactly what the test hands it. The shape for anything about
/// rows, tiers, sorting or state, because it makes the result the test's own input rather than a fixture's.
///
/// <para>Shared rather than repeated per file because the panel's construction is not a detail a test gets to
/// have an opinion about: the marshal must be the synchronous <c>action =&gt; action()</c> and the clock must be
/// the fake one, or the debounce never elapses and the test hangs instead of failing.</para>
///
/// <para><b>Settling is the whole protocol.</b> Validation is debounced and then runs on the pool, so every
/// assertion about rows, counts or state has to advance the clock past the quiet period and then await the
/// worker going idle. <see cref="SettleAsync"/> is that step; an assertion made without it is racing the panel
/// rather than testing it.</para>
/// </summary>
internal sealed class ProblemsRig : IDisposable
{
    public FakeTimeProvider Clock { get; } = new();

    public ShellHarness Harness { get; }

    /// <summary>The monitor the panel presents — this rig's own, over the findings the test wrote.</summary>
    public ValidationMonitor Validation { get; }

    public ProblemsPanelViewModel Panel { get; }

    public ProblemsRig(params ValidationFinding[] findings)
        : this(_ => ImmutableArray.Create(findings))
    {
    }

    public ProblemsRig(Func<Ihc.Vis.Projects.Project, EquatableArray<ValidationFinding>> validate)
    {
        Harness = ShellHarness.Create(Clock);
        // A monitor of its own rather than the session's: the panel is the thing under test here, and it must be
        // drivable over findings the test chose rather than over whatever the real engine happens to produce.
        Validation = new ValidationMonitor(Harness.Session, validate);
        Panel = new ProblemsPanelViewModel(Harness.Session, Validation);
    }

    /// <summary>Advances past the quiet period and waits for the run it starts to finish.</summary>
    public async Task SettleAsync()
    {
        Clock.Advance(ValidationWorker.DefaultDebounce);
        await Panel.Idle.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>A new project, validated once — the state most assertions start from.</summary>
    public async Task<ProblemsRig> WithNewProjectAsync()
    {
        await Harness.Session.NewAsync();
        await SettleAsync();
        return this;
    }

    public void Dispose()
    {
        Panel.Dispose();
        Validation.Dispose();
        Harness.Dispose();
    }
}

/// <summary>
/// The WHOLE shell, panel included. The shape for anything that crosses the panel's edge — navigation into the
/// trees, the send gate, the view's own rendering — because those need the shell's commands and its real
/// <see cref="ProjectAppService"/>-backed validation, not a stand-in.
/// </summary>
internal sealed class ProblemsShellRig : IDisposable
{
    public FakeTimeProvider Clock { get; } = new();

    public ShellHarness Harness { get; }

    public MainWindowViewModel Shell { get; }

    public ProblemsPanelViewModel Panel => Shell.Problems;

    /// <summary>The session's own validation — the one the send gate reads, not the panel's view of it.</summary>
    public ValidationMonitor Validation => Harness.Session.Validation;

    public ProblemsShellRig()
    {
        Harness = ShellHarness.Create(Clock);
        Shell = Harness.CreateViewModel();
    }

    /// <inheritdoc cref="ProblemsRig.SettleAsync"/>
    public async Task SettleAsync()
    {
        Clock.Advance(ValidationWorker.DefaultDebounce);
        await Panel.Idle.WaitAsync(TimeSpan.FromSeconds(30));
    }

    public void Dispose()
    {
        Shell.Dispose();
        Harness.Dispose();
    }
}
