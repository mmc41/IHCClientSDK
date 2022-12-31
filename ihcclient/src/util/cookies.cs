using System.Threading.Tasks;
using System;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Ihc {
    /**
    * Cookie managment interface.
    */
    public interface ICookieHandler
    {
        string GetCookie();
        void SetCookie(string _cookie);
    }

    /**
    * Interface that authentication services provide.
    */
    public interface ICookieHandlerService
    {
        ICookieHandler GetCookieHandler();
    }

    internal sealed class CookieHandler : ICookieHandler
    {
        private readonly object _lock = new object();
        private readonly ILogger logger;
        private string cookie = null;

        public CookieHandler(ILogger logger)
        {
            this.logger = logger;
        }

        public string GetCookie()
        {
            lock (_lock)
            {
                logger.LogTrace("USING COOKIE '" + cookie + "'");
                return cookie;
            }
        }

        public void SetCookie(string _cookie)
        {
            lock (_lock)
            {
                if (_cookie == null)
                  logger.LogInformation("CLEARING COOKIE");
                else logger.LogInformation("SETTING COOKIE TO: '" + cookie + "'");

                cookie = _cookie;
            }
        }
    }
}