using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Diagnostics;
using System.Net.Http;


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
        /// <param name="application">Application name</param>
        public Task<IhcUser> Authenticate(string userName, string password, Application application = Application.openapi);

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
        // Concrete rather than ICookieHandler because this service MINTS the handler every other service
        // borrows, so the field is never anything else - and CA1859 refuses the interface where the concrete
        // type is known, which it is at both assignments.
        private readonly CookieHandler cookieHandler;

        public ICookieHandler GetCookieHandler()
        {
            return cookieHandler;
        }

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Authentication.AuthenticationService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings, HttpClient? transport)
                : base(cookieHandler, settings, "AuthenticationService", transport) { }

            public Task<outputMessageName2> authenticateAsync(inputMessageName2 request)
            {
                string? cookie = null;

                var result = soapPost<outputMessageName2, inputMessageName2>("authenticate", request, resp =>
                {
                    // Use side-effect to capture cookie sice our post call only captures xml response.
                    cookie = SetCookieHeader.FirstOrNull(resp);
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

            // The cookie is dropped by Disconnect's finally rather than by an on-OK side effect here: the
            // side effect runs only after EnsureSuccessStatusCode, so a controller that answered 5xx left
            // the session reporting disconnected while every later call still presented its cookie.
            public Task<outputMessageName1> disconnectAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("disconnect", request);
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
            this.impl = new SoapImpl(cookieHandler, settings, transport: null);
            this.isConnected = false;
        }

        /// <summary>
        /// Test seam: substitute the HTTP transport (used by unit tests only). Every other service
        /// inherits this one's settings and cookie session, so the login exchange itself is only
        /// reachable controller-free by answering it at the socket - the seam
        /// <see cref="ServiceBaseImpl"/> already carries, which this constructor threads through.
        /// </summary>
        /// <remarks>
        /// The transport is REQUIRED here rather than optional, so that taking one is a truthful claim
        /// that the caller supplied it. The controller-reach guard reads that claim off the signature:
        /// a constructor handed its transport cannot reach a network the caller did not provide.
        /// </remarks>
        internal AuthenticationService(IhcSettings settings, HttpClient transport)
            : base(settings)
        {
            ArgumentNullException.ThrowIfNull(transport);
            this.cookieHandler = new CookieHandler(settings.LogSensitiveData);
            this.impl = new SoapImpl(cookieHandler, settings, transport);
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
            return await Authenticate(settings.UserName, settings.Password, settings.Application)
                .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        public Task<IhcUser> Authenticate(string userName, string password, Application application = Application.openapi)
        {
            // Invariant: the controller expects the ASCII application name, which a Turkish locale would fold to
            // a dotless 'ı' for any member carrying an I.
            return DoAuthenticate(userName, password, application.ToString().ToLowerInvariant());
        }

        private async Task<IhcUser> DoAuthenticate(string userName, string password, string application)
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
                            // The controller reported a SUCCESSFUL login, so a session now exists on it and its
                            // cookie has been captured - but the answer cannot be made into a user, so it is
                            // refused. Nothing else would ever end that session: Dispose logs out only a session
                            // it believes is live, and this one never became connected.
                            await EndTheSessionThisAnswerOpened().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                            throw new ErrorWithCodeException(Errors.LOGIN_UNKNOWN_ERROR, "Ihc server login succeeded but returned null user data for " + impl.Url);
                        }

                        // The session is handed over only once there is a user to hand over with it. The
                        // controller opened it and sent its cookie before the SDK looked at the answer at
                        // all, so a mapping that threw with the session already marked live would leave
                        // IsAuthenticated true and the cookie attached on a login this call reports as
                        // FAILED - and a caller reading that as "login failed" has no reason to dispose,
                        // which is what would otherwise end the session.
                        IhcUser user;
                        try
                        {
                            user = MapLoggedInUser(result.loggedInUser);
                        }
                        catch (Exception ex)
                        {
                            await EndTheSessionThisAnswerOpened().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                            throw new ErrorWithCodeException(Errors.LOGIN_UNKNOWN_ERROR,
                                "Ihc server login succeeded but returned user data that could not be mapped for " + impl.Url, ex);
                        }

                        lock (isConnectedLock)
                        {
                            isConnected = true;
                        }

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

        /// <summary>
        /// The user the login answer carries.
        /// </summary>
        /// <remarks>
        /// The dates are nullable on this wire and read as unset when absent, the way every other WSDate the
        /// SDK maps does - <see cref="UserManagerService"/> included, which maps these very fields off the
        /// same record and which this has to agree with about one account.
        /// </remarks>
        private static IhcUser MapLoggedInUser(WSUser loggedInUser) => new()
        {
            Username = loggedInUser.username,
            Password = loggedInUser.password,
            Firstname = loggedInUser.firstname,
            Lastname = loggedInUser.lastname,
            Phone = loggedInUser.phone,
            Email = loggedInUser.email,
            Group = UserManagerService.mapUserGroup(loggedInUser.group?.type),
            Project = loggedInUser.project,
            CreatedDate = DateHelper.OrAbsentSentinel(loggedInUser.createdDate?.ToDateTimeOffset(), nameof(IhcUser.CreatedDate)),
            LoginDate = DateHelper.OrAbsentSentinel(loggedInUser.loginDate?.ToDateTimeOffset(), nameof(IhcUser.LoginDate)),
        };

        /// <summary>
        /// Ends a controller session this instance opened but will not hand to its caller.
        /// </summary>
        /// <remarks>
        /// Best effort: a controller that will not answer the logout leaves nothing this side can do about
        /// the session, and the refusal the caller is about to receive is the failure worth reporting.
        /// <see cref="Disconnect"/> drops the cookie either way, so no later call rides a session this
        /// instance has disowned.
        /// </remarks>
        private async Task EndTheSessionThisAnswerOpened()
        {
            try
            {
                await Disconnect().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            }
            catch (Exception)
            {
                // Deliberately swallowed; see above.
            }
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
                        // BOTH halves of the local session end, whatever the controller answered. Clearing
                        // the flag alone left the cookie attached to every later request, so a session
                        // reporting itself disconnected went on presenting itself as live on the wire.
                        lock (isConnectedLock)
                        {
                            isConnected = false;
                        }
                        cookieHandler.SetCookie(null);
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
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disconnects the session. A derived type that overrides this must call the base implementation.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>, false from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

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