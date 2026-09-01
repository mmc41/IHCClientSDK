using System;
using System.IO;
using System.Threading.Tasks;
using Ihc;
using Microsoft.Extensions.Configuration;
using Ihc.Bootstrap;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Ihc.download_upload_example
{      
    /// <summary>
    /// Download or upload an IHC project file
    /// </summary>
    class Program
    {
        public const string AppServiceName = "IhcProjectDownLoadUpLoad";
        public const string AppServiceNamespace = "Ihc";
        public const string ActivitySourceName = "IhcProjectDownLoadUpLoad";
    
        const string CMD_GET = "GET";
        const string CMD_STORE = "STORE";

        /// <summary>Everything asked for was done.</summary>
        private const int ExitOk = 0;

        /// <summary>Something the operator asked for did not happen.</summary>
        private const int ExitFailed = 1;

        /// <summary>The command line was not usable, so nothing was attempted.</summary>
        private const int ExitUsage = 2;

        /// <summary>
        /// Returns an EXIT CODE, because a script driving a controller has nothing else to read.
        /// </summary>
        /// <remarks>
        /// It used to return <c>Task</c>, so every path — a bad argument, a refused upload, a transport fault —
        /// exited 0, and the ONLY way the process ever exited non-zero was an unhandled throw from the logout in
        /// the finally. That inverted the signal exactly: a script saw failure when the upload and the reboot had
        /// both succeeded and only the logout had not.
        /// </remarks>
        static async Task<int> Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine($"Expected arguments: '{CMD_GET} <destfile>' OR '{CMD_STORE} <sourcefile>'");
                return ExitUsage;
            }

            // Invariant: the command words are ASCII, and a Turkish locale would fold 'i' to 'İ', leaving a
            // correctly typed command unrecognized on that machine alone.
            string command = args[0].ToUpperInvariant();
            string path = args[1];
            if (command != CMD_GET && command != CMD_STORE)
            {
                Console.WriteLine($"Illegal command. Expected {CMD_GET} or {CMD_STORE}");
                return ExitUsage;
            }

            if (command == CMD_STORE && !File.Exists(path))
            {
                Console.WriteLine("Could not find source project file  " + path);
                return ExitUsage;
            }

            // Read configuration settings
            IConfigurationRoot config = IhcConfiguration.FromAppDirectory();

            // Read configuration settings
            var settings = IhcSettings.GetFromConfiguration(config);
            var telemetryConfig = TelemetryConfiguration.GetFromConfiguration(config);

            // Create client for IHC services that this utility use:
            using var authService = new AuthenticationService(settings);
            var controllerService = new ControllerService(authService);
            var ressourceService = new ResourceInteractionService(authService);
            var configService = new ConfigurationService(authService);

            int exitCode = ExitOk;
            try
            {
                // Both providers, each gated on its configured endpoint, from the one builder every IHC host
                // shares (R7). The hand-rolled copy this replaces tested the endpoint INSIDE the exporter
                // callback, so an unconfigured utility still built a provider and still exported - to the OTLP
                // default endpoint. It also gains this utility metrics, which it never had.
                using ILoggerFactory loggerFactory = TelemetryBootstrap.SetupTelemetryAndLogging(
                    AppServiceName, AppServiceNamespace, ActivitySourceName, telemetryConfig, config);

                // Authenticate against IHC system. 
                var login = await authService.Authenticate();

                if (command == CMD_GET)
                {
                    ProjectFile project = await controllerService.GetProject()
                        ?? throw new InvalidOperationException("The controller returned no project - it likely has none stored.");
                    await File.WriteAllTextAsync(path, project.Data, ProjectFile.Encoding);
                    Console.WriteLine($"Downloaded project to {path}, size {project.Data.Length} characters (Org filename was {project.Filename})");
                }
                else if (command == CMD_STORE)
                {
                    var encoding = ProjectFile.Encoding;
                    ProjectFile project = new ProjectFile(
                        Filename: Path.GetFileName(path),
                        Data: await File.ReadAllTextAsync(path, encoding)
                    );

                    var projectContent = await File.ReadAllTextAsync(path);

                    // TODO: Read all runtime values and store them 

                    bool success = await controllerService.StoreProject(project);
                    if (!success)
                    {
                        // exitCode, not an early return: returning from here skipped the telemetry flush at the
                        // end of the method, so the one run most worth diagnosing exported nothing.
                        Console.WriteLine("Failed to store project to controller");
                        exitCode = ExitFailed;
                    }
                    else
                    {
                        // TODO: Reapply runtime values

                        // THE CONTROLLER HOLDS THE NEW PROJECT FROM HERE, and the old one is gone. Said BEFORE the
                        // reboot is attempted, because it is already true and it is the fact an operator most
                        // needs: the reboot is a separate call that the SDK documents as the caller's to make
                        // (ControllerService.StoreProject: "Does not reset runtime values or reboot controller").
                        Console.WriteLine($"Uploaded project from {path}, size {projectContent.Length} bytes.");

                        // Reboot controller to activate new project
                        try
                        {
                            await configService.DelayedReboot(100);
                            Console.WriteLine("Rebooting controller to activate it.");
                        }
                        catch (Exception ex)
                        {
                            // NOT an upload failure, and it used to be reported as one: the whole block shared a
                            // guard, so a reboot that timed out printed "Failed operation" and nothing else,
                            // leaving the operator to believe the controller was untouched.
                            //
                            // What it does NOT say is whether the reboot happened. A transport fault can land
                            // after the controller accepted the request, so from here the remote state is
                            // genuinely unknown — and an operator told "it is still running the old project"
                            // would act on a certainty nobody has.
                            Console.WriteLine(
                                "The project was uploaded, but the reboot could not be confirmed: "
                                + $"{ProblemConsoleFormat.Describe(ex)}");
                            Console.WriteLine(
                                "Check the controller: if it did not restart, restart it to activate the new project.");
                            exitCode = ExitFailed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // The SDK's own coded problem when it raised one — identity, its Danish sentence and its declared
                // arguments — instead of a bare English message that names no operation (R17: a non-GUI consumer
                // renders the contract too).
                Console.WriteLine($"Failed operation: {ProblemConsoleFormat.Describe(ex)}");
                exitCode = ExitFailed;
            }
            finally
            {
                // CONTAINED. An exception leaving a finally replaces whatever the method was returning, so a
                // failed logout used to be the one and only way this utility exited non-zero — reported over a
                // run whose actual work had succeeded. Logging out is housekeeping; it cannot make the upload
                // untrue, so it says so and changes nothing.
                try
                {
                    await authService.Disconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Note: logging out of the controller failed: {ProblemConsoleFormat.Describe(ex)}");
                }
            }

            // A console process exits as soon as this returns, so the flush happens here rather than in a
            // finalizer that will not run.
            TelemetryBootstrap.Shutdown();
            return exitCode;
        }
    }
}
