using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The rules behind <see cref="ControllerStubs.ApplySwitch"/>: which argument is the switch, what it may name, and
    /// what the settings become when it is given.
    /// </summary>
    internal static class ControllerStubSwitch
    {
        /// <summary>
        /// The login a switched run uses in place of the one its settings carry. A stub accepts any non-blank password,
        /// and replacing both values keeps a person's real credentials out of every stub, journal and trace.
        /// </summary>
        internal const string SwitchUserName = "stub-user";

        internal const string SwitchPassword = "stub-only";

        private static readonly Lock Gate = new();

        // Every registration a switch made in this process. Held and never disposed, so a process started with the
        // switch stays a stub process until it exits, whatever its own code does later.
        private static readonly List<ControllerStubRegistration> Held = [];

        /// <summary>True for the switch in either form, valid or not.</summary>
        internal static bool IsSwitch(string argument) =>
            argument == ControllerStubs.SwitchArgument
            || argument.StartsWith(ControllerStubs.SwitchArgument + "=", StringComparison.Ordinal);

        // The switch's name with its separators left out, which every spelling below is compared against.
        private const string BareName = "controllerstub";

        // What an option may start with: the dashes, a Windows slash, and the en and em dashes an editor substitutes
        // for a typed one when a command line is copied from a document.
        private static readonly char[] OptionLeaders = ['-', '/', '–', '—'];

        /// <summary>
        /// True for the switch, and for an option that names it in another spelling: another case, a colon for the
        /// equals sign, one dash, a slash, or the hyphen left out or written as an underscore. A near spelling must be
        /// refused, never ignored, because an executable that ignored it would run on its settings file's controller
        /// in a run its author believes is stubbed.
        /// </summary>
        internal static bool NamesSwitch(string argument)
        {
            if (IsSwitch(argument))
            {
                return true;
            }
            string option = argument.TrimStart(OptionLeaders);
            if (option.Length == argument.Length)
            {
                return false;
            }
            string name = NameOf(option).Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
            return string.Equals(name, BareName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>An option without its value: everything before the first '=' or ':'.</summary>
        private static string NameOf(string option)
        {
            int end = option.IndexOfAny(['=', ':']);
            return end < 0 ? option : option[..end];
        }

        /// <summary>Keeps <paramref name="registration"/> live until the process exits.</summary>
        internal static void Hold(ControllerStubRegistration registration)
        {
            lock (Gate)
            {
                Held.Add(registration);
            }
        }

        /// <summary>
        /// Applies the switch and returns the registration it made, or null without a switch. The caller owns that
        /// registration, which is what lets a test dispose it; an executable gets no such chance.
        /// </summary>
        internal static (IhcSettings Effective, IReadOnlyList<string> RemainingArgs, ControllerStubRegistration? Registration)
            ApplySwitchCore(IReadOnlyList<string> args, IhcSettings loaded)
        {
            int[] at = [.. args.Select((argument, index) => (argument, index)).Where(a => NamesSwitch(a.argument)).Select(a => a.index)];
            if (at.Length == 0)
            {
                return (loaded, [.. args], null);
            }
            if (at.Length > 1)
            {
                throw Refused("it was given more than once, so which stub was meant is not known");
            }

            string argument = args[at[0]];
            string prefix = ControllerStubs.SwitchArgument + "=";
            if (!IsSwitch(argument))
            {
                // Only the name is repeated: the value is the host's address, whose path is its token.
                throw Refused($"'{NameOf(argument)}' is another spelling of it, and is refused rather than ignored, which "
                    + $"would leave the application on its settings file. Write it exactly as {prefix}http://127.0.0.1:<port>/<token>/");
            }
            if (!argument.StartsWith(prefix, StringComparison.Ordinal))
            {
                // The two-token form is refused rather than read, so the application's own next argument can never be
                // taken for a stub's address.
                throw Refused($"its value must follow '=' in the same argument, as in {prefix}http://127.0.0.1:<port>/<token>/");
            }
            string value = argument[prefix.Length..];
            if (value.StartsWith(SpecialEndpoints.MockedPrefix, StringComparison.Ordinal))
            {
                throw Refused("a mock:// stub runs inside a test, and an application carries none to serve it");
            }
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? baseAddress))
            {
                throw Refused($"'{value}' is not an absolute URL");
            }

            // The stub API's one blocking wait, for the reasons ApplySwitch documents. Every await beneath it ignores the
            // captured context, so it cannot deadlock on one, and GetResult rethrows a refusal as itself, never wrapped
            // in an AggregateException, so an executable catches the same ErrorWithCodeException either way.
            ControllerStubRegistration registration = ControllerStubs.RegisterLoopbackAsync(baseAddress, CancellationToken.None)
                .GetAwaiter().GetResult();
            IhcSettings effective = loaded with
            {
                Endpoint = registration.Endpoint,
                UserName = SwitchUserName,
                Password = SwitchPassword,
            };
            return (effective, [.. args.Where((_, index) => index != at[0])], registration);
        }

        private static ErrorWithCodeException Refused(string why) =>
            new(Errors.CONTROLLER_STUB_ENDPOINT_INVALID,
                $"{nameof(Errors.CONTROLLER_STUB_ENDPOINT_INVALID)}: the {ControllerStubs.SwitchArgument} switch was "
                + $"refused because {why}. The application does not start, and does not fall back to its settings file.");
    }
}
