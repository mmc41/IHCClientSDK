using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ihc.Stubbing
{
    /// <summary>
    /// Serves one controller stub over HTTP on a loopback port, so an SDK in another process can be pointed at it
    /// with <see cref="ControllerStubs.RegisterLoopbackAsync"/>. It only listens: it opens no connection of its own.
    /// </summary>
    /// <remarks>
    /// Its address carries a random token, and every answer under that address carries the token in a header, which
    /// is how the SDK tells this host from a real controller that a tunnel or a port-forward put on loopback.
    /// </remarks>
    public sealed class ControllerStubLoopbackHost : IAsyncDisposable
    {
        // The SDK keeps its own copy of each of these, internal to it. A drift between the copies fails the loopback
        // tests, which register through the SDK and read the defect code it raises.
        internal const string IdentityHeader = "X-Ihc-Controller-Stub";
        internal const string IdentifyPath = "identify";
        internal const string IdentityAnswerPrefix = "ihcclient-controller-stub/1 ";
        internal const string DefectHeader = "X-Ihc-Controller-Stub-Fault";
        internal const string DefectValue = "defect";

        private const string ServicePath = "ws/";
        private const string SoapActionHeader = "SOAPAction";

        private readonly WebApplication app;
        private readonly IControllerStub stub;
        private readonly string token;
        private int identifyRequests;

        private ControllerStubLoopbackHost(WebApplication app, IControllerStub stub, string token)
        {
            this.app = app;
            this.stub = stub;
            this.token = token;
        }

        /// <summary>The address to register, <c>http://127.0.0.1:&lt;port&gt;/&lt;token&gt;/</c>.</summary>
        public Uri BaseAddress { get; private set; } = null!;

        /// <summary>How many identity checks this host has answered; a registration makes exactly one.</summary>
        public int IdentifyRequests => Volatile.Read(ref identifyRequests);

        /// <summary>Starts serving <paramref name="stub"/> on an ephemeral loopback port.</summary>
        /// <param name="stub">The stub that answers every SOAP request sent to this host.</param>
        /// <param name="cancellationToken">Cancels the start.</param>
        /// <returns>The running host. Dispose it to stop listening.</returns>
        public static async Task<ControllerStubLoopbackHost> StartAsync(IControllerStub stub, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stub);
            // The empty builder registers no logging provider, so the host writes nothing to the console of a process
            // whose own output a person reads.
            WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
            builder.WebHost.UseKestrelCore().ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            WebApplication app = builder.Build();
            var host = new ControllerStubLoopbackHost(app, stub, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)));
            ((IApplicationBuilder)app).Run(host.HandleAsync);
            try
            {
                await app.StartAsync(cancellationToken).ConfigureAwait(false);
                // Kestrel always publishes the addresses it bound.
                string bound = app.Services.GetRequiredService<IServer>().Features
                    .Get<IServerAddressesFeature>()!.Addresses.Single();
                host.BaseAddress = new Uri(bound.TrimEnd('/') + "/" + host.token + "/");
                return host;
            }
            catch
            {
                await app.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>Stops listening.</summary>
        public async ValueTask DisposeAsync()
        {
            await app.StopAsync(CancellationToken.None).ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
        }

        private async Task HandleAsync(HttpContext context)
        {
            string prefix = "/" + token + "/";
            string path = context.Request.Path.Value ?? string.Empty;
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
            {
                // Outside the token's address nothing is answered, and nothing proves this host's identity.
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            context.Response.Headers[IdentityHeader] = token;
            string rest = path[prefix.Length..];

            if (HttpMethods.IsGet(context.Request.Method) && rest == IdentifyPath)
            {
                Interlocked.Increment(ref identifyRequests);
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync(IdentityAnswerPrefix + token, context.RequestAborted).ConfigureAwait(false);
                return;
            }
            if (HttpMethods.IsPost(context.Request.Method) && rest.StartsWith(ServicePath, StringComparison.Ordinal))
            {
                await AnswerAsync(context, rest[ServicePath.Length..]).ConfigureAwait(false);
                return;
            }
            context.Response.StatusCode = StatusCodes.Status404NotFound;
        }

        /// <summary>
        /// Hands one SOAP request to the stub as the in-process handler does, and writes its answer back with the
        /// content type the in-process handler gives it, so both transports present the SDK with the same response.
        /// </summary>
        private async Task AnswerAsync(HttpContext context, string service)
        {
            string body;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
            }
            string? cookie = context.Request.Headers.Cookie.Count > 0 ? context.Request.Headers.Cookie.ToString() : null;
            var request = new ControllerStubRequest(service, context.Request.Headers[SoapActionHeader].ToString(), body, cookie);

            ControllerStubResponse? answer;
            try
            {
                answer = await stub.HandleAsync(request, context.RequestAborted).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // A cancellation is the caller's, never the stub's fault, so the request just ends.
                context.Abort();
                return;
            }
#pragma warning disable CA1031 // Any exception from the stub is a defect, and the client is told so rather than left waiting.
            catch (Exception)
#pragma warning restore CA1031
            {
                AnswerDefect(context);
                return;
            }

            if (answer is null)
            {
                AnswerDefect(context);
                return;
            }
            if (answer.IsConnectionLost)
            {
                // A real transport failure, which is what a lost connection is to the client.
                context.Abort();
                return;
            }

            context.Response.StatusCode = answer.StatusCode;
            context.Response.ContentType = "text/xml; charset=utf-8";
            if (answer.SetCookie is not null)
            {
                context.Response.Headers.SetCookie = answer.SetCookie;
            }
            await context.Response.WriteAsync(answer.Body, Encoding.UTF8, context.RequestAborted).ConfigureAwait(false);
        }

        /// <summary>
        /// Tells the client the stub broke, apart from every answer the stub meant to give: the SDK turns the header
        /// into the same defect code an in-process call raises. A <see cref="ControllerStub"/> has already recorded
        /// the exception among its defects.
        /// </summary>
        private static void AnswerDefect(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.Headers[DefectHeader] = DefectValue;
        }
    }
}
