using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Authentication;
using System.Diagnostics;


namespace Ihc {
    /**
    * A highlevel client interface for the IHC AuthenticationService without any of the soap distractions.
    */
    public interface IAuthenticationService : ICookieHandlerService, IDisposable, IAsyncDisposable, IIHCService
    {
        /**
        * Login to IHC controller. This method must be called prior to most other calls on other services.
        *
        * @param userName Your registered IHC controller user name
        * @param password Your registered IHC controller password
        * @param application Allowed applications: "treeview", "openapi", "administrator"
        */
        public Task<IhcUser> Authenticate(string userName, string password, string application = "openapi");

        /**
        * Logout from IHC controller and clear session cookie.
        */
        public Task<bool> Disconnect();

        /**
        * Check if the IHC controller is up and running and serving API calls.
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
    public class AuthenticationService : ServiceBase, IAuthenticationService
    {
        private readonly ICookieHandler cookieHandler;
        private readonly string endpoint;
        private readonly bool logSensitiveData;

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
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint, bool logSensitiveData, bool asyncContinueOnCapturedContext) : base(logger, cookieHandler, endpoint, "AuthenticationService", logSensitiveData, asyncContinueOnCapturedContext) { }

            public Task<outputMessageName2> authenticateAsync(inputMessageName2 request)
            {
                string cookie = null;

                var result = soapPost<outputMessageName2, inputMessageName2>("authenticate", request, resp =>
                {
                    // Use side-effect to capture cookie sice our post call only captures xml response.
                    cookie = resp.Headers.GetValues("Set-Cookie").FirstOrDefault();
                });

                return result.ContinueWith((r) =>
                {
                    var response = r.Result;

                    // Add cookie only on success.
                    if (response.authenticate2?.loginWasSuccessful == true)
                    {
                        cookieHandler.SetCookie(cookie);
                    } else
                    {
                        cookieHandler.SetCookie(null);
                    }

                    return response;
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

        /// <summary>
        /// Create an AuthenticationService instance for access to the IHC API related to authentication.
        /// NOTE: The AuthenticationService instance should be passed as an argument to other services (except OpenAPI).
        /// </summary>
        /// <param name="logger">A logger instance. Alternatively, use NullLogger&lt;YourClass&gt;.Instance</param>
        /// <param name="endpoint">IHC controller endpoint of form http://&lt;YOUR CONTROLLER IP ADDRESS&gt;</param>
        /// <param name="logSensitiveData">If true, log passwords and session cookies. If false (default), redact sensitive values in logs.
        ///                                WARNING: Enabling this will expose credentials in logs. Only enable for debugging in secure environments.</param>
        /// <param name="asyncContinueOnCapturedContext">If true, continue on captured context for async operations.</param>
        public AuthenticationService(ILogger logger, string endpoint, bool logSensitiveData = false, bool asyncContinueOnCapturedContext = false)
            : base(logger, logSensitiveData, asyncContinueOnCapturedContext)
        {
            this.endpoint = endpoint;
            this.logSensitiveData = logSensitiveData;
            this.cookieHandler = new CookieHandler(logger, logSensitiveData);
            this.impl = new SoapImpl(logger, cookieHandler, endpoint, logSensitiveData, asyncContinueOnCapturedContext);
            this.isConnected = false;
        }

        public async Task<bool> Ping()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.pingAsync(new inputMessageName3()).ConfigureAwait(asyncContinueOnCapturedContext);
            var result = resp.ping1;
            var retv = result.HasValue ? result.Value : false;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<IhcUser> Authenticate(string userName, string password, string application = "openapi")
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(
                ("userName", userName),
                ("password", logSensitiveData ? password : "***REDACTED***"),
                ("application", application)
            );

            logger.LogInformation("IHC Authenticate called");
            isConnected = false;
            var resp = await impl.authenticateAsync(new inputMessageName2() { authenticate1 = new WSAuthenticationData { username = userName, password = password, application = application } })
                                 .ConfigureAwait(asyncContinueOnCapturedContext);

            var result = resp.authenticate2;
            if (result.loginWasSuccessful)
            {
                // Add null checks for loggedInUser and nested properties
                if (result.loggedInUser == null)
                {
                    throw new ErrorWithCodeException(Errors.LOGIN_UNKNOWN_ERROR, "Ihc server login succeeded but returned null user data for " + impl.Url);
                }

                isConnected = true;

                var user = new IhcUser()
                {
                    Username = result.loggedInUser.username,
                    Password = result.loggedInUser.password,
                    Firstname = result.loggedInUser.firstname,
                    Lastname = result.loggedInUser.lastname,
                    Phone = result.loggedInUser.phone,
                    Group = result.loggedInUser.group?.type,
                    Project = result.loggedInUser.project,
                    CreatedDate = result.loggedInUser.createdDate.ToDateTimeOffset(),
                    LoginDate = result.loggedInUser.loginDate.ToDateTimeOffset(),

                };
                logger.LogInformation($"Successfully authenticated user: {user.Username}");

                activity?.SetReturnValue(user);
                return user;
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
                throw new ErrorWithCodeException(Errors.LOGIN_FAILED_DUE_TO_INSUFFICIENT_USER_RIGHTS_ERROR, "Ihc server login reports insufficient user rights for " + impl.Url);
            }
            else
            {
                throw new ErrorWithCodeException(Errors.LOGIN_UNKNOWN_ERROR, "Ihc server failed login for " + impl.Url);
            }
        }

        public async Task<bool> Disconnect()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            logger.LogInformation("IHC Disconnect called");
            bool? result;

            try
            {
                var resp = await impl.disconnectAsync(new inputMessageName1()).ConfigureAwait(asyncContinueOnCapturedContext);
                result = resp.disconnect1;
            }
            finally
            {
                isConnected = false;
            }

            var retv = result.HasValue ? result.Value : false;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public void Dispose()
        {
            try
            {
                if (isConnected)
                {
                    Task.Run(() => this.Disconnect());
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Exception during disconnect in Dispose");
            } // Ignore
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Disconnect().ConfigureAwait(asyncContinueOnCapturedContext);
            GC.SuppressFinalize(this);
        }
    }
}