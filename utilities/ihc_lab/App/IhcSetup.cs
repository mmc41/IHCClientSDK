using System;
using Ihc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IhcLab;

/// <summary>
/// Lab's IHC services, always the real ones, built from the current settings. A controller stub is reached the
/// same way as a controller: its endpoint routes to the stub registered for it, and is refused when none is.
/// </summary>
public class IhcSetup
{
    public IhcSettings IhcSettings { get; init; }
    public ILoggerFactory loggerFactory { get; internal set; }

    public IAuthenticationService AuthenticationService { get; set; }
    public IControllerService ControllerService { get; set; }
    public IResourceInteractionService ResourceInteractionService { get; set; }
    public IConfigurationService ConfigurationService { get; set; }
    public IOpenAPIService OpenAPIService { get; set; }
    public INotificationManagerService NotificationManagerService { get; set; }
    public IMessageControlLogService MessageControlLogService { get; set; }
    public IModuleService ModuleService { get; set; }
    public ITimeManagerService TimeManagerService { get; set; }
    public IUserManagerService UserManagerService { get; set; }
    public IAirlinkManagementService AirlinkManagementService { get; set; }

    public ISmsModemService SmsModemService { get; set; }
    public IInternalTestService InternalTestService { get; set; }
    public ILedDimmerManagementService LedDimmerManagementService { get; set; }

    public IIHCApiService[] AllIhcServices
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
                AirlinkManagementService,
                SmsModemService,
                InternalTestService,
                LedDimmerManagementService
            ];
        }
    }

    // CS8618: Non-nullable field must contain a non-null value when exiting constructor.
    // Suppression is safe here because UpdateSetup() is called immediately in constructor
    // and will initialize all service fields. If UpdateSetup() throws, the object is not used.
#pragma warning disable CS8618
    public IhcSetup()
    {
        this.loggerFactory = Program.loggerFactory ?? new NullLoggerFactory();
        this.IhcSettings = Program.config?.ihcSettings ?? new IhcSettings();

        // This will initialize all service properties
        UpdateSetup();
    }
#pragma warning restore CS8618

    /// <summary>
    /// Rebuilds every service from the current settings, all or none: a refused endpoint replaces nothing, so the
    /// services in place never become a mix of two endpoints.
    /// </summary>
    public void UpdateSetup()
    {
        if (IhcSettings.Endpoint == null)
            throw new InvalidOperationException("IhcSettings.Endpoint is null in IhcDomain UpdateSetup");

        var authentication = new AuthenticationService(IhcSettings);
        try
        {
            var controller = new ControllerService(authentication);
            var resourceInteraction = new ResourceInteractionService(authentication);
            var configuration = new ConfigurationService(authentication);
            var openApi = new OpenAPIService(IhcSettings);
            var notificationManager = new NotificationManagerService(authentication);
            var messageControlLog = new MessageControlLogService(authentication);
            var module = new ModuleService(authentication);
            var timeManager = new TimeManagerService(authentication);
            var userManager = new UserManagerService(authentication);
            var airlinkManagement = new AirlinkManagementService(authentication);
            var smsModem = new SmsModemService(authentication);
            var internalTest = new InternalTestService(authentication);
            var ledDimmerManagement = new LedDimmerManagementService(authentication);

            this.AuthenticationService = authentication;
            this.ControllerService = controller;
            this.ResourceInteractionService = resourceInteraction;
            this.ConfigurationService = configuration;
            this.OpenAPIService = openApi;
            this.NotificationManagerService = notificationManager;
            this.MessageControlLogService = messageControlLog;
            this.ModuleService = module;
            this.TimeManagerService = timeManager;
            this.UserManagerService = userManager;
            this.AirlinkManagementService = airlinkManagement;
            this.SmsModemService = smsModem;
            this.InternalTestService = internalTest;
            this.LedDimmerManagementService = ledDimmerManagement;
        }
        catch
        {
            // Never logged in, so disposing it sends nothing; it only releases the session it would have held.
            authentication.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        AuthenticationService?.Dispose();
    }
}
