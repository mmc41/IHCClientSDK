using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Ihc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Threading.Tasks;

namespace IhcLab;

// Helper class to wrap services with display names
public class ServiceItem
{
    public IIHCService Service { get; }
    public string DisplayName { get; }

    public int InitialOperationSelectedIndex { get; set; }

    public ServiceItem(IIHCService service)
    {
        Service = service;

        // Find the IHC service interface (works for both real services and fakes)
        var serviceInterfaces = service.GetType().GetInterfaces()
            .Where(i => i != typeof(IIHCService) && typeof(IIHCService).IsAssignableFrom(i))
            .ToList();

        // Use the interface name, stripping the leading 'I' for display
        if (serviceInterfaces.Count > 0)
        {
            var interfaceName = serviceInterfaces[0].Name;
            DisplayName = interfaceName.StartsWith("I") ? interfaceName.Substring(1) : interfaceName;
        }
        else
        {
            // Fallback to type name if no interface found
            DisplayName = service.GetType().Name;
        }

        InitialOperationSelectedIndex = 0;
    }
}

public class IhcDomain
{
    public IhcSettings IhcSettings { get; init; }
    public ILoggerFactory loggerFactory { get; internal set; }

    public IAuthenticationService AuthenticationService { get; init; }
    public IControllerService ControllerService { get; init; }
    public IResourceInteractionService ResourceInteractionService { get; init; }
    public IConfigurationService ConfigurationService { get; init; }
    public IOpenAPIService OpenAPIService { get; init; }
    public INotificationManagerService NotificationManagerService { get; init; }
    public IMessageControlLogService MessageControlLogService { get; init; }
    public IModuleService ModuleService { get; init; }
    public ITimeManagerService TimeManagerService { get; init; }
    public IUserManagerService UserManagerService { get; init; }
    public IAirlinkManagementService AirlinkManagementService { get; init; }

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

    public IhcDomain()
    {
        var loggerFactory = Program.loggerFactory;

        var config = Program.config;

        var settings = config?.ihcSettings ?? new IhcSettings();

        this.IhcSettings = settings;
        this.loggerFactory = loggerFactory ?? new NullLoggerFactory();

        if (settings == null)
            throw new Exception("IhcSettings is null in IhcDomain constructor");  
        if (settings.Endpoint == null)
            throw new Exception("IhcSettings.Endpoint is null in IhcDomain constructor");

        if (!settings.Endpoint.StartsWith(SpecialEndpoints.MockedPrefix))
        {
            // Real services by default:
            this.AuthenticationService = new AuthenticationService(settings);
            this.ControllerService = new ControllerService(AuthenticationService);
            this.ResourceInteractionService = new ResourceInteractionService(AuthenticationService);
            this.ConfigurationService = new ConfigurationService(AuthenticationService);
            this.OpenAPIService = new OpenAPIService(settings);
            this.NotificationManagerService = new NotificationManagerService(AuthenticationService);
            this.MessageControlLogService = new MessageControlLogService(AuthenticationService);
            this.ModuleService = new ModuleService(AuthenticationService);
            this.TimeManagerService = new TimeManagerService(AuthenticationService);
            this.UserManagerService = new UserManagerService(AuthenticationService);
            this.AirlinkManagementService = new AirlinkManagementService(AuthenticationService);
        }
        else
        {
            // All services can be faked. This may at first seem out of place,
            // but it allows for easy human explorative testing of the GUI aspects of
            // the app without being connected to a real IHC system. ALso allows for 
            // safe automated testing og the GUI without a real IHC system.

            this.AuthenticationService = IhcFakeSetup.SetupAuthenticationService(IhcSettings);
            this.ControllerService = IhcFakeSetup.SetupControllerService(IhcSettings);
            this.ResourceInteractionService = IhcFakeSetup.SetupResourceInteractionService(IhcSettings);
            this.ConfigurationService = IhcFakeSetup.SetupConfigurationService(IhcSettings);
            this.OpenAPIService = IhcFakeSetup.SetupOpenAPIService(IhcSettings);
            this.NotificationManagerService = IhcFakeSetup.SetupNotificationManagerService(IhcSettings);
            this.MessageControlLogService = IhcFakeSetup.SetupMessageControlLogService(IhcSettings);
            this.ModuleService = IhcFakeSetup.SetupModuleService(IhcSettings);
            this.TimeManagerService = IhcFakeSetup.SetupTimeManagerService(IhcSettings);
            this.UserManagerService = IhcFakeSetup.SetupUserManagerService(IhcSettings);
            this.AirlinkManagementService = IhcFakeSetup.SetupAirlinkManagementService(IhcSettings);
        }
       
    }

    public void Dispose()
    {
        AuthenticationService.Dispose();
        // TelmetryTracerProvider?.Shutdown();
    }
}