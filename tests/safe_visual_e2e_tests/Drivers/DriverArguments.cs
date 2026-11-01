using System;

namespace safe_visual_e2e_tests;

/// <summary>
/// Reading a verb's options out of its argument array.
/// </summary>
/// <remarks>
/// Shared by every driver, because the argument array IS the contract between a scenario and whichever driver
/// serves it: a scenario writes one command line and both drivers must read it the same way. Two private copies
/// of this is how one driver comes to accept an option the other silently ignores.
/// </remarks>
internal static class DriverArguments
{
    /// <summary>The launch verb's option carrying the controller-stub switch's value to pass to the application.</summary>
    internal const string ControllerStubOption = "--controller-stub";

    /// <summary>
    /// The flag that makes <c>doctor</c> launch the application. Without it <c>doctor</c> only reports whether a
    /// launched application is ready, so a readiness check can never start one around the launch gate.
    /// </summary>
    internal const string LaunchFlag = "--launch";

    /// <summary>Whether <paramref name="args"/> ask the driver to launch the application: the one reader of
    /// <see cref="LaunchFlag"/>, which the launch-site pin counts beside the one sender.</summary>
    internal static bool LaunchRequested(string[] args) => Has(args, LaunchFlag);

    /// <summary>
    /// Whether a verb answers before there is an application to drive, which every other verb refuses: <c>doctor</c>,
    /// which launches one with the launch flag and otherwise reports that none runs, and <c>session status</c>, which
    /// reports the empty context a caller uses to discover that nothing is open.
    /// </summary>
    internal static bool AnswersWithoutApplication(string domain, string verb) =>
        (domain, verb) is ("doctor", _) or ("session", "status");

    /// <summary>The value following <paramref name="name"/>, or null when the option is absent or last.</summary>
    internal static string? Option(string[] args, string name)
    {
        ArgumentNullException.ThrowIfNull(args);

        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>Whether a valueless flag is present.</summary>
    internal static bool Has(string[] args, string flag)
    {
        ArgumentNullException.ThrowIfNull(args);
        return Array.IndexOf(args, flag) >= 0;
    }
}
