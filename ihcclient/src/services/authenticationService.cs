using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Authentication;


namespace Ihc {
    /**
    * A highlevel client interface for the IHC AuthenticationService without any of the soap distractions.
    *
    * Status: 100% API coverage.
    */
    public interface IAuthenticationService : ICookieHandlerService, IDisposable, IAsyncDisposable
    {
        /*
        * Login to IHC controller. This method most be called prior to most other calls on other services.
        *
        * @userName Your registered IHC controller user name
        * @password Your registered IHC controller user name
        * @application Allowed applications seems to be "treeview", "openapi", "administrator" etc.
        */
        public Task<IhcUser> Authenticate(string userName, string password, string application = "openapi");

        /**
        * Logoff to IHC controller.
        */
        public Task<bool> Disconnect();

        /**
        * Check if the IHC controller is UP and running incl. serving API calls.
        */
        public Task<bool> Ping();
        
        /**
        * The IHC endpoint used (supplied to constructor in default implementation).
        */
        public string Endpoint { get; }

        /**
        * The logger used (supplied to constructor in default implementation).
        */
        public ILogger Logger { get; }
    }

    /**
    * A highlevel implementation of a client to the IHC AuthenticationService without exposing the soap distractions.
    */
    public class AuthenticationService : IAuthenticationService
    {
        private readonly ILogger logger;
        private readonly ICookieHandler cookieHandler;
        private readonly string endpoint;

        public ICookieHandler GetCookieHandler()
        {
            return cookieHandler;
        }

        public string Endpoint { 
          get {
            return endpoint;
          } 
        }

        public ILogger Logger { 
          get {
            return logger;
          } 
        }

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Authentication.AuthenticationService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "AuthenticationService") { }

            public Task<outputMessageName2> authenticateAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("authenticate", request, resp =>
                {
                    // Use side-effect to capture cookie sice our post call only captures xml response.
                    var cookie = resp.Headers.GetValues("Set-Cookie").FirstOrDefault();
                    if (cookie != null)
                    {
                        cookieHandler.SetCookie(cookie);
                    }
                });
            }

            public Task<outputMessageName1> disconnectAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("disconnect", request, resp =>
                {
                    cookieHandler.SetCookie(null);
                });
            }

            public Task<outputMessageName3> pingAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("ping", request);
            }
        }


        private readonly SoapImpl impl;
        private bool isConnected;

        /**
        * Create an AuthenticationService instance for access to the IHC API related to authentication.
        *
        * <param name="logger">A logger instance. Alternatively, use NullLogger<YourClass>.Instance</param>
        * <param name="endpoint">IHC controller endpoint of form http://\<YOUR CONTROLLER IP ADDRESS\></param>
        * 
        * NOTE: The AuthenticationService instance should be passed as an argument to other services (except OpenAPI).
        */
        public AuthenticationService(ILogger logger, string endpoint)
        {
            this.logger = logger;
            this.endpoint = endpoint;
            this.cookieHandler = new CookieHandler(logger);
            this.impl = new SoapImpl(logger, cookieHandler, endpoint);
        }

        public async Task<bool> Ping()
        {
            var resp = await impl.pingAsync(new inputMessageName3());
            var result = resp.ping1;
            return result.HasValue ? result.Value : false;
        }

        public async Task<IhcUser> Authenticate(string userName, string password, string application = "openapi")
        {
            logger.LogInformation("IHC Authenticate called");
            isConnected = false;
            var resp = await impl.authenticateAsync(new inputMessageName2() { authenticate1 = new WSAuthenticationData { username = userName, password = password, application = application } });
            var result = resp.authenticate2;
            if (result.loginWasSuccessful)
            {
                isConnected = true;

                return new IhcUser()
                {
                    Username = result.loggedInUser.username,
                    Password = result.loggedInUser.password,
                    Firstname = result.loggedInUser.firstname,
                    Lastname = result.loggedInUser.lastname,
                    Phone = result.loggedInUser.phone,
                    Group = result.loggedInUser.group.type,
                    Project = result.loggedInUser.project,
                    CreatedDate = result.loggedInUser.createdDate.ToDateTimeOffset(),
                    LoginDate = result.loggedInUser.loginDate.ToDateTimeOffset(),

                };
            }
            else if (result.loginFailedDueToAccountInvalid)
            {
                throw new ErrorWithCodeException(Errors.LOGIN_FAILED_DUE_TO_ACCOUNT_INVALID_ERROR, "Ihc server login reports invalid account for " + impl.Url);
            }
            else if (result.loginFailedDueToConnectionRestrictions)
            {
                throw new ErrorWithCodeException(Errors.LOGIN_FAILED_DUE_TO_CONNECTION_RESTRUCTIONS_ERROR, "Ihc server login reports connection restriction error for " + impl.Url);
            }
            else if (result.loginFailedDueToInsufficientUserRights)
            {
                throw new ErrorWithCodeException(Errors.LOGIN_FAILED_DUE_TO_INSUFFICIENT_USER_RIGHTS_ERROR, "Ihc server login reports invalid account for " + impl.Url);
            }
            else
            {
                throw new ErrorWithCodeException(Errors.LOGIN_UNKNOWN_ERROR, "Ihc server failed login for " + impl.Url);
            }
        }

        public async Task<bool> Disconnect()
        {
            logger.LogInformation("IHC Disconnect called");
            bool? result;

            try {
                var resp = await impl.disconnectAsync(new inputMessageName1());
                result = resp.disconnect1;
            } finally {
                isConnected = false;
            }
            
            return result.HasValue ? result.Value : false;
        }

        public void Dispose()
        {
            try {
                if (isConnected) {
                  Task.Run( () => this.Disconnect());
                }
            } catch (Exception) {} // Ignore
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Disconnect();
            GC.SuppressFinalize(this);
        }
    }
}