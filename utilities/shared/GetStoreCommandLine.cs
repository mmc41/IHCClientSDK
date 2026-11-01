using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ihc.Stubbing;

namespace Ihc.Utilities
{
    /// <summary>
    /// The command line the file utilities share: <c>GET &lt;destfile&gt;</c> or <c>STORE &lt;sourcefile&gt;</c>.
    /// Compiled into each utility that takes it, so the wording and the checks cannot drift between them. It reads
    /// nothing but the arguments, so a utility checks it before it reads its settings file, and answers a command line it
    /// cannot use whether or not that file exists.
    /// </summary>
    /// <param name="Command">The command word, upper-cased.</param>
    /// <param name="Path">The file it reads or writes.</param>
    internal sealed record GetStoreCommandLine(string Command, string Path)
    {
        internal const string Get = "GET";
        internal const string Store = "STORE";

        /// <summary>
        /// The command line in <paramref name="args"/>, or null after printing why it is not usable, in which case
        /// nothing may be attempted.
        /// </summary>
        /// <param name="args">The utility's arguments, the controller-stub switch included.</param>
        /// <param name="fileKind">What the file holds, as the missing-file message names it.</param>
        internal static GetStoreCommandLine? Parse(IReadOnlyList<string> args, string fileKind)
        {
            IReadOnlyList<string> line = WithoutSwitch(args);
            string? error = UsageError(line, fileKind);
            if (error is not null)
            {
                Console.WriteLine(error);
                return null;
            }
            return new GetStoreCommandLine(CommandOf(line), line[1]);
        }

        /// <summary>
        /// <paramref name="args"/> without the controller-stub switch, which is no part of this command line. Each
        /// argument is put to the SDK's own test of what the switch is, so this leaves the arguments
        /// <see cref="ControllerStubs.ApplySwitch"/> leaves whenever it accepts the switch, without the settings that
        /// call needs.
        /// </summary>
        private static IReadOnlyList<string> WithoutSwitch(IReadOnlyList<string> args) =>
            [.. args.Where(argument => !ControllerStubs.SwitchRequested([argument]))];

        private static string? UsageError(IReadOnlyList<string> args, string fileKind)
        {
            if (args.Count != 2)
            {
                return $"Expected arguments: '{Get} <destfile>' OR '{Store} <sourcefile>'";
            }
            string command = CommandOf(args);
            if (command != Get && command != Store)
            {
                return $"Illegal command. Expected {Get} or {Store}";
            }
            if (command == Store && !File.Exists(args[1]))
            {
                return $"Could not find source {fileKind} file  " + args[1];
            }
            return null;
        }

        // Invariant: the command words are ASCII, and a Turkish locale would fold 'i' to 'İ', leaving a correctly
        // typed command unrecognized on that machine alone.
        private static string CommandOf(IReadOnlyList<string> args) => args[0].ToUpperInvariant();
    }
}
