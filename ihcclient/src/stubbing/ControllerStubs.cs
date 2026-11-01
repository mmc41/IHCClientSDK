using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ihc.Stubbing
{
    /// <summary>
    /// Routes a settings endpoint to a controller stub, so every SDK service built from those settings talks to
    /// the stub instead of a controller.
    /// </summary>
    public static class ControllerStubs
    {
        /// <summary>
        /// The command-line switch that points an executable at a controller stub host on this machine, written
        /// <c>--controller-stub=http://127.0.0.1:&lt;port&gt;/&lt;token&gt;/</c>.
        /// </summary>
        public const string SwitchArgument = "--controller-stub";

        /// <summary>
        /// Applies <see cref="SwitchArgument"/> once at an executable's start, after its own settings loader and before
        /// any SDK service is built.
        /// </summary>
        /// <param name="args">The executable's arguments.</param>
        /// <param name="loaded">The settings the executable's own loader produced, blank when it read no file.</param>
        /// <returns>
        /// Without the switch, <paramref name="loaded"/> itself and the arguments unchanged. With it, the settings
        /// every SDK service must be built from, which are <paramref name="loaded"/> with the endpoint replaced by the
        /// stub's and the login replaced by a stub-only one, and the arguments without the switch.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The registration the switch makes is kept inside the SDK until the process exits and is never returned, so
        /// nothing in the executable can release it: a process started against a stub stays a stub process, and
        /// refuses every real endpoint.
        /// </para>
        /// <para>
        /// The executable's own argument parser never sees the switch. It reads the arguments this returns, or, when it
        /// checks a command line before the settings loader runs, leaves out each argument <see cref="SwitchRequested"/>
        /// recognises, which leaves the same arguments whenever the switch is accepted.
        /// </para>
        /// <para>
        /// Synchronous by design, and the one member of the stub API that blocks: an executable's start may not go on
        /// before the stub host has proved itself, and a desktop application applies the switch from a synchronous
        /// <c>Main</c>. With the switch, the call blocks its thread until the host answers the identity check, for at
        /// most five seconds, so make it at start-up, before any UI thread runs, never from one. Without the switch it
        /// sends nothing and returns at once. <see cref="RegisterLoopbackAsync"/> makes the same registration without
        /// blocking, for a caller that can await it.
        /// </para>
        /// </remarks>
        /// <exception cref="ErrorWithCodeException">
        /// <see cref="Errors.CONTROLLER_STUB_ENDPOINT_INVALID"/> for a switch that is not one loopback stub address
        /// written after '=', or that is spelt any way but exactly <see cref="SwitchArgument"/>, and
        /// <see cref="Errors.CONTROLLER_STUB_IDENTITY_FAILED"/> when nothing answers there with
        /// its token. The executable must then refuse to start; there is no fallback to its settings file.
        /// </exception>
        public static (IhcSettings Effective, IReadOnlyList<string> RemainingArgs) ApplySwitch(IReadOnlyList<string> args, IhcSettings loaded)
        {
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(loaded);
            var (effective, remaining, registration) = ControllerStubSwitch.ApplySwitchCore(args, loaded);
            if (registration is not null)
            {
                ControllerStubSwitch.Hold(registration);
            }
            return (effective, remaining);
        }

        /// <summary>
        /// True when any argument is <see cref="SwitchArgument"/>, in either form, valid or not, or an option that
        /// names it in another spelling, such as another case or a colon for the '=', which
        /// <see cref="ApplySwitch"/> refuses. It reads nothing but <paramref name="args"/>, so an executable can ask
        /// it before its telemetry starts.
        /// </summary>
        /// <param name="args">The executable's arguments.</param>
        /// <returns>Whether the switch was given.</returns>
        public static bool SwitchRequested(IReadOnlyList<string> args)
        {
            ArgumentNullException.ThrowIfNull(args);
            return args.Any(ControllerStubSwitch.NamesSwitch);
        }

        /// <summary>
        /// Registers <paramref name="stub"/> in this process and returns the registration, whose
        /// <see cref="ControllerStubRegistration.Endpoint"/> a test puts in its settings.
        /// </summary>
        /// <param name="stub">The stub that answers every exchange sent to the registration's endpoint.</param>
        /// <param name="endpoint">
        /// Null for a fresh endpoint under a reserved host that never resolves, which suits a registration per
        /// test. Otherwise a <c>mock://</c> key, which is single-use per process and so suits a fixture that holds
        /// it for a whole run.
        /// </param>
        /// <returns>
        /// The registration. The caller owns it and disposes it once the services built on it are done; until
        /// then it stays live, even when nothing references it any more.
        /// </returns>
        /// <remarks>
        /// While any registration is live, every real controller endpoint in this process is refused. A registration
        /// from this method or <see cref="RegisterLoopbackAsync"/> keeps that refusal on only until it is disposed, and
        /// disposing the last live one ends it. The registration <see cref="ApplySwitch"/> makes is never released, so
        /// a process started with the switch refuses real endpoints until it exits.
        /// </remarks>
        /// <exception cref="ErrorWithCodeException">
        /// <see cref="Errors.CONTROLLER_STUB_ENDPOINT_INVALID"/> for a chosen key that is not <c>mock://</c>, and
        /// <see cref="Errors.CONTROLLER_STUB_ENDPOINT_IN_USE"/> for a key registered before in this process.
        /// </exception>
        public static ControllerStubRegistration Register(IControllerStub stub, string? endpoint = null)
        {
            ArgumentNullException.ThrowIfNull(stub);
            return ControllerStubRegistry.Add(stub, endpoint);
        }

        /// <summary>
        /// Registers a fresh stub endpoint that is sent to a controller stub host running in another process on this
        /// machine, once that host has proved who it is.
        /// </summary>
        /// <param name="baseAddress">
        /// The host's address as it prints it: <c>http://127.0.0.1:&lt;port&gt;/&lt;token&gt;/</c>, with a loopback IP
        /// literal, an explicit port and one token segment.
        /// </param>
        /// <param name="cancellationToken">Cancels the registration while the host's identity check is under way.</param>
        /// <returns>
        /// The registration, once the host has answered the identity check, which it gets at most five seconds to do.
        /// Its <see cref="ControllerStubRegistration.Endpoint"/> is what the settings take, and the caller owns it as it
        /// owns one from <see cref="Register"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// No registration exists before the host has proved itself, so no service can reach the host before then, and
        /// a cancelled or failed call leaves no registration behind.
        /// </para>
        /// <para>
        /// Failures follow the task-based asynchronous pattern. Only a null <paramref name="baseAddress"/>, a usage
        /// error, is thrown by the call itself; every other failure is stored in the returned task. A cancellation
        /// through <paramref name="cancellationToken"/> ends the task cancelled, which is never the outcome of a host
        /// that does not answer in time: that one faults the task with
        /// <see cref="Errors.CONTROLLER_STUB_IDENTITY_FAILED"/>.
        /// </para>
        /// <para>
        /// An executable's start makes the same registration through <see cref="ApplySwitch"/>, which blocks until the
        /// registration is made.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="baseAddress"/> is null. Thrown by the call itself.
        /// </exception>
        /// <exception cref="ErrorWithCodeException">
        /// Stored in the returned task: <see cref="Errors.CONTROLLER_STUB_ENDPOINT_INVALID"/> for an address that is not
        /// such a loopback URL, and <see cref="Errors.CONTROLLER_STUB_IDENTITY_FAILED"/> when nothing answers there with
        /// the token within five seconds, which is checked before the task completes and before any controller request
        /// is sent. A later answer that does not carry the token exactly once fails its call with the same code, after
        /// that call's request was sent.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Stored in the returned task, which ends cancelled, when <paramref name="cancellationToken"/> is cancelled
        /// before the host has answered. The exception carries that token.
        /// </exception>
        public static Task<ControllerStubRegistration> RegisterLoopbackAsync(Uri baseAddress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(baseAddress);
            return ControllerStubRegistry.AddLoopbackAsync(baseAddress, cancellationToken);
        }
    }
}
