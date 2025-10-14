using System;
using System.IO;
using System.Reflection;
using Ihc;
using IhcLab;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

// Helper class to wrap services with display names
public class ServiceItem
{
    public IIHCService Service { get; }
    public string DisplayName { get; }

    public int InitialOperationSelectedIndex { get; set; }

    public ServiceItem(IIHCService service)
    {
        Service = service;
        DisplayName = service.GetType().Name;
        InitialOperationSelectedIndex = 1;
    }
}

public class IhcDomain
{

    static private readonly object _lock = new object();
    static private IhcDomain? _ihcDomainSingleton = null;

    static public IhcDomain GetOrCreateIhcDomain()
    {
        lock (_lock)
        {
            if (_ihcDomainSingleton == null)
            {
                _ihcDomainSingleton = new IhcDomain();
            }

            return _ihcDomainSingleton;
        }
    }

    static public void DisposeIhcDomain()
    {
        lock (_lock)
        {
            if (_ihcDomainSingleton != null)
            {
                _ihcDomainSingleton.Dispose();
                _ihcDomainSingleton = null;
            }
        }
    }


    public IhcSettings IhcSettings { get; init; }
    public ILoggerFactory loggerFactory { get; internal set; }

    public TracerProvider? TelmettryTracerProvider { get; internal set; }

    public AuthenticationService AuthenticationService { get; init; }
    public ControllerService ControllerService { get; init; }
    public ResourceInteractionService ResourceInteractionService { get; init; }
    public ConfigurationService ConfigurationService { get; init; }
    public OpenAPIService OpenAPIService { get; init; }
    public NotificationManagerService NotificationManagerService { get; init; }
    public MessageControlLogService MessageControlLogService { get; init; }
    public ModuleService ModuleService { get; init; }
    public TimeManagerService TimeManagerService { get; init; }
    public UserManagerService UserManagerService { get; init; }
    public AirlinkManagementService AirlinkManagementService { get; init; }

    public IIHCService[] AllIhcServices
    {
        get
        {
            return
            [
                AuthenticationService,
                ControllerService,
                ResourceInteractionService,
                ConfigurationService,
                OpenAPIService,
                NotificationManagerService,
                MessageControlLogService,
                ModuleService,
                TimeManagerService,
                UserManagerService,
                AirlinkManagementService
            ];
        }
    }

    private IhcDomain()
    {
        // Access configuration file that stores IHC and SDK setup informnation including username, password etc.
        string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory;
        IConfigurationRoot config = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("ihcsettings.json")
                    .Build();


        IConfigurationSection? loggingConfig = config.GetSection("Logging");
        TelemetryConfiguration? telemetryConfig = config.GetSection("telemetry").Get<TelemetryConfiguration>();
        if (telemetryConfig == null || loggingConfig == null)
        {
            throw new InvalidDataException("Could not read Telemtry client settings from configuration");
        }

         var loggerFactory = SetupTelemtryAndLoggingFactory(telemetryConfig, loggingConfig);

        // Developer tools attachment removed - requires additional Avalonia DevTools package
        // this.AttachDeveloperTools(o =>
        // {
        //     o.AddMicrosoftLoggerObservable(loggerFactory);
        // });

        var logger = loggerFactory.CreateLogger<IhcDomain>();

        var settings = config.GetSection("ihcclient").Get<IhcSettings>();
        if (settings == null)
        {
            throw new InvalidDataException("Could not read IHC client settings from configuration");
        }

        this.IhcSettings = settings;

        this.AuthenticationService = new AuthenticationService(logger, settings);
        this.ControllerService = new ControllerService(AuthenticationService);
        this.ResourceInteractionService = new ResourceInteractionService(AuthenticationService);
        this.ConfigurationService = new ConfigurationService(AuthenticationService);
        this.OpenAPIService = new OpenAPIService(logger, settings);
        this.NotificationManagerService = new NotificationManagerService(AuthenticationService);
        this.MessageControlLogService = new MessageControlLogService(AuthenticationService);
        this.ModuleService = new ModuleService(AuthenticationService);
        this.TimeManagerService = new TimeManagerService(AuthenticationService);
        this.UserManagerService = new UserManagerService(AuthenticationService);
        this.AirlinkManagementService = new AirlinkManagementService(AuthenticationService);
    }

    private ILoggerFactory SetupTelemtryAndLoggingFactory(TelemetryConfiguration config, IConfigurationSection loggingConfig)
    {
        string logsEndpoint = $"{config.Host}/api/{config.Organization}/v1/logs";
        string tracingEndpoint = $"{config.Host}/api/{config.Organization}/v1/traces";
        string metricsEndpoint = $"{config.Host}/api/{config.Organization}/v1/metrics";
        string headers = $"Authorization={config.Authentication}," +
                         $"stream-name={config.Stream}," +
                         $"organization={config.Organization}";


        // Create a logger for our application which delegates to Telemetry:
        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(loggingOpts =>
            {
                loggingOpts.IncludeFormattedMessage = true;
                loggingOpts.IncludeScopes = true;
                loggingOpts.AddOtlpExporter(opts =>
                {
                    opts.Endpoint = new Uri(logsEndpoint);
                    opts.Headers = headers;
                    opts.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                });
            });
    
            builder.AddConfiguration(loggingConfig);
        });

        // Setup tracing for our application 
        TelmettryTracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetErrorStatusOnException(true)
            .AddSource(Ihc.Telemetry.ActivitySourceName, IhcLab.Telemetry.ActivitySourceName)
            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri(tracingEndpoint);
                opts.Headers = headers;
                opts.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            }).Build();

        return loggerFactory;
    }
    
    public void Verify()
    {

    }


    private void Dispose()
    {
        AuthenticationService.Dispose();
        TelmettryTracerProvider?.Shutdown();
    }
}