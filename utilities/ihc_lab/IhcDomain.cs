using System;
using System.IO;
using System.Reflection;
using Ihc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


// Helper class to wrap services with display names
public class ServiceItem
{
    public IIHCService Service { get; }
    public string DisplayName { get; }

    public ServiceItem(IIHCService service)
    {
        Service = service;
        DisplayName = service.GetType().Name;
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
    public ILoggerFactory loggerFactory { get; init; }

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

        // Create a logger for our application. Alternatively use NullLogger<Setup>.Instance.
        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConfiguration(config.GetSection("Logging"));
            builder.AddConsole();
        });

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

    public void Verify()
    {

    }


    private void Dispose()
    {
        AuthenticationService.Dispose();
    }
}