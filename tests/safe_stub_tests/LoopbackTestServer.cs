using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ihc.Tests
{
    /// <summary>
    /// A loopback HTTP server that answers however a case tells it to, standing in for what a loopback registration
    /// must refuse: something on this machine that is not the stub host it was told to expect.
    /// </summary>
    internal sealed class LoopbackTestServer : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private int _requests;

        private LoopbackTestServer(WebApplication app) => _app = app;

        /// <summary>The server's address, <c>http://127.0.0.1:&lt;port&gt;</c>, without a trailing slash.</summary>
        internal string Address { get; private set; } = string.Empty;

        /// <summary>How many requests reached the server.</summary>
        internal int Requests => Volatile.Read(ref _requests);

        internal static async Task<LoopbackTestServer> StartAsync(Func<HttpContext, LoopbackTestServer, Task> answer)
        {
            WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
            builder.WebHost.UseKestrelCore().ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            WebApplication app = builder.Build();
            var server = new LoopbackTestServer(app);
            ((IApplicationBuilder)app).Run(context =>
            {
                Interlocked.Increment(ref server._requests);
                return answer(context, server);
            });
            await app.StartAsync();
            // Kestrel always publishes the addresses it bound.
            server.Address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
                .Addresses.Single().TrimEnd('/');
            return server;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
