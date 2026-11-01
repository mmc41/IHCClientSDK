using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The live controller-stub registrations of this process, keyed by endpoint.
    /// </summary>
    /// <remarks>
    /// A set rather than a counter: a registration leaves only through its own <c>Dispose</c>, matched by
    /// identity, so disposing one twice can never release another. Every live registration is held strongly here
    /// and none has a finalizer, so dropping the last reference to one releases nothing: a leaked registration
    /// stays live and fails later work loudly rather than silently.
    /// </remarks>
    internal static class ControllerStubRegistry
    {
        private const string ServicePathMarker = "/ws/";

        private static readonly Lock Gate = new();
        private static readonly Dictionary<string, ControllerStubRegistration> Live = new(StringComparer.OrdinalIgnoreCase);

        // Every key ever registered, live or disposed. A key is single-use, so a service built on a disposed key
        // can never reach a later registration's stub, and two fixtures can never silently swap each other's.
        private static readonly HashSet<string> Used = new(StringComparer.OrdinalIgnoreCase);

        private static long generated;

        internal static ControllerStubRegistration Add(IControllerStub stub, string? endpoint)
        {
            string chosen = endpoint is null ? NextGeneratedEndpoint() : ValidatedKey(endpoint);
            // CA2000: the handler is owned by the client, and the client by the registration, which disposes it when
            // the registration ends.
#pragma warning disable CA2000
            return Enter(chosen, () => Client.CreateHttpClient(new ControllerStubHandler(stub)));
#pragma warning restore CA2000
        }

        /// <summary>
        /// Registers a fresh endpoint routed to the loopback stub host at <paramref name="baseAddress"/>, once that
        /// host has proved who it is. A host that fails the check is never registered, so no SOAP request reaches it,
        /// and neither is one whose check the caller cancelled.
        /// </summary>
        internal static async Task<ControllerStubRegistration> AddLoopbackAsync(Uri baseAddress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uri loopbackBase = ControllerStubLoopback.ValidatedBase(baseAddress);
            string endpoint = NextGeneratedEndpoint();
            HttpClient client = ControllerStubLoopback.CreateClient(loopbackBase, endpoint);
            try
            {
                await ControllerStubLoopback.PreflightAsync(client, loopbackBase, TimeProvider.System, cancellationToken)
                    .ConfigureAwait(false);
                return Enter(endpoint, () => client);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        private static ControllerStubRegistration Enter(string chosen, Func<HttpClient> transport)
        {
            string key = KeyOf(chosen);
            lock (Gate)
            {
                if (!Used.Add(key))
                {
                    throw new ErrorWithCodeException(Errors.CONTROLLER_STUB_ENDPOINT_IN_USE,
                        $"{nameof(Errors.CONTROLLER_STUB_ENDPOINT_IN_USE)}: {chosen} was already registered in this "
                        + "process. A mock:// key is single-use: register a key of its own, or pass no endpoint "
                        + "to get a generated one.");
                }

                var registration = new ControllerStubRegistration(chosen, transport());
                Live.Add(key, registration);
                return registration;
            }
        }

        /// <summary>
        /// True while any registration is live, which makes this a stub process: the latch then refuses every real
        /// endpoint, so a service built by hand from real settings cannot reach a controller during a stub run.
        /// </summary>
        internal static bool AnyLive
        {
            get
            {
                lock (Gate)
                {
                    return Live.Count > 0;
                }
            }
        }

        /// <summary>The latch's refusal of a real endpoint while a stub is registered.</summary>
        internal static ErrorWithCodeException SocketRefused(string serviceUrl) =>
            new(Errors.CONTROLLER_SOCKET_REFUSED_WHILE_STUBBED,
                $"{nameof(Errors.CONTROLLER_SOCKET_REFUSED_WHILE_STUBBED)}: a controller stub is registered in this "
                + $"process, so the call to {serviceUrl} was refused rather than sent to a real controller.");

        /// <summary>Removes <paramref name="registration"/> if it is still live; false when it already left.</summary>
        internal static bool Remove(ControllerStubRegistration registration)
        {
            string key = KeyOf(registration.Endpoint);
            lock (Gate)
            {
                return Live.TryGetValue(key, out ControllerStubRegistration? held)
                    && ReferenceEquals(held, registration)
                    && Live.Remove(key);
            }
        }

        /// <summary>
        /// The client that reaches the stub registered for the endpoint <paramref name="serviceUrl"/> was built
        /// from, or null when none is live.
        /// </summary>
        internal static HttpClient? TransportFor(string serviceUrl)
        {
            int marker = serviceUrl.LastIndexOf(ServicePathMarker, StringComparison.Ordinal);
            string key = KeyOf(marker < 0 ? serviceUrl : serviceUrl[..marker]);
            lock (Gate)
            {
                return Live.TryGetValue(key, out ControllerStubRegistration? registration) ? registration.Transport : null;
            }
        }

        /// <summary>
        /// Refuses to build a service on a <c>mock://</c> endpoint that no stub is registered for. Such an endpoint
        /// comes from a settings file, which no longer starts a stub: a shipped application carries none. Refusing
        /// at construction makes it the application's startup error, with the repair in the message, rather than a
        /// refusal on whichever call comes first.
        /// </summary>
        internal static void RefuseUnregisteredMockEndpoint(string endpoint)
        {
            if (!endpoint.StartsWith(SpecialEndpoints.MockedPrefix, StringComparison.Ordinal))
            {
                return;
            }
            lock (Gate)
            {
                if (Live.ContainsKey(KeyOf(endpoint)))
                {
                    return;
                }
            }
            throw new ErrorWithCodeException(Errors.CONTROLLER_STUB_NOT_REGISTERED,
                $"{nameof(Errors.CONTROLLER_STUB_NOT_REGISTERED)}: the settings name the controller-stub endpoint "
                + $"{endpoint}, but no stub is registered for it in this process, and an application carries no "
                + "stub of its own. Either change the endpoint in the settings file (ihcsettings.json) to a "
                + $"controller's address, or start the application with {ControllerStubs.SwitchArgument}=<address of a "
                + $"running controller stub>. A test registers its stub with {nameof(ControllerStubs)}.{nameof(ControllerStubs.Register)} "
                + "before it builds the services, and builds them on the registration's Endpoint.");
        }

        /// <summary>The refusal for a stub endpoint with no live registration.</summary>
        internal static ErrorWithCodeException NotRegistered(string serviceUrl) =>
            new(Errors.CONTROLLER_STUB_NOT_REGISTERED,
                $"{nameof(Errors.CONTROLLER_STUB_NOT_REGISTERED)}: no controller stub is registered in this "
                + $"process for {serviceUrl}, so the call was refused rather than sent. A test builds its services on "
                + "the Endpoint of a live registration, and disposes the registration only after them.");

        private static string NextGeneratedEndpoint()
        {
            long n = Interlocked.Increment(ref generated);
            return "http://s" + n.ToString(CultureInfo.InvariantCulture) + StubEndpoint.HostSuffix;
        }

        private static string ValidatedKey(string endpoint)
        {
            if (!endpoint.StartsWith(SpecialEndpoints.MockedPrefix, StringComparison.Ordinal)
                || endpoint[SpecialEndpoints.MockedPrefix.Length..].Trim('/').Length == 0)
            {
                throw new ErrorWithCodeException(Errors.CONTROLLER_STUB_ENDPOINT_INVALID,
                    $"{nameof(Errors.CONTROLLER_STUB_ENDPOINT_INVALID)}: a chosen stub endpoint must be a named "
                    + $"mock:// key, but was '{endpoint}'. Pass no endpoint to get a generated one.");
            }
            return endpoint;
        }

        // A trailing slash would otherwise make one endpoint two keys, since a service URL appends its own path.
        private static string KeyOf(string endpoint) => endpoint.TrimEnd('/');
    }
}
