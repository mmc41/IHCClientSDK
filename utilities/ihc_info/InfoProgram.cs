using Ihc.App;
using Microsoft.Extensions.Configuration;
using Ihc.Bootstrap;
using Ihc.Stubbing;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ihc.example
{
    /// <summary>
    /// Read IHC system information such as system version, license info, number of users, modules and resources.
    /// </summary>
    sealed class Program
    {
        // Built once: JsonSerializerOptions caches the reflection metadata it derives on first use, and a
        // fresh instance per call throws that cache away. Shared safely because it is never mutated after
        // construction, which is what makes the type thread-safe.
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public const string AppServiceName = "IhcInfoConsole";
        public const string AppServiceNamespace = "Ihc";
        // Equal to AppServiceName, as in every other host. They were "IhcInfoConsole" and "IhcInfo",
        // so this utility reported one identity as its service and another as its scope.
        public const string ActivitySourceName = AppServiceName;
        public static ActivitySource ActivitySource { get; } =
            new ActivitySource(name: ActivitySourceName, version: TelemetryBootstrap.GetAppVersionStr());

        /// <summary>The command line was not usable, so nothing was attempted.</summary>
        private const int ExitUsage = 2;

        static async Task Main(string[] args)
        {
            // Before the settings file is read, so a command line this utility cannot use is answered with its usage
            // whether or not that file exists. An argument it ignored could be a mistyped option someone meant
            // something by. The controller-stub switch, in any spelling, is left to ApplySwitch below.
            string[] unexpected = [.. args.Where(argument => !ControllerStubs.SwitchRequested([argument]))];
            if (unexpected.Length > 0)
            {
                Console.WriteLine($"Expected no arguments but {ControllerStubs.SwitchArgument}=http://127.0.0.1:<port>/<token>/, "
                    + "which is optional; got " + string.Join(' ', unexpected));
                Environment.ExitCode = ExitUsage;
                return;
            }

            IConfigurationRoot config = IhcConfiguration.FromAppDirectory();

            // Read configuration settings
            var settings = IhcSettings.GetFromConfiguration(config);
            var telemetryConfig = TelemetryConfiguration.GetFromConfiguration(config);

            try
            {
                // Both providers, each gated on its configured endpoint, from the one builder every IHC host
                // shares (R7). The hand-rolled copy this replaces tested the endpoint INSIDE the exporter
                // callback, so an unconfigured utility still built a provider and still exported - to the OTLP
                // default endpoint. It also gains this utility metrics, which it never had.
                using ILoggerFactory loggerFactory = TelemetryBootstrap.SetupTelemetryAndLogging(
                    AppServiceName, AppServiceNamespace, ActivitySourceName, telemetryConfig, config,
                    ControllerStubs.SwitchRequested(args));

                // After the pipeline, so the stub's identity check is traced; before the first service is built. A
                // refused switch lands in the catch below, with no fallback to the file's endpoint.
                (settings, _) = ControllerStubs.ApplySwitch(args, settings);
                ControllerStubConsole.AnnounceIfStub(settings);

                // Create client information app service.
                using (InformationAppService infoService = new InformationAppService(settings))
                {
                    var info = await infoService.GetInformationModel();

                    string json = JsonSerializer.Serialize(info, JsonOptions);

                    Console.WriteLine($"IHC information: {json}");
                }
            } catch (Exception ex)
            {
                Console.WriteLine($"Failed operation: {ex.Message} : {ex.StackTrace}");
                // Main returns normally either way, so without this a refused switch or a failed read would report
                // success to whatever started the utility.
                Environment.ExitCode = 1;
            }

            // A console process exits as soon as this returns, so the flush happens here rather than in a
            // finalizer that will not run.
            TelemetryBootstrap.Shutdown();
        }
    }
}

