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
