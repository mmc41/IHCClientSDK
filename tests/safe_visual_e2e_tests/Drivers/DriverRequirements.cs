using System;
using System.Runtime.InteropServices;

namespace safe_visual_e2e_tests;

/// <summary>Why a real-GUI driver cannot run where it finds itself, in the words a reader can act on.</summary>
/// <remarks>
/// Shared by every driver that needs a Windows desktop, and by the stub that stands in for one where it cannot
/// even be constructed. Written once because it is one fact: a refusal has to name the platform it is refusing
/// on AND the way forward, or the reader cannot tell an unsupported OS from a broken install.
/// </remarks>
internal static class DriverRequirements
{
    internal static string NeedsWindowsUiAutomation() =>
        "the real GUI is driven through Windows UI Automation, and this is "
        + $"{RuntimeInformation.OSDescription}. Re-run with "
        // Quoted and prefixed with the argument separator so the sentence is a command line that runs as
        // pasted; single quotes are literal in every shell this message can be read in.
        + $"-- 'TestRunParameters.Parameter(name=\"{E2E.HeadlessParameter}\",value=\"true\")' for the headless mode.";
}

/// <summary>
/// A driver that exists only to explain why it cannot run.
/// </summary>
/// <remarks>
/// The real driver's type is annotated Windows-only, so on another platform it cannot be CONSTRUCTED at all —
/// but the suite still has to obtain a driver and read its unmet requirement to decide whether to ignore
/// itself. This stands in, carrying the requirement and nothing else.
/// </remarks>
/// <param name="name">How a failure message names the mode this stands in for.</param>
/// <param name="requirement">Why it cannot run here.</param>
internal sealed class UnavailableDriver(string name, string requirement) : IE2EDriver
{
    public string Name => name;

    public string? UnmetRequirement => requirement;

    public E2E.Envelope Run(string[] args) => throw new InvalidOperationException(requirement);

    public void KillApp()
    {
        // Nothing was ever started.
    }
}
