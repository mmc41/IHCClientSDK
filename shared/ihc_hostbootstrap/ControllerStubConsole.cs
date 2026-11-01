using System;
using Ihc;

namespace Ihc.Bootstrap
{
    /// <summary>
    /// The one line a console utility prints when it runs against a controller stub. It is the single print site, so
    /// every utility announces a stub the same way, and it only prints: each utility's <c>Main</c> still applies the
    /// switch itself.
    /// </summary>
    public static class ControllerStubConsole
    {
        /// <summary>What the line starts with; the effective endpoint follows it.</summary>
        public const string LinePrefix = "CONTROLLER STUB in use (no real controller): ";

        /// <summary>
        /// Writes the stub line to standard error when <paramref name="effective"/> names a stub endpoint, and nothing
        /// otherwise. Standard error, so a utility's piped standard output stays clean while the line still shows on
        /// the console.
        /// </summary>
        /// <param name="effective">The settings the utility will build its services from, after the switch.</param>
        public static void AnnounceIfStub(IhcSettings effective)
        {
            ArgumentNullException.ThrowIfNull(effective);
            if (effective.IsStubConfigured())
            {
                Console.Error.WriteLine(LinePrefix + effective.Endpoint);
            }
        }
    }
}
