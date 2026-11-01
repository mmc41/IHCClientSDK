using System;
using System.Net.Http;

namespace Ihc.Stubbing
{
    /// <summary>
    /// One live controller stub in this process, reached through <see cref="Endpoint"/>. Dispose it once the
    /// services built on it are done.
    /// </summary>
    /// <remarks>
    /// A call that starts after <see cref="Dispose"/> returns is refused with
    /// <see cref="Errors.CONTROLLER_STUB_NOT_REGISTERED"/>; a call already in flight may end in
    /// <see cref="OperationCanceledException"/> or <see cref="ObjectDisposedException"/>. None of them reaches a
    /// controller.
    /// </remarks>
    public sealed class ControllerStubRegistration : IDisposable
    {
        private readonly HttpClient transport;

        // Explicit and internal: only the registry may make a registration, and a primary constructor would be
        // as public as the class.
        internal ControllerStubRegistration(string endpoint, HttpClient transport)
        {
            Endpoint = endpoint;
            this.transport = transport;
        }

        /// <summary>The endpoint to put in the settings of every service that should talk to this stub.</summary>
        public string Endpoint { get; }

        /// <summary>The client every exchange to <see cref="Endpoint"/> is sent through.</summary>
        internal HttpClient Transport => transport;

        /// <summary>
        /// Ends this registration. Disposing it again does nothing, and never releases another registration.
        /// </summary>
        public void Dispose()
        {
            if (ControllerStubRegistry.Remove(this))
            {
                // Ends any call still held by the stub, so nothing outlives the registration it was routed by.
                transport.Dispose();
            }
        }
    }
}
