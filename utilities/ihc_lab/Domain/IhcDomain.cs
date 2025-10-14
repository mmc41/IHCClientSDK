using System;
using System.IO;
using System.Reflection;
using Ihc;
using ihc_lab;
using IhcLab;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
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
        var loggerFactory = Program.loggerFactory;
        if (loggerFactory == null)
          throw new Exception("logger factory not set");

        var config = Program.config;
        if (config == null)
          throw new Exception("configuration not set");

        var settings = config.ihcSettings;
        if (settings == null)
        {
            throw new InvalidDataException("Could not read IHC client settings from configuration");
        }

        this.IhcSettings = settings;
        this.loggerFactory = loggerFactory;

        var logger = loggerFactory.CreateLogger<IhcDomain>();

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

    private void Dispose()
    {
        AuthenticationService.Dispose();
        // TelmetryTracerProvider?.Shutdown();
    }
}