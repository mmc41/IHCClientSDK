using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Diagnostics;


namespace Ihc {
    /// <summary>
    /// A highlevel client interface for the IHC AuthenticationService without any of the soap distractions.
    /// </summary>
    public interface IAuthenticationService : ICookieHandlerService, IDisposable, IAsyncDisposable, IIHCApiService
    {
        /// <summary>
        /// Login to IHC controller with user/password and application in predefined configuration settings. This method must be called prior to most other calls on other services.
        /// </summary>
        public Task<IhcUser> Authenticate();

        /// <summary>
        /// Login to IHC controller overriding user/password and application in predefined configuration settings. This method must be called prior to most other calls on other services.
        /// </summary>
        /// <param name="userName">Your registered IHC controller user name</param>
        /// <param name="password">Your registered IHC controller password</param>
        /// <param name="application">Allowed applications: "treeview", "openapi", "administrator"</param>
        public Task<IhcUser> Authenticate(string userName, string password, string application = "openapi");

        /// <summary>
        /// Logout from IHC controller and clear session cookie.
        /// </summary>
        public Task<bool> Disconnect();

        /// <summary>
        /// Check if the IHC controller is up and running and serving API calls.
        /// </summary>
        public Task<bool> Ping();

        /// <summary>
        /// Check if the client is currently authenticated with the IHC controller.
        /// Returns true if Authenticate() was successfully called and Disconnect() has not been called since.
        /// </summary>
        /// <returns>True if authenticated, false otherwise</returns>
        public Task<bool> IsAuthenticated();
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC AuthenticationService without exposing the soap distractions.
    /// </summary>
    public class AuthenticationService : ServiceBase, IAuthenticationService
    {
        private readonly ICookieHandler cookieHandler;

        public ICookieHandler GetCookieHandler()
        {
            return cookieHandler;
        }

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Authentication.AuthenticationService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, "AuthenticationService") { }

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
        private readonly object isConnectedLock = new object();
        private volatile bool isConnected;

        /// <summary>
        /// Create an AuthenticationService instance for access to the IHC API related to authentication.
        /// NOTE: The AuthenticationService instance should be passed as an argument to other services (except OpenAPI).
        /// </summary>
        /// <param name="settings">IHC settings configuration</param>
        public AuthenticationService(IhcSettings settings)
            : base(settings)
        {
            this.cookieHandler = new CookieHandler(settings.LogSensitiveData);
            this.impl = new SoapImpl(cookieHandler, settings);
            this.isConnected = false;
        }

        public async Task<bool> Ping()
        {
            using (var activity = StartActivity(nameof(Ping)))
            {
                try
                {
                    var resp = await impl.pingAsync(new inputMessageName3()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var result = resp.ping1;
                    var retv = result.HasValue ? result.Value : false;

                    activity?.SetReturnValue(retv);
                     return retv;
                } catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IhcUser> Authenticate()
        {
            return await Authenticate(settings.UserName, settings.Password, settings.Application);
        }

        public async Task<IhcUser> Authenticate(string userName, string password, string application = "openapi")
        {
            using (var activity = StartActivity(nameof(Authenticate))) {
                try
                {
                    activity?.SetParameters(
                        (nameof(userName), userName),
                        (nameof(password), settings.LogSensitiveData ? password : UserConstants.REDACTED_PASSWORD),
                        (nameof(application), application)
                    );

                    lock (isConnectedLock)
                    {
                        isConnected = false;
                    }

                    var resp = await impl.authenticateAsync(new inputMessageName2() { authenticate1 = new WSAuthenticationData { username = userName, password = password, application = application } })
                                        .ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    var result = resp.authenticate2;
                    if (result.loginWasSuccessful)
                    {
                        // Add null checks for loggedInUser and nested properties
                        if (result.loggedInUser == null)
                        {
                            throw new ErrorWithCodeException(Errors.LOGIN_UNKNOWN_ERROR, "Ihc server login succeeded but returned null user data for " + impl.Url);
                        }

                        lock (isConnectedLock)
                        {
                            isConnected = true;
                        }

                        var user = new IhcUser()
                        {
                            Username = result.loggedInUser.username,
                            Password = result.loggedInUser.password,
                            Firstname = result.loggedInUser.firstname,
                            Lastname = result.loggedInUser.lastname,
                            Phone = result.loggedInUser.phone,
                            Group = UserManagerService.mapUserGroup(result.loggedInUser.group?.type),
                            Project = result.loggedInUser.project,
                            CreatedDate = result.loggedInUser.createdDate.ToDateTimeOffset(),
                            LoginDate = result.loggedInUser.loginDate.ToDateTimeOffset(),

                        };

                        activity?.SetReturnValue(user.ToString(settings.LogSensitiveData));
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
                } catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            };
        }

        public async Task<bool> Disconnect()
        {
            using (var activity = StartActivity(nameof(Disconnect)))
            {
                try
                {
                    bool? result;

                    try
                    {
                        var resp = await impl.disconnectAsync(new inputMessageName1()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        result = resp.disconnect1;
                    }
                    finally
                    {
                        lock (isConnectedLock)
                        {
                            isConnected = false;
                        }
                    }

                    var retv = result.HasValue ? result.Value : false;

                    activity?.SetReturnValue(retv);
                    return retv;
                } catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        #pragma warning disable 1998
        public async Task<bool> IsAuthenticated()
        {
            using (var activity = StartActivity(nameof(IsAuthenticated)))
            {
                try
                {
                    bool retv;
                    lock (isConnectedLock)
                    {
                        retv = isConnected;
                    }
                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public void Dispose()
        {
            try
            {
                // Block synchronously - this ensures cleanup completes
                DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // Ignore exceptions during dispose
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                bool shouldDisconnect;
                lock (isConnectedLock)
                {
                    shouldDisconnect = isConnected;
                }

                if (shouldDisconnect)
                {
                    await Disconnect().ConfigureAwait(false);
                }
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }
    }
}